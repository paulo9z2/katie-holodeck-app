// KatieHoloDeck Liquid Glass GPU (twin Oyenza) — rota B, P/Invoke puro.
// Motor D3D11 do efeito liquid: lente convexa na borda (refração REAL),
// aberração cromática 3-fetch, frost duplo via mip chain (GenerateMips no C#),
// rim light bidirecional, glow interno, sombra adaptativa, tint com flip
// claro/escuro (dead band no CPU — shadow.w).
// Input: srcTex = proxy do painel (BGRA PREMULTIPLIED, W×H px do RTB).
// Output: BGRA premultiplied direto pro WriteableBitmap (Pbgra32).
// Compilado em runtime por D3DCompile (d3dcompiler_47.dll) — vs_5_0/ps_5_0.
//
// 27/10: SEM flip Y (o pipeline RTB→staging→WB é top-down nas duas pontas);
// saturate(dAbs/bevel) no perfil da lente (pow de base negativa = NaN no miolo).

cbuffer GlassParams : register(b0)
{
    float2 body;       // tamanho da FACE px
    float2 shape;       // .x = corner radius, .y = shoulder
    float  pxPerDIP;    // dpi/96
    int    useCopy;     // 1 = backdrop disponível
    float4 lens;        // .x=bevel, .y=mag, .z=rimPower, .w=frostMix
    float4 tintDark;    // cor straight + alpha
    float4 tintLight;   // cor straight + alpha
    float3 lightDir;    // normalizada
    float  lightRim;
    float  lightGlow;
    float4 shadow;      // .x=alpha, .y=reach, .z=drop, .w=lightMix (flip CPU)
    float4 copy;        // .x=W RT, .y=H RT, .z=saturation, .w=dispersion px
    float4 probe;       // zeros no twin (sem captura de tela)
    float2 origin;      // top-left da FACE no RT (pad px)
};

Texture2D<float4> srcTex : register(t0);
SamplerState linearClamp : register(s0);

// ── SDF da silhueta notch: topo reto + ombros côncavos + cantos arredondados
float inside(float2 q)
{
    float2 c = body * 0.5;
    float2 d = abs(q - c);
    float r = shape.x;
    float shoulder = shape.y;
    float topY = c.y - r;
    float sideX = c.x - shoulder;

    if (d.y > c.y - r) {
        // Metade de baixo: rounded rect padrão
        float2 q2 = max(d - c + r, 0);
        return length(q2) - r;
    } else if (d.x > sideX) {
        // Ombro côncavo
        float2 sc = float2(sideX, topY);
        return length(d - sc) - r;
    } else {
        // Topo reto (notch)
        return d.y - topY;
    }
}

// ── Perfil da lente: mag * (1 - d/bevel)^rimPower (offset SEMPRE p/ dentro)
float rim_offset(float d, float bevel, float mag, float rimPower)
{
    if (d >= bevel) return 0.0;
    float t = 1.0 - d / bevel;
    return mag * pow(t, rimPower);
}

// ── VS: triângulo fullscreen via SV_VertexID (sem vertex buffer, sem input layout)
float4 VS_Main(uint id : SV_VertexID) : SV_Position
{
    float2 uv = float2((id << 1) & 2, id & 2);   // (0,0) (2,0) (0,2)
    return float4(uv * float2(2, -2) + float2(-1, 1), 0, 1);
    // → (-1,+1) (+3,+1) (-1,-3): cobre o viewport inteiro
}

// ── PS: o vidro
float4 PS_Main(float4 pos : SV_Position) : SV_Target
{
    float2 p = pos.xy;                 // px do RT
    float2 q = p - origin;             // face space (face começa no pad)
    float d = inside(q);
    if (d > 0.0) return float4(0, 0, 0, 0);   // fora da face: transparente

    float bevel = lens.x, mag = lens.y, rimP = lens.z, frostMix = lens.w;
    float dAbs = abs(d);
    float2 c = body * 0.5;
    float2 toC = c - q;
    float2 dir = toC / max(length(toC), 1e-4);   // aponta PRA DENTRO (guard div0)

    float2 uv = p / copy.xy;                     // espaço do RT
    float offset = rim_offset(dAbs, bevel, mag, rimP);
    float2 dUV = dir * offset / copy.xy;         // deslocamento da lente
    float2 cUV = dir * copy.w / copy.xy;         // dispersão cromática px→uv

    // ── Refração: 3 fetches (R/G/B com dispersão)
    float4 colR = srcTex.Sample(linearClamp, uv + dUV + cUV);
    float4 colG = srcTex.Sample(linearClamp, uv + dUV);
    float4 colB = srcTex.Sample(linearClamp, uv + dUV - cUV);
    float4 sharp = float4(colR.r, colG.g, colB.b, colG.a);   // premultiplied

    // ── Frost duplo via mip chain (σ3 ≈ LOD 1.35, σ9 ≈ LOD 2.70)
    float4 frostLo = srcTex.SampleLevel(linearClamp, uv + dUV * 0.5, 1.35);
    float4 frostHi = srcTex.SampleLevel(linearClamp, uv, 2.70);
    float4 frost = lerp(frostLo, frostHi, frostMix);

    // ── Material: borda nítida refratada, miolo frost — w = (1-x)^1.3
    float x = saturate(dAbs / bevel);   // 0 na borda, 1 no centro (saturate: NaN guard)
    float w = pow(1.0 - x, 1.3);
    float4 material = lerp(frost, sharp, w);

    // ── Saturação
    float luma = dot(material.rgb, float3(0.2126, 0.7152, 0.0722));
    float3 sat = luma + (material.rgb - luma) * copy.z;

    // ── Tint com flip claro/escuro (lightMix vem do CPU com dead band)
    float4 tint = lerp(tintDark, tintLight, shadow.w);
    float alpha = material.a * tint.a;
    float3 rgb = sat * tint.rgb * tint.a;   // premultiplied correto

    // ── Rim light bidirecional (normal da lente)
    float3 nOut = normalize(float3(-dir, 0.55));
    float facing = max(dot(nOut, lightDir), 0.0);
    float opposite = max(dot(-nOut, lightDir), 0.0);
    float rim = pow(facing, 2.0) * lightRim + 0.85 * pow(opposite, 2.0) * lightRim;

    // ── Glow interno exponencial
    float glow = exp(-(1.0 - x) * 8.0) * lightGlow;

    // ── Sombra adaptativa (borda interna)
    float sd = max(0.0, shadow.y - dAbs) / max(shadow.y, 1e-4);
    float shad = pow(sd, 2.0) * shadow.x * shadow.z;

    // Aditivos modulados pelo alpha (premult consistente pro Pbgra32)
    return float4(rgb + (rim + glow - shad) * alpha, alpha);
}
