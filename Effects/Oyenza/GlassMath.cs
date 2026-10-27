using System;
using System.Numerics;
using System.Runtime.InteropServices;

namespace KatieHoloDeck.LiquidGlass;

/// <summary>
/// Parâmetros do liquid glass — espelha poni_core::glass + glass.rs GlassParams.
/// Port Oyenza 26/10: stdlib pura (sem Vortice — os using do spike não eram
/// usados nos corpos). Correções de port: MathF.Lerp não existe em .NET 8
/// (helper Lerp local); float2 é HLSL, não C# (Vector2).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct GlassParams
{
    // Body
    public readonly Vector2 Body;           // island size (DIPs)
    public readonly float CornerRadius;     // DIPs
    public readonly float ShoulderDepth;    // concave shoulder depth (DIPs)
    public readonly float PxPerDip;         // dpi/96

    // Lens
    public readonly float Bevel;            // border band width (DIPs)
    public readonly float Magnitude;        // lens magnitude (DIPs)
    public readonly float RimPower;         // 1.6
    public readonly float FrostMix;         // 0..1 (thin→thick)

    // Tint
    public readonly Vector4 TintDark;       // premultiplied RGBA
    public readonly Vector4 TintLight;      // premultiplied RGBA

    // Light
    public readonly Vector3 LightDir;       // normalized
    public readonly float LightRim;
    public readonly float LightGlow;

    // Shadow
    public readonly float ShadowAlpha;
    public readonly float ShadowReach;
    public readonly float ShadowDrop;
    public readonly float LightMix;         // 0=dark glass .. 1=light glass

    // Copy/backdrop
    public readonly Vector2 CopySize;       // texture size (px)
    public readonly float Saturation;       // 1.45
    public readonly float Dispersion;       // 0.03 chromatic aberration

    // Probe (anti-self-capture)
    public readonly float ProbeX;
    public readonly int ProbeDraw;
    public readonly int ProbeLens;
    public readonly int ProbeSmoked;

    // Origin
    public readonly Vector2 Origin;         // copy top-left in window px

    // Use copy
    public readonly int UseCopy;

    // Computed constants
    public const float RIM_FOLD_GUARD = 0.85f;
    public const float SATURATION = 1.45f;
    public const float DISPERSION = 0.03f;
    public const float RIM_POWER = 1.6f;
    public const float SMOKED_OPACITY = 0.82f;
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
            // Luminância relativa (WCAG)
            float L1 = RelativeLuminance(test);
            float L2 = RelativeLuminance(ink);
            float contrast = (MathF.Max(L1, L2) + 0.05f) / (MathF.Min(L1, L2) + 0.05f);
            if (contrast >= target) hi = mid; else lo = mid;
        }
        return Lerp4(material, toward, hi);
    }

    private static float RelativeLuminance(Vector4 c)
    {
        // sRGB -> linear
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
