// ============================================================================
//  LiquidGlass.hlsl — KatieHoloDeck T3 (24/10/2026)
//  Refração real (Snell) + chromatic aberration (Liquify) sobre backdrop.
//
//  Base: spike F0 (21/10, 149 slots, provado) — matemática reescrita analítica
//  a partir de [[LiquidLens - Ótica e Física da Refração]] + FATOS_F0.md.
//  Sem loops, sem tabelas: MAXMAG assado (1.1031322746349106).
//
//  Registers (WPF PixelShaderConstantCallback — um DP por register, layout F0):
//    c0.xy = InnerSize (px da FACE, sem pad)   [Size,  callback 0]
//    c1.x  = Radius   (canto arredondado)      [double, callback 1]
//    c2.x  = Chamfer  (chanfro 45°; 0 = rect)  [double, callback 2]
//    c3.x  = Bezel    (faixa refrativa, px)    [double, callback 3]
//    c4.x  = Refraction (escala px, pico)      [double, callback 4]
//    c5.x  = Chromatic (0 = mono, 1 = Liquify) [double, callback 5]
//    c6.xy = Pad      (overscan px por lado)   [Size,  callback 6]
//
//  Overscan embutido (mata o gargalo #1 do F0 — clamp da dobra):
//  o host é maior que a face por 2*Pad px em cada eixo; o SDF é da FACE,
//  o sampling alcança Pad px além da borda => a dobra lê conteúdo real.
// ============================================================================

sampler2D Input : register(s0);

// Constants declarados explicitamente (fxc moderno não tem c0..cN implícito).
// WPF escreve por ÍNDICE de register (PixelShaderConstantCallback), nomes são livres.
float2 c0 : register(c0);   // InnerSize (face px)
float2 c1 : register(c1);   // Radius   (c1.x)
float2 c2 : register(c2);   // Chamfer  (c2.x)
float2 c3 : register(c3);   // Bezel    (c3.x)
float2 c4 : register(c4);   // Refraction (c4.x)
float2 c5 : register(c5);   // Chromatic  (c5.x)
float2 c6 : register(c6);   // Pad (overscan px por lado)

float4 main(float2 uv : TEXCOORD) : COLOR
{
    float2 sz = c0.xy;         // face px
    float  rd = c1.x;          // radius
    float  ch = c2.x;          // chamfer 45
    float  bz = c3.x;          // bezel
    float  rf = c4.x;          // refraction
    float  cr = c5.x;          // chromatic
    float2 pd = c6.xy;         // overscan por lado

    float2 fz = sz + 2.0 * pd;                    // tamanho real do host/textura
    float2 p  = uv * fz - pd - sz * 0.5;          // px, origem no CENTRO da face
    float2 hs = sz * 0.5;

    // ---- clamps internos (NaN nunca entra; bezel nunca engole a face) ----
    float hmin = min(hs.x, hs.y);
    rd = clamp(rd, 0.0, hmin);
    ch = clamp(ch, 0.0, min(hs.x, hs.y) * 0.5);
    bz = clamp(bz, 0.0, hmin);

    // ---- SDF rounded rect (iq, analítico, sem loop) ----
    float2 q   = abs(p) - (hs - rd);
    float2 ofl = max(q, 0.0);
    float sd   = length(ofl) + min(max(q.x, q.y), 0.0) - rd;

    // ---- corte octogonal 45°: plano |x|+|y| = hx+hy-ch ----
    float cut = (abs(p.x) + abs(p.y) - (hs.x + hs.y - ch)) * 0.70710678;

    // ---- smooth-max (fillet 3px no kink do chanfro) ----
    float k  = 3.0;
    float sm = saturate(0.5 + 0.5 * (cut - sd) / k);
    float sdo = lerp(sd, cut, sm) + k * sm * (1.0 - sm);

    // ---- normal outward = gradiente de tela do SDF (ddx/ddy, 1 avaliação) ----
    float2 n = normalize(float2(ddx(sdo), ddy(sdo)));

    // ---- máscara alpha: 1 dentro, AA 1px na borda (padrão F0) ----
    float alpha = saturate(-sdo);

    // ---- perfil Snell squircle (n=1.5), fechado, sem loop ----
    // height(x) = (1-(1-x)^4)^(1/4);  dh/dx = ((1-x)/h)^3;
    // sin/cos fechados a partir de dh (zero trig de biblioteca).
    float depth = -sdo;                           // >0 dentro
    float x = saturate(depth / max(bz, 0.0001));  // 0 borda -> 1 interior
    float u = 1.0 - x;
    float h1 = pow(max(1.0 - pow(u, 4.0), 0.0), 0.25);
    float iv = u / max(h1, 1e-4);
    float dh = iv * iv * iv;
    float ca = 1.0 / sqrt(1.0 + dh * dh);         // cos(theta1)
    float sa = dh * ca;                          // sin(theta1)
    float sb = sa * 0.66666667;                  // Snell: n1/n2 = 1/1.5
    float cb = sqrt(max(0.0, 1.0 - sb * sb));
    float sD = sa * cb - ca * sb;                // sin(theta1 - theta2)
    float cD = ca * cb + sa * sb;                // cos(theta1 - theta2)
    float deflect = sD / max(cD, 1e-4);           // tan(theta1 - theta2)
    float mag = deflect * (rf / 1.1031322746349106);  // px normalizados (pico ~ rf)

    // ---- displacement: SEM flow control (WPF ShaderEffect não roda branch
    // dinâmico no ps_3_0: pipeline quebra e o sampler volta preto).
    // Linear sempre: 3 fetches, cr=0 reduz a mono puro pelos fatores 1.
    float kg = 1.0 - 0.075 * cr;              // G: 0.925 @ cr=1
    float kb = 1.0 - 0.125 * cr;             // B: 0.875 @ cr=1
    float3 tr = tex2D(Input, uv + n * (mag      / fz)).rgb;
    float3 tg = tex2D(Input, uv + n * (mag * kg / fz)).rgb;
    float3 tb = tex2D(Input, uv + n * (mag * kb / fz)).rgb;
    float3 rgb = float3(tr.r, tg.g, tb.b);

    return float4(rgb, alpha);
}
