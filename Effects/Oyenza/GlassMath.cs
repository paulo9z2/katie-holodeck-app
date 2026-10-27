using System;
using System.Numerics;
using System.Runtime.InteropServices;

namespace KatieHoloDeck.LiquidGlass;

/// <summary>Constantes de material Oyenza v0.2.6 (tuning do spike 26/10).
/// Tints: cor STRAIGHT + opacidade — o shader faz o premultiply.</summary>
public static class GlassConsts
{
    public const float FrostLodLo = 1.35f;  // mip LOD ~ σ3 (frost fino)
    public const float FrostLodHi = 2.70f; // mip LOD ~ σ9 (frost grosso)
    public const float ChromaticK = 0.125f; // dispersão px = mag * chromatic * K

    /// <summary>Vidro escuro (fundo claro): cor straight + opacidade.</summary>
    public static readonly Vector4 TintDark = new(0.10f, 0.10f, 0.12f, 0.62f);
    /// <summary>Vidro claro (fundo escuro): cor straight + opacidade.</summary>
    public static readonly Vector4 TintLight = new(0.88f, 0.90f, 0.94f, 0.55f);
    /// <summary>Direção da luz (normalizada): canto superior esquerdo.</summary>
    public static readonly Vector3 LightDir = Vector3.Normalize(new Vector3(-0.35f, -0.80f, 0.49f));
}

/// <summary>
/// Constant buffer do shader glass — byte-a-byte com o cbuffer HLSL (rows de 16).
/// Layout explícito porque o HLSL empacota float3+float em rows de 16 (pads
/// 24-31 e 100-111); LayoutKind.Sequential leria lixo no driver.
/// 27/10: struct flat original promovido a Explicit (port do spike validado).
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 176)]
public readonly struct GlassParams
{
    // Computed constants (não ocupam layout — consts)
    public const float RIM_FOLD_GUARD = 0.85f;
    public const float SATURATION = 1.45f;
    public const float DISPERSION = 0.03f;
    public const float RIM_POWER = 1.6f;
    public const float SMOKED_OPACITY = 0.82f;

    // Row 0 (0-15)
    [FieldOffset(0)]  public readonly Vector2 Body;      // tamanho da FACE em px
    [FieldOffset(8)]  public readonly Vector2 Shape;     // .x = corner radius px, .y = shoulder px

    // Row 16
    [FieldOffset(16)] public readonly float PxPerDip;    // dpi/96
    [FieldOffset(20)] public readonly int UseCopy;       // 1 = backdrop disponível
    // pad 24-31

    // Rows 32-79
    [FieldOffset(32)] public readonly Vector4 Lens;      // .x=bevel px, .y=mag px, .z=rimPower, .w=frostMix
    [FieldOffset(48)] public readonly Vector4 TintDark;   // cor straight + alpha
    [FieldOffset(64)] public readonly Vector4 TintLight;  // cor straight + alpha

    // Row 80-99
    [FieldOffset(80)] public readonly Vector3 LightDir;   // normalizada
    [FieldOffset(92)] public readonly float LightRim;
    [FieldOffset(96)] public readonly float LightGlow;
    // pad 100-111

    // Rows 112-159
    [FieldOffset(112)] public readonly Vector4 Shadow;    // .x=alpha, .y=reach px, .z=drop, .w=lightMix (CPU flip)
    [FieldOffset(128)] public readonly Vector4 Copy;      // .x=W px, .y=H px, .z=saturation, .w=dispersão px
    [FieldOffset(144)] public readonly Vector4 Probe;     // twin sem captura de tela → zeros

    // Row 160
    [FieldOffset(160)] public readonly Vector2 Origin;    // top-left da FACE no RT (pad px)
    // pad 168-175

    public GlassParams(Vector2 body, Vector2 shape, float pxPerDip, int useCopy,
        Vector4 lens, Vector4 tintDark, Vector4 tintLight, Vector3 lightDir,
        float lightRim, float lightGlow, Vector4 shadow, Vector4 copy,
        Vector4 probe, Vector2 origin)
    {
        Body = body; Shape = shape; PxPerDip = pxPerDip; UseCopy = useCopy;
        Lens = lens; TintDark = tintDark; TintLight = tintLight; LightDir = lightDir;
        LightRim = lightRim; LightGlow = lightGlow; Shadow = shadow; Copy = copy;
        Probe = probe; Origin = origin;
    }
}

/// <summary>
/// Maths puras do liquid glass — port de poni_core::glass
/// </summary>
public static class GlassMath
{
    // Thickness interpolation (pill 37 DIPs → page 220 DIPs)
    private const float THIN_H = 37.0f;
    private const float THICK_H = 220.0f;
    private const float FLIP_TO_LIGHT = 0.62f;
    private const float FLIP_TO_DARK = 0.45f;

    /// <summary>Interpolação linear — MathF.Lerp não existe no .NET 8.</summary>
    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    private static Vector4 Lerp4(in Vector4 a, in Vector4 b, float t) =>
        new(Lerp(a.X, b.X, t), Lerp(a.Y, b.Y, t), Lerp(a.Z, b.Z, t), Lerp(a.W, b.W, t));

