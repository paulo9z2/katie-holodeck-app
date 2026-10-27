// KatieHoloDeck Liquid Glass — Port do glass.hlsl do Oyenza/Poni (v0.2.6)
// Shader HLSL para refração real (lente convexa na borda), frost duplo, aberração cromática,
// rim light bidirecional, glow interno, sombra adaptativa, tint WCAG automático.
// Compilado em runtime via D3DCompile (d3dcompiler_47.dll do System32).

cbuffer GlassParams : register(b0)
{
    // === BODY ===
    float2 body;                  // island size (px, DIPs)
    float2 shape;                 // .x = corner radius, .y = shoulder (concavity depth)
    float  pxPerDIP;              // 1.0 @ 96 DPI, scales with monitor DPI
    int    useCopy;               // 1 = backdrop copy available (sharp/frostLo/frostHi)

    // === LENS (borda convexa) ===
    float4 lens;                  // .x=bevel(band width), .y=mag(magnitude), .z=rimPower, .w=frostMix
    float4 tintDark;              // dark glass tint (rgba, premultiplied)
    float4 tintLight;             // light glass tint (rgba, premultiplied)

    // === LIGHT ===
    float3 lightDir;              // normalized, world space (e.g., top-left)
    float  lightRim;              // rim strength
    float  lightGlow;             // inner glow strength

    // === SHADOW ===
    float4 shadow;                // .x=alpha, .y=reach, .z=drop, .w=lightMix (0=dark..1=light)

    // === COPY ===
    float4 copy;                  // .x=width, .y=height, .z=saturation, .w=dispersion
    float4 probe;                 // .x=x, .y=draw, .z=lens, .w=smoked (anti-self-capture)

    // === ORIGIN ===
    float2 origin;                // copyTopLeftInWindowPx (when window moves/resizes)
};

Texture2D<float4> sharpTex : register(t0);   // backdrop nítido (BGRA premultiplied)
Texture2D<float4> frostLoTex : register(t1); // blur σ=3 DIPs
Texture2D<float4> frostHiTex : register(t2); // blur σ=9 DIPs

SamplerState linearClamp : register(s0);     // Linear, Clamp
SamplerState pointClamp  : register(s1);     // Point, Clamp

// ── Signed distance to island silhouette (notch shape: top flat, concave shoulders, rounded corners)
float inside(float2 p)
{
    float2 c = body * 0.5;
    float2 d = abs(p - c);
    float r = shape.x;

    // Top edge: straight across screen top (y=0), shoulders curve inward
    float shoulder = shape.y;
    float topY = c.y - r;
    float sideX = c.x - shoulder;

    if (d.y > c.y - r) {
        // Bottom half: standard rounded rect
        float2 q = max(d - c + r, 0);
        return length(q) - r;
    } else if (d.x > sideX) {
        // Shoulder concave curve
        float2 sc = float2(sideX, topY);
        float sd = length(d - sc) - r;
        return sd;
    } else {
        // Top flat (notch)
        return d.y - topY;
    }
}

// ── Rim offset: magnitude * (1 - d/bevel)^RIM_POWER
// Guard: mag * RIM_POWER / bevel < 0.85 → sample ALWAYS walks INWARD (magnifies)
float rim_offset(float d, float bevel, float mag, float rimPower)
{
    if (d >= bevel) return 0.0;
    float t = 1.0 - d / bevel;
    return mag * pow(t, rimPower);
}

