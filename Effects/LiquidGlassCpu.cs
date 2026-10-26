// ============================================================================
//  LiquidGlassCpu.cs — KatieHoloDeck T3.3 (25/10/2026)
//  Liquid glass SEM PixelShader: a mesma óptica do LiquidGlass.hlsl portada
//  pra CPU (LUT pré-computada + loop bilinear). Motivação: o pipeline
//  ShaderEffect do WPF não rasteriza bytecode custom nesta máquina (probe
//  GlassProbe v1..v10, 25/10: ps_2_0/3_0, fxc-43/47, sampler custom e
//  implicit — TODOS preto; InvalidPixelShaderEncountered em ps_2_a). CPU é
//  determinístico e roda em qualquer tier.
//
//  Contrato (idêntico ao do fx): face = InnerSize + chanfro/radius; o host é
//  maior por 2*Pad/lado (overscan da dobra). A LUT guarda por pixel do host
//  (device px): normal (nx,ny), deflexão Snell mag (px device) e alpha AA.
//  O fetch por canal usa as razões Liquify (R:G:B = 1:0.925:0.875 @cr=1).
//
//  Custo (painel ~750x108 device px): LUT ~8 ms por resize; loop ~2-3 ms por
//  frame @30 fps ≈ 7% de 1 core — não entra em jeito nenhum de render thread.
// ============================================================================

using System;

namespace KatieHoloDeck.Effects
{
    public sealed class LiquidGlassCpu : IDisposable
    {
        // pico de deflect do perfil Snell n=1.5 (assado do F0; iguais ao HLSL)
        private const double MaxMag = 1.1031322746349106;

        private readonly double _dpi;
        private readonly double _innerW, _innerH, _radius, _chamfer, _bezel, _refraction, _pad;

        private readonly int _w, _h;            // host em device px
        private readonly float[] _lut;          // 4 floats/px: nx, ny, mag, alpha
        private readonly byte[] _straight;      // src un-premultiplied (reuso)
        private readonly byte[] _dst;           // Pbgra de saída (reuso)
        private double _chromatic;

        public int Width => _w;
        public int Height => _h;
        public byte[] Output => _dst;

        /// <summary>Diagnóstico (gate): valores da LUT num pixel.</summary>
        public (float nx, float ny, float mag, float alpha) LutAt(int x, int y)
        {
            int l = (y * _w + x) * 4;
            return (_lut[l], _lut[l + 1], _lut[l + 2], _lut[l + 3]);
        }

        public LiquidGlassCpu(double innerW, double innerH,
                              double radius, double chamfer, double bezel,
                              double refraction, double pad, double dpi,
                              double chromatic)
        {
            if (!double.IsFinite(innerW) || !double.IsFinite(innerH) || innerW < 4 || innerH < 4)
                throw new ArgumentOutOfRangeException("face", $"tamanho inválido ({innerW}×{innerH}) — mount antes do layout?");
            _innerW = innerW; _innerH = innerH;
            _radius = radius; _chamfer = chamfer; _bezel = bezel;
            _refraction = refraction; _pad = pad;
            _dpi = double.IsFinite(dpi) && dpi > 0 ? Math.Max(0.5, dpi) : 1.0;
            _chromatic = chromatic;

            // probe 25/10: unchecked (int)(+inf) = int.MaxValue => _w*_h*4 wrapa NEGATIVO
            // => new float[neg] => OverflowException no dispatcher. Clamp = blindagem total.
            _w = Math.Clamp((int)Math.Round((innerW + 2 * pad) * _dpi), 4, 8192);
            _h = Math.Clamp((int)Math.Round((innerH + 2 * pad) * _dpi), 4, 8192);
            _lut = new float[_w * _h * 4];
            _straight = new byte[_w * _h * 4];
            _dst = new byte[_w * _h * 4];
            BuildLut();
        }

        public double Chromatic
        {
            get => _chromatic;
            set => _chromatic = value;   // kg/kb derivam no loop; LUT não muda
        }