    private static Vector2 Abs2(in Vector2 v) => new(MathF.Abs(v.X), MathF.Abs(v.Y));

    private static Vector2 Max2(in Vector2 a, in Vector2 b) =>
        new(MathF.Max(a.X, b.X), MathF.Max(a.Y, b.Y));

    /// <summary>Calcula parâmetros de espessura baseados na altura da island</summary>
    public static (float bevel, float magnitude, float frostMix, float shadowReach, float shadowDrop) Thickness(float h)
    {
        float t = Math.Clamp((h - THIN_H) / (THICK_H - THIN_H), 0f, 1f);
        float bevel = Lerp(9f, 24f, t);
        float mag = 0.85f * bevel / GlassParams.RIM_POWER; // RIM_FOLD_GUARD * bevel / RIM_POWER
        float frostMix = t; // 0=lo, 1=hi
        float shadowReach = Lerp(5f, 7f, t);
        float shadowDrop = Lerp(1.5f, 3f, t);
        return (bevel, mag, frostMix, shadowReach, shadowDrop);
    }

    /// <summary>Rim offset: mag * (1 - d/bevel)^RIM_POWER — garante offset INWARD (amplia)</summary>
    public static float RimOffset(float d, float bevel, float mag, float rimPower = GlassParams.RIM_POWER)
    {
        if (d >= bevel) return 0f;
        float t = 1f - d / bevel;
        return mag * MathF.Pow(t, rimPower);
    }

    /// <summary>Flip light/dark com dead band + hold (anti-flicker)</summary>
    public static float UpdateFlip(float lightMix, float dt, ref float flipHoldTimer, ref bool isLight)
    {
        const float HOLD = 0.25f;
        if (isLight) {
            if (lightMix < FLIP_TO_DARK) {
                flipHoldTimer += dt;
                if (flipHoldTimer >= HOLD) { isLight = false; flipHoldTimer = 0f; }
            } else flipHoldTimer = 0f;
        } else {
            if (lightMix > FLIP_TO_LIGHT) {
                flipHoldTimer += dt;
                if (flipHoldTimer >= HOLD) { isLight = true; flipHoldTimer = 0f; }
            } else flipHoldTimer = 0f;
        }
        return isLight ? 1f : 0f;
    }

    /// <summary>Tint para contraste WCAG 4.5:1 — busca binária na mediana (não média!)</summary>
    public static Vector4 TintForContrast(Vector4 material, Vector4 ink, Vector4 toward, float min, float max, float target = 4.8f)
    {
        float lo = min, hi = max;
        for (int i = 0; i < 20; i++) {
            float mid = (lo + hi) * 0.5f;
            Vector4 test = Lerp4(material, toward, mid);
            float L1 = RelativeLuminance(test);
            float L2 = RelativeLuminance(ink);
            float contrast = (MathF.Max(L1, L2) + 0.05f) / (MathF.Min(L1, L2) + 0.05f);
            if (contrast >= target) hi = mid; else lo = mid;
        }
        return Lerp4(material, toward, hi);
    }

    private static float RelativeLuminance(Vector4 c)
    {
        float R = Linearize(c.X);
        float G = Linearize(c.Y);
        float B = Linearize(c.Z);
        return 0.2126f * R + 0.7152f * G + 0.0722f * B;
    }

    private static float Linearize(float c)
    {
        return c <= 0.04045f ? c / 12.92f : MathF.Pow((c + 0.055f) / 1.055f, 2.4f);
    }

    /// <summary>Alpha da sombra adaptativa (busy → mais profunda)</summary>
    public static float ShadowAlpha(bool busy, float thick) => busy ? Lerp(0.35f, 0.55f, thick) : Lerp(0.15f, 0.35f, thick);

    /// <summary>Signed distance à silhueta notch (topo reto + ombros côncavos + cantos arredondados)</summary>
    public static float Inside(Vector2 p, Vector2 body, float radius, float shoulder)
    {
        Vector2 c = body * 0.5f;
        Vector2 d = Abs2(p - c);

        float topY = c.Y - radius;
        float sideX = c.X - shoulder;

        if (d.Y > c.Y - radius) {
            // Bottom half: rounded rect
            Vector2 q = Max2(d - c + new Vector2(radius, radius), Vector2.Zero);
            return q.Length() - radius;
        } else if (d.X > sideX) {
            // Shoulder concave
            Vector2 sc = new(sideX, topY);
            return (d - sc).Length() - radius;
        } else {
            // Top flat (notch)
            return d.Y - topY;
        }
    }
}

/// <summary>
/// Flip state (light/dark glass) — port de poni_core::glass::Flip
/// </summary>
public struct FlipState
{
    public bool IsLight;
    public float HoldTimer;

    public float Update(float lightMix, float dt)
    {
        return GlassMath.UpdateFlip(lightMix, dt, ref HoldTimer, ref IsLight);
    }
}