// ── Main PS
float4 PS_Main(float4 pos : SV_Position) : SV_Target
{
    float2 p = pos.xy;
    float2 c = body * 0.5;

    // Probe pixels (invisible, nudged RGB exact values) — detect self-capture
    if (probe.y > 0.5) {
        float2 probePos = float2(probe.x, 0);
        if (all(abs(p - probePos) < 1.5)) {
            // Return nudged signature (R=12, G=34, B=56, A=78)
            return float4(12, 34, 56, 78) / 255.0;
        }
    }

    // Signed distance
    float d = inside(p);

    // Outside island: transparent
    if (d > 0.0) {
        return float4(0, 0, 0, 0);
    }

    // Smoked glass fallback (no refraction)
    if (probe.w > 0.5) {
        float4 ground = float4(0.08, 0.08, 0.09, 0.82); // SMOKED_OPACITY
        float facing = max(dot(normalize(float3(0,0,-1)), lightDir), 0.0);
        float rim = pow(facing, 2.0) * lightRim;
        return ground + float4(rim, rim, rim, 0);
    }

    // === UV in copy texture space ===
    float2 uv = (p - origin) / copy.xy;
    uv.y = 1.0 - uv.y; // flip Y (D3D11 texture origin top-left)

    // Clamp to copy bounds
    if (uv.x < 0 || uv.x > 1 || uv.y < 0 || uv.y > 1) {
        return float4(0, 0, 0, 0);
    }

    // === Lens offset ===
    float bevel = lens.x;
    float mag   = lens.y;
    float rimP  = lens.z;
    float frostMix = lens.w;

    float dAbs = abs(d); // distance inside (0 at edge, positive inward)
    float offset = rim_offset(dAbs, bevel, mag, rimP);
    float2 offsetDir = normalize(c - p); // points INWARD from edge
    float2 sampleUV = uv + offsetDir * offset * float2(1.0 / copy.x, 1.0 / copy.y);

    // === Chromatic aberration: 3 fetches (R/G/B offsets) ===
    float dispersion = copy.w; // 0.03
    float2 uvR = sampleUV + offsetDir * dispersion * float2(1.0 / copy.x, 1.0 / copy.y);
    float2 uvB = sampleUV - offsetDir * dispersion * float2(1.0 / copy.x, 1.0 / copy.y);

    float4 colR = sharpTex.Sample(linearClamp, uvR);
    float4 colG = sharpTex.Sample(linearClamp, sampleUV);
    float4 colB = sharpTex.Sample(linearClamp, uvB);

    float4 sharp = float4(colR.r, colG.g, colB.b, colG.a);

    // === Frost dual (lo/hi) ===
    float4 frostLo = frostLoTex.Sample(linearClamp, sampleUV);
    float4 frostHi = frostHiTex.Sample(linearClamp, sampleUV);
    float4 frost = lerp(frostLo, frostHi, frostMix);

    // === Material blend: lerp(frost, sharp, w) where w = (1-x)^1.3, x = normalized dist
    float x = dAbs / bevel; // 0 at edge, 1 at center
    float w = pow(1.0 - x, 1.3);
    float4 material = lerp(frost, sharp, w);

    // === Saturation boost ===
    float saturation = copy.z; // 1.45
    float luma = dot(material.rgb, float3(0.2126, 0.7152, 0.0722));
    float4 sat = float4(luma + (material.rgb - luma) * saturation, material.a);

    // === Light/dark flip (pill small follows backdrop) ===
    // Compute light_mix from backdrop (handled on CPU, passed via shadow.w)
    float lightMix = shadow.w;

    // === Tint interpolation (dark <-> light) ===
    float4 tint = lerp(tintDark, tintLight, lightMix);
    float4 tinted = sat * tint; // premultiplied multiply

    // === Rim light (bidirectional) ===
    // Normal points outward from center
    float3 nOut = normalize(float3(offsetDir, sqrt(max(0, 1 - dot(offsetDir, offsetDir)))));
    float facing = max(dot(nOut, lightDir), 0.0);
    float opposite = max(dot(-nOut, lightDir), 0.0);
    float rim = pow(facing, 2.0) * lightRim + 0.85 * pow(opposite, 2.0) * lightRim;

    // === Inner glow (exponential) ===
    float glowDist = 1.0 - x; // 0 at center, 1 at edge
    float glow = exp(-glowDist * 8.0) * lightGlow;

    // === Shadow (adaptive) ===
    float shadowAlpha = shadow.x; // busy? more : less
    float shadowReach = shadow.y;
    float shadowDrop  = shadow.z;
    // Shadow offset from edge inward
    float sd = max(0, shadowReach - dAbs) / shadowReach;
    float shadow = pow(sd, 2.0) * shadowAlpha * shadowDrop;

    // === Final compose ===
    float3 finalRGB = tinted.rgb + rim + glow - shadow;
    float finalA = tinted.a;

    // Premultiplied output
    return float4(finalRGB * finalA, finalA);
}

// ── Vertex Shader (passthrough, 1 triangle covering island)
struct VS_IN { float2 pos : POSITION; };
struct PS_IN { float4 pos : SV_POSITION; };

PS_IN VS_Main(VS_IN input)
{
    PS_IN output;
    output.pos = float4(input.pos, 0, 1);
    return output;
}

// Technique
technique10 Glass
{
    pass P0
    {
        SetVertexShader(CompileShader(vs_4_0, VS_Main()));
        SetPixelShader(CompileShader(ps_4_0, PS_Main()));
    }
}