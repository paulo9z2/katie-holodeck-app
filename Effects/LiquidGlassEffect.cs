using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace KatieHoloDeck.Effects
{
    /// <summary>
    /// T3 (24/10) — Liquid Glass shader de verdade: refração Snell na faixa do bezel +
    /// chromatic aberration (razões Liquify) sobre backdrop explícito.
    ///
    /// Bytecode: Effects/LiquidGlass.ps (ps_3_0, ~104 instruction slots, compilado
    /// offline via D3DCompile — d3dcompiler_47.dll, fxc não existe nesta máquina).
    /// Fonte: Effects/LiquidGlass.hlsl (matemática do spike F0 reescrita analítica,
    /// MAXMAG assado 1.1031322746349106, zero loops).
    ///
    /// Registers (um DP por register, layout F0 — sem packing):
    ///   c0 = InnerSize (px da FACE, sem pad) · c1 = Radius · c2 = Chamfer
    ///   c3 = Bezel · c4 = Refraction · c5 = Chromatic (0 mono → 1 Liquify)
    ///   c6 = Pad (overscan px por lado — mata o clamp da dobra, gargalo #1 do F0)
    ///
    /// R18: NUNCA Freeze() no static ctor (TypeInitializationException envenena o
    /// AppDomain). Carga em try/catch + <see cref="Available"/>; recurso ausente ou
    /// pack URI irreal = degrau vetorial, nunca crash.
    /// </summary>
    public class LiquidGlassEffect : ShaderEffect
    {
        // =====================================================================
        // BYTECODE COMPARTILHADO — 1 PixelShader p/ todos os hosts (o caro é o
        // bytecode; cada host tem a PRÓPRIA instância de Effect com Input próprio)
        // =====================================================================
        private static readonly PixelShader? _shader;
        public static bool Available { get; }

        static LiquidGlassEffect()
        {
            try
            {
                _shader = new PixelShader
                {
                    UriSource = new Uri("pack://application:,,,/KatieHoloDeck;component/Effects/LiquidGlass.ps")
                };
                Available = true;
            }
            catch
            {
                // Recurso ausente / pack URI irreal / sem Application: fica null.
                // Glass.Resolve() lê Available e degrada pro teto vetorial.
                Available = false;
            }
        }

        public LiquidGlassEffect()
        {
            if (_shader is null) return;
            PixelShader = _shader;
            UpdateShaderValue(InputProperty);
            UpdateShaderValue(InnerSizeProperty);
            UpdateShaderValue(RadiusProperty);
            UpdateShaderValue(ChamferProperty);
            UpdateShaderValue(BezelProperty);
            UpdateShaderValue(RefractionProperty);
            UpdateShaderValue(ChromaticProperty);
            UpdateShaderValue(PadProperty);
        }

        // =====================================================================
        // SAMPLER — backdrop EXPLÍCITO (padrão F0: ImplicitInput em host com
        // conteúdo vivo re-rasteriza subárvore por frame — R9. Nunca usar.)
        // =====================================================================
        public Brush Input
        {
            get => (Brush)GetValue(InputProperty);
            set => SetValue(InputProperty, value);
        }
        public static readonly DependencyProperty InputProperty =
            ShaderEffect.RegisterPixelShaderSamplerProperty("Input", typeof(LiquidGlassEffect), 0);

        // =====================================================================
        // CONSTANTS — índice de callback = índice do register (c0..c6)
        // =====================================================================

        /// <summary>Tamanho da FACE em px (sem pad). c0.</summary>
        public Size InnerSize
        {
            get => (Size)GetValue(InnerSizeProperty);
            set => SetValue(InnerSizeProperty, value);
        }
        public static readonly DependencyProperty InnerSizeProperty =
            DependencyProperty.Register(nameof(InnerSize), typeof(Size), typeof(LiquidGlassEffect),
                new UIPropertyMetadata(new Size(1.0, 1.0), PixelShaderConstantCallback(0)));

        /// <summary>Raio dos cantos (alinhado ao CornerRadius do host). c1.</summary>
        public double Radius
        {
            get => (double)GetValue(RadiusProperty);
            set => SetValue(RadiusProperty, value);
        }
        public static readonly DependencyProperty RadiusProperty =
            DependencyProperty.Register(nameof(Radius), typeof(double), typeof(LiquidGlassEffect),
                new UIPropertyMetadata(14.0, PixelShaderConstantCallback(1)));

        /// <summary>Chanfro 45° (Chamfer.Size do host; 0 = sem chanfro). c2.</summary>
        public double Chamfer
        {
            get => (double)GetValue(ChamferProperty);
            set => SetValue(ChamferProperty, value);
        }
        public static readonly DependencyProperty ChamferProperty =
            DependencyProperty.Register(nameof(Chamfer), typeof(double), typeof(LiquidGlassEffect),
                new UIPropertyMetadata(12.0, PixelShaderConstantCallback(2)));

        /// <summary>Faixa refrativa em px (rampa da borda; interior neutro). c3.</summary>
        public double Bezel
        {
            get => (double)GetValue(BezelProperty);
            set => SetValue(BezelProperty, value);
        }
        public static readonly DependencyProperty BezelProperty =
            DependencyProperty.Register(nameof(Bezel), typeof(double), typeof(LiquidGlassEffect),
                new UIPropertyMetadata(10.0, PixelShaderConstantCallback(3)));

        /// <summary>Escala de refração em px (pico do deslocamento na borda). c4.</summary>
        public double Refraction
        {
            get => (double)GetValue(RefractionProperty);
            set => SetValue(RefractionProperty, value);
        }
        public static readonly DependencyProperty RefractionProperty =
            DependencyProperty.Register(nameof(Refraction), typeof(double), typeof(LiquidGlassEffect),
                new UIPropertyMetadata(8.0, PixelShaderConstantCallback(4)));

        /// <summary>
        /// 0 = mono (1 fetch) · 1 = chromatic pleno (3 fetches, razões Liquify
        /// R:G:B = 1:0.925:0.875). Branch uniforme no ps_3_0.
        /// </summary>
        public double Chromatic
        {
            get => (double)GetValue(ChromaticProperty);
            set => SetValue(ChromaticProperty, value);
        }
        public static readonly DependencyProperty ChromaticProperty =
            DependencyProperty.Register(nameof(Chromatic), typeof(double), typeof(LiquidGlassEffect),
                new UIPropertyMetadata(0.6, PixelShaderConstantCallback(5)));

        /// <summary>Overscan px por lado (host maior que a face). c6.</summary>
        public Size Pad
        {
            get => (Size)GetValue(PadProperty);
            set => SetValue(PadProperty, value);
        }
        public static readonly DependencyProperty PadProperty =
            DependencyProperty.Register(nameof(Pad), typeof(Size), typeof(LiquidGlassEffect),
                new UIPropertyMetadata(new Size(0.0, 0.0), PixelShaderConstantCallback(6)));
    }
}