        // ------------------------------------------------------------------
        // LUT — uma vez por construção. SDF squircle+chanfro (iq), gradiente
        // central (mesma semântica do ddx/ddy do HLSL), perfil Snell fechado.
        // ------------------------------------------------------------------
        private void BuildLut()
        {
            double hsx = _innerW * 0.5, hsy = _innerH * 0.5;
            double hmin = Math.Min(hsx, hsy);
            double rd = Clamp(_radius, 0, hmin);
            double ch = Clamp(_chamfer, 0, Math.Min(hsx, hsy) * 0.5);
            double bz = Math.Max(Clamp(_bezel, 0, hmin), 1e-4);
            double rf = _refraction;

            int l = 0;
            for (int y = 0; y < _h; y++)
            {
                for (int x = 0; x < _w; x++, l += 4)
                {
                    // px device → dip, origem no CENTRO da face
                    double px = (x + 0.5) / _dpi - _pad - _innerW * 0.5;
                    double py = (y + 0.5) / _dpi - _pad - _innerH * 0.5;

                    double sdo = Sdf(px, py, hsx, hsy, rd, ch);
                    float alpha = (float)Sat(-sdo);

                    if (alpha <= 0f) { _lut[l] = 0; _lut[l + 1] = 0; _lut[l + 2] = 0; _lut[l + 3] = 0; continue; }

                    // normal = gradiente central do SDF (ddx/ddy do shader)
                    double e = 1.0 / _dpi;   // 1 device px em dip
                    double gx = Sdf(px + e, py, hsx, hsy, rd, ch) - Sdf(px - e, py, hsx, hsy, rd, ch);
                    double gy = Sdf(px, py + e, hsx, hsy, rd, ch) - Sdf(px, py - e, hsx, hsy, rd, ch);
                    double glen = Math.Sqrt(gx * gx + gy * gy);
                    if (glen < 1e-9) { _lut[l] = 0; _lut[l + 1] = 0; _lut[l + 2] = 0; _lut[l + 3] = alpha; continue; }
                    float nx = (float)(gx / glen), ny = (float)(gy / glen);

                    // perfil Snell squircle n=1.5 (fechado, igual ao HLSL)
                    double depth = -sdo;
                    double xs = Sat(depth / bz);
                    double u = 1.0 - xs;
                    double h1 = Math.Pow(Math.Max(1.0 - u * u * u * u, 0.0), 0.25);
                    double iv = u / Math.Max(h1, 1e-4);
                    double dh = iv * iv * iv;
                    double ca = 1.0 / Math.Sqrt(1.0 + dh * dh);
                    double sa = dh * ca;
                    double sb = sa * 0.66666667;
                    double cb = Math.Sqrt(Math.Max(0.0, 1.0 - sb * sb));
                    double sD = sa * cb - ca * sb;
                    double cD = ca * cb + sa * sb;
                    double deflect = sD / Math.Max(cD, 1e-4);
                    float mag = (float)(deflect * (rf / MaxMag) * _dpi);   // px device

                    _lut[l] = nx; _lut[l + 1] = ny; _lut[l + 2] = mag; _lut[l + 3] = alpha;
                }
            }
        }

        // SDF squircle (radius) com corte octogonal 45° (chamfer) e smooth-max
        // fillet 3 px — cópia direta do LiquidGlass.hlsl.
        private static double Sdf(double px, double py, double hsx, double hsy, double rd, double ch)
        {
            double qx = Math.Abs(px) - (hsx - rd);
            double qy = Math.Abs(py) - (hsy - rd);
            double ox = Math.Max(qx, 0.0), oy = Math.Max(qy, 0.0);
            double sd = Math.Sqrt(ox * ox + oy * oy) + Math.Min(Math.Max(qx, qy), 0.0) - rd;

            if (ch <= 0.0) return sd;

            double cut = (Math.Abs(px) + Math.Abs(py) - (hsx + hsy - ch)) * 0.70710678;
            const double k = 3.0;
            double sm = Sat(0.5 + 0.5 * (cut - sd) / k);
            return sd + (cut - sd) * sm + k * sm * (1.0 - sm);
        }

        private static double Sat(double v) => v < 0.0 ? 0.0 : (v > 1.0 ? 1.0 : v);
        private static double Clamp(double v, double lo, double hi) => v < lo ? lo : (v > hi ? hi : v);

        // ------------------------------------------------------------------
        // Distort — um frame. src = Pbgra32 (W*H*4, stride = W*4), output em
        // Output (Pbgra32). Buffers internos reusados: zero alocação por frame.
        // ------------------------------------------------------------------
        public void Distort(byte[] src)
        {
            int W = _w, H = _h, area = W * H;

            // 1) un-premultiply (o fetch por canal precisa de cor straight)
            for (int i = 0; i < area * 4; i += 4)
            {
                byte a = src[i + 3];
                if (a == 255 || a == 0)
                {
                    _straight[i] = src[i]; _straight[i + 1] = src[i + 1];
                    _straight[i + 2] = src[i + 2]; _straight[i + 3] = a;
                }
                else
                {
                    _straight[i]     = (byte)(src[i]     * 255 / a);
                    _straight[i + 1] = (byte)(src[i + 1] * 255 / a);
                    _straight[i + 2] = (byte)(src[i + 2] * 255 / a);
                    _straight[i + 3] = a;
                }
            }

            float cr = (float)_chromatic;
            float kg = 1f - 0.075f * cr;
            float kb = 1f - 0.125f * cr;
            float[] lut = _lut;
            byte[] s = _straight, d = _dst;
            float xmax = W - 1.001f, ymax = H - 1.001f;

            int idx = 0, l = 0;
            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++, idx += 4, l += 4)
                {
                    float al = lut[l + 3];
                    byte a = (byte)(al * 255f + 0.5f);
                    if (a == 0) { d[idx] = 0; d[idx + 1] = 0; d[idx + 2] = 0; d[idx + 3] = 0; continue; }

                    float nx = lut[l], ny = lut[l + 1], mag = lut[l + 2];

                    // ---- canal R (mag pleno) — bilinear com clamp ----
                    float sx = x + nx * mag; if (sx < 0) sx = 0; else if (sx > xmax) sx = xmax;
                    float sy = y + ny * mag; if (sy < 0) sy = 0; else if (sy > ymax) sy = ymax;
                    int x0 = (int)sx, y0 = (int)sy, y1 = y0 + 1, x1 = x0 + 1;
                    float fx = sx - x0, fy = sy - y0;
                    int p00 = (y0 * W + x0) * 4, p10 = (y0 * W + x1) * 4;
                    int p01 = (y1 * W + x0) * 4, p11 = (y1 * W + x1) * 4;
                    float top = s[p00 + 2] + (s[p10 + 2] - s[p00 + 2]) * fx;
                    float bot = s[p01 + 2] + (s[p11 + 2] - s[p01 + 2]) * fx;
                    byte r = (byte)(top + (bot - top) * fy);

                    // ---- canal G (mag*kg) ----
                    sx = x + nx * mag * kg; if (sx < 0) sx = 0; else if (sx > xmax) sx = xmax;
                    sy = y + ny * mag * kg; if (sy < 0) sy = 0; else if (sy > ymax) sy = ymax;
                    x0 = (int)sx; y0 = (int)sy; y1 = y0 + 1; x1 = x0 + 1;
                    fx = sx - x0; fy = sy - y0;
                    p00 = (y0 * W + x0) * 4; p10 = (y0 * W + x1) * 4;
                    p01 = (y1 * W + x0) * 4; p11 = (y1 * W + x1) * 4;
                    top = s[p00 + 1] + (s[p10 + 1] - s[p00 + 1]) * fx;
                    bot = s[p01 + 1] + (s[p11 + 1] - s[p01 + 1]) * fx;
                    byte g = (byte)(top + (bot - top) * fy);

                    // ---- canal B (mag*kb) ----
                    sx = x + nx * mag * kb; if (sx < 0) sx = 0; else if (sx > xmax) sx = xmax;
                    sy = y + ny * mag * kb; if (sy < 0) sy = 0; else if (sy > ymax) sy = ymax;
                    x0 = (int)sx; y0 = (int)sy; y1 = y0 + 1; x1 = x0 + 1;
                    fx = sx - x0; fy = sy - y0;
                    p00 = (y0 * W + x0) * 4; p10 = (y0 * W + x1) * 4;
                    p01 = (y1 * W + x0) * 4; p11 = (y1 * W + x1) * 4;
                    top = s[p00] + (s[p10] - s[p00]) * fx;
                    bot = s[p01] + (s[p11] - s[p01]) * fx;
                    byte b = (byte)(top + (bot - top) * fy);

                    // ---- premultiply pelo alpha da LUT e escreve ----
                    d[idx]     = (byte)(b * a / 255);
                    d[idx + 1] = (byte)(g * a / 255);
                    d[idx + 2] = (byte)(r * a / 255);
                    d[idx + 3] = a;
                }
            }
        }

        public void Dispose() { }
    }
}
