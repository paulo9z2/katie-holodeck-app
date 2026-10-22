using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;

namespace KatieHoloDeck
{
    /// <summary>Modo efetivo do material. F1 tem teto VETORIAL — Shader é F2.</summary>
    public enum GlassMode { Off = 0, Vector = 1, Shader = 2 }

    /// <summary>
    /// F1 — ARO DE CRISTAL T2 (vidro vetorial). Substitui, nas 3 superfícies-alvo, o
    /// retângulo de fundo por um <see cref="Path"/> octogonal de verdade (R12) e desenha
    /// o aro num Adorner, em 3 camadas que NÃO se atropelam:
    ///   · aro  = edge 1 px branca neutra 30%  (token ThemeGlassEdge, alpha via WithA —
    ///     F2.6: era 8-10% e sumia no AA; agora lê como polimento sem virar moldura)
    ///     — só nos lados RETOS: o canto é da faceta, não do aro (nada de contorno duplo)
    ///   · faceta = banda CHEIA de 1,5 px centrada 0,75 px dentro da diagonal do chanfro
    ///     (token ThemeGlassFacet) — a borda externa da banda coincide com a silhueta
    ///     (ZERO pixel fora) e ela não se sobrepõe ao aro (nada de "blob" no canto)
    ///   · specular = 0x55, SÓ na faceta superior-esquerda (luz da casa = −24°), branco
    ///     NEUTRO de token próprio (GlassEdgeHighlightBrush — F2.6: era o AccentBrushFaint
    ///     0x33, que pintava o specular de ciano; agora pode subir sem acento)
    /// Sem sheen, sem véu iOS, sem glow: nada de acento na face nem no aro (R13/R14).
    ///
    /// Regras da casa respeitadas por construção:
    ///   · leitura AO VIVO no OnRender (nenhum cache de paleta → R7 morto por construção);
    ///   · zero alocação por frame: geometria/pens são reconstruídos só quando muda o
    ///     tamanho/chanfro/raio, e os brushes vivos são re-resolvidos por referência;
    ///   · nenhum Freezable de ResourceDictionary é mutado aqui: só se LÊ o brush vivo e
    ///     se aponta o Pen para ele (o SetAccent/ApplyTheme continua dono da mutação);
    ///   · nada de stackalloc (Color é managed — crash v9.8.2, commit 0307cc2).
    /// </summary>
    public static class Glass
    {
        // =====================================================================
        // MODO EFETIVO (Off / Vector / Shader)
        // =====================================================================

        private static GlassMode _mode = GlassMode.Off;

        /// <summary>Modo em vigor, lido ao vivo por Chamfer/Glass (sem evento, sem cache).</summary>
        public static GlassMode Mode => _mode;

        /// <summary>
        /// Resolve o modo efetivo a partir do toggle + perfil de efeitos.
        /// `OptNone` ("Sem animações") FORÇA Vector: o material nunca entra no caminho de
        /// shader animado — a regra nasce aqui para o F2 não poder violá-la.
        /// </summary>
        public static GlassMode Resolve(bool enabled, string? effectsProfile)
        {
            if (!enabled) return GlassMode.Off;
            if (string.Equals(effectsProfile, "none", StringComparison.OrdinalIgnoreCase))
                return GlassMode.Vector;   // perfil "Sem animações" fica travado no vetorial
            return GlassMode.Vector;       // F1: teto vetorial. F2 troca SÓ esta linha por Shader.
        }

        public static string Describe(GlassMode mode) => mode switch
        {
            GlassMode.Vector => "vector",
            GlassMode.Shader => "shader",
            _ => "off"
        };

        /// <summary>Aplica toggle+perfil. Se o modo mudou, reaponta todas as superfícies.</summary>
        public static void Apply(bool enabled, string? effectsProfile)
        {
            var next = Resolve(enabled, effectsProfile);
            if (next == _mode) return;
            _mode = next;
            Refresh();
        }

        // =====================================================================
        // REGISTRO (troca de modo em runtime sem recriar árvore visual)
        // =====================================================================

        private static readonly List<WeakReference<FrameworkElement>> _targets = new();

        private static void Track(FrameworkElement el)
        {
            for (int i = 0; i < _targets.Count; i++)
                if (_targets[i].TryGetTarget(out var t) && ReferenceEquals(t, el)) return;
            _targets.Add(new WeakReference<FrameworkElement>(el));
        }

        /// <summary>Reaplica o vidro em todas as superfícies registradas (toggle/perfil).</summary>
        public static void Refresh()
        {
            // índice reverso: permite remover referências mortas sem alocar lista nova
            for (int i = _targets.Count - 1; i >= 0; i--)
            {
                if (!_targets[i].TryGetTarget(out var el)) { _targets.RemoveAt(i); continue; }
                switch (el)
                {
                    case Path p: ApplyFace(p); break;
                    case Border b: ApplyRing(b); Chamfer.Refresh(b); TechFrame.Invalidate(b); break;
                    default: ApplyDim(el); break;
                }
            }
        }

        // =====================================================================
        // ARO — Adorner (edge + faceta + specular)
        // =====================================================================

        public static readonly DependencyProperty RingProperty =
            DependencyProperty.RegisterAttached("Ring", typeof(bool), typeof(Glass),
                new FrameworkPropertyMetadata(false, OnRingChanged));

        public static bool GetRing(DependencyObject o) => (bool)o.GetValue(RingProperty);
        public static void SetRing(DependencyObject o, bool v) => o.SetValue(RingProperty, v);

        /// <summary>
        /// true quando o aro de cristal está desenhando. O ChamferAdorner consulta isto
        /// para NÃO pintar o traço de acento por baixo do vidro (R13: aro neutro).
        /// </summary>
        public static bool IsRingActive(DependencyObject o) => _mode != GlassMode.Off && GetRing(o);

        private static void OnRingChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            if (o is not FrameworkElement el) return;
            el.SizeChanged -= RingSizeChanged;
            el.SizeChanged += RingSizeChanged;
            el.Loaded -= RingLoaded;
            el.Loaded += RingLoaded;
            Track(el);
            ApplyRing(el);
        }

        private static void RingSizeChanged(object sender, SizeChangedEventArgs e) => ApplyRing(sender as FrameworkElement);
        private static void RingLoaded(object sender, RoutedEventArgs e) => ApplyRing(sender as FrameworkElement);

        private static void ApplyRing(FrameworkElement? el)
        {
            if (el == null || el.ActualWidth <= 0 || el.ActualHeight <= 0) return;
            var layer = AdornerLayer.GetAdornerLayer(el);
            if (layer == null) return;   // mesmo padrão do Chamfer: Loaded/SizeChanged tentam de novo
            RingAdorner? ad = null;
            var existing = layer.GetAdorners(el);
            if (existing != null)
                foreach (var a in existing)
                    if (a is RingAdorner ra) { ad = ra; break; }
            if (ad == null) { ad = new RingAdorner(el); layer.Add(ad); }
            ad.InvalidateVisual();
        }

        private sealed class RingAdorner : Adorner
        {
            // Pens PRIVADOS (não vão pra dicionário nenhum): podem ser reusados e reapontados
            // a cada render sem alocar. Não são congelados de propósito — o brush vivo do
            // acento é compartilhado e o LivePalette continua dono da mutação.
            // ===== MEDIDAS DO MATERIAL (T2) — o sir fixou: aro 1 px · faceta 1,5 px =====
            private const double EdgeInset = 0.5;    // aro de 1 px 100% DENTRO da silhueta
            private const double FacetWidth = 1.5;    // "faceta 45° dura 1-3px"
            private const double SpecularSpan = 0.62; // fatia da faceta UL que pega a luz da casa (−24°)
            private const double Sqrt2 = 0.7071067811865476;

            private readonly Pen _edge = new() { Thickness = 1.0, StartLineCap = PenLineCap.Flat, EndLineCap = PenLineCap.Flat };
            private readonly Pen _facet = new() { Thickness = FacetWidth, StartLineCap = PenLineCap.Flat, EndLineCap = PenLineCap.Flat };
            private readonly Pen _spec = new() { Thickness = FacetWidth, StartLineCap = PenLineCap.Flat, EndLineCap = PenLineCap.Flat };

            private Geometry? _edgeGeo, _facetGeo, _specGeo;
            private double _w = double.NaN, _h = double.NaN, _c = double.NaN, _r = double.NaN;

            public RingAdorner(FrameworkElement el) : base(el) { IsHitTestVisible = false; }

            protected override void OnRender(DrawingContext dc)
            {
                // Leitura AO VIVO do modo e da paleta — nenhum cache de cor.
                if (_mode == GlassMode.Off) return;

                var el = (FrameworkElement)AdornedElement;
                double w = el.ActualWidth, h = el.ActualHeight;
                if (w < 10 || h < 10) return;

                double c = Chamfer.GetSize(el);
                double r = el is Border b ? b.CornerRadius.TopLeft : 0;
                if (c > 0 && (w < 2 * c + 1 || h < 2 * c + 1)) c = 0;   // degenerado → cai no arredondado
                if (c <= 0 && r <= 0) return;

                // Geometria só é reconstruída quando o tamanho/chanfro/raio muda (zero alloc/frame).
                if (w != _w || h != _h || c != _c || r != _r) Build(w, h, c, r);

                // Brushes vivos re-resolvidos por REFERÊNCIA (mutação in-place ou troca de
                // objeto no dicionário quando a plataforma congela) — sem alocar.
                var edge = LivePalette.Instance.Get("GlassEdgeBrush");
                if (!ReferenceEquals(edge, _edge.Brush)) _edge.Brush = edge;
                var facet = LivePalette.Instance.Get("GlassFacetBrush");
                if (!ReferenceEquals(facet, _facet.Brush)) _facet.Brush = facet;
                var spec = LivePalette.Instance.Get("GlassEdgeHighlightBrush"); // 0x55 neutro, token próprio
                if (!ReferenceEquals(spec, _spec.Brush)) _spec.Brush = spec;

                if (_edge.Brush == null || _facet.Brush == null || _spec.Brush == null) return;

                // Três traços e nada mais: sem glow (V3), sem véu (V4), sem sheen.
                if (_edgeGeo != null) dc.DrawGeometry(null, _edge, _edgeGeo);
                if (_facetGeo != null) dc.DrawGeometry(null, _facet, _facetGeo);
                if (_specGeo != null) dc.DrawGeometry(null, _spec, _specGeo);
            }

            private void Build(double w, double h, double c, double r)
            {
                _w = w; _h = h; _c = c; _r = r;
                if (c > 0) BuildOctagon(w, h, c);
                else BuildRounded(w, h, r);
            }

            /// <summary>
            /// Silhueta OCTOGONAL (chanfro da casa) — o aro em 3 camadas que não se atropelam:
            ///   · aro (edge) = 4 lados RETOS de 1 px, inset 0,5 → 100% dentro da silhueta;
            ///   · faceta = banda CHEIA de 1,5 px centrada 0,75 px dentro de cada diagonal do
            ///     chanfro (a borda externa da banda coincide com a diagonal → ZERO pixel fora
            ///     da silhueta; e a banda NÃO se sobrepõe ao aro → nada de "blob" no canto);
            ///   · specular = SÓ a faceta superior-esquerda (a luz da casa é −24°: o reator
            ///     ilumina de cima/esquerda) e só na fatia que pega a luz.
            /// </summary>
            private void BuildOctagon(double w, double h, double c)
            {
                double cc = Math.Min(c, Math.Min(w, h) * 0.5 - 0.5);
                if (cc < 1) { BuildRounded(w, h, 0); return; }

                // ---- aro: 4 lados retos, 1 px, dentro da silhueta ----
                var edge = new PathGeometry();
                AddLine(edge, new Point(cc, EdgeInset), new Point(w - cc, EdgeInset));            // topo
                AddLine(edge, new Point(w - EdgeInset, cc), new Point(w - EdgeInset, h - cc));    // direita
                AddLine(edge, new Point(w - cc, h - EdgeInset), new Point(cc, h - EdgeInset));    // baixo
                AddLine(edge, new Point(EdgeInset, h - cc), new Point(EdgeInset, cc));            // esquerda
                _edgeGeo = edge;

                // ---- faceta: banda de 1,5 px centrada 0,75 px dentro de cada diagonal ----
                double e = FacetWidth * Sqrt2;    // 1,06066 — deslocamento por eixo do centro da banda
                double ox = e - FacetWidth * 0.5; // 0,31066 — recuo dos extremos da banda
                double k = FacetWidth * 0.5;      // 0,75 — meia banda (profundidade da linha de centro)
                var tl1 = new Point(cc + ox, k);         // diagonal sup-esq: extremo de cima
                var tl2 = new Point(k, cc + ox);         // diagonal sup-esq: extremo lateral
                var tr1 = new Point(w - cc - ox, k);      // sup-dir: extremo de cima
                var tr2 = new Point(w - k, cc + ox);
                var br1 = new Point(w - cc - ox, h - k);  // inf-dir: extremo de baixo
                var br2 = new Point(w - k, h - cc - ox);
                var bl1 = new Point(k, h - cc - ox);      // inf-esq: extremo lateral
                var bl2 = new Point(cc + ox, h - k);

                var facet = new PathGeometry();
                AddLine(facet, tl1, tl2);
                AddLine(facet, tr1, tr2);
                AddLine(facet, br1, br2);
                AddLine(facet, bl1, bl2);
                _facetGeo = facet;

                // ---- specular: fatia de cima da faceta sup-esquerda (luz da casa, −24°) ----
                var spec = new PathGeometry();
                AddLine(spec, tl1, new Point(tl1.X + (tl2.X - tl1.X) * SpecularSpan,
                                             tl1.Y + (tl2.Y - tl1.Y) * SpecularSpan));
                _specGeo = spec;
            }

            /// <summary>
            /// Controle pequeno (T3 proíbe chanfro novo abaixo de 34 px): silhueta arredondada
            /// nativa, MESMO idioma do octógono — aro de 1 px nos lados retos, banda de faceta
            /// de 1,5 px dentro de cada arco de canto, specular só no arco superior-esquerdo.
            /// </summary>
            private void BuildRounded(double w, double h, double r)
            {
                _edgeGeo = null; _facetGeo = null; _specGeo = null;
                double rr = Math.Max(0, Math.Min(r, Math.Min(w, h) * 0.5));
                if (rr <= 0) return;

                var edge = new PathGeometry();
                AddLine(edge, new Point(rr, EdgeInset), new Point(w - rr, EdgeInset));
                AddLine(edge, new Point(w - EdgeInset, rr), new Point(w - EdgeInset, h - rr));
                AddLine(edge, new Point(w - rr, h - EdgeInset), new Point(rr, h - EdgeInset));
                AddLine(edge, new Point(EdgeInset, h - rr), new Point(EdgeInset, rr));
                _edgeGeo = edge;

                double R = rr - FacetWidth * 0.5;   // raio da linha de centro da banda
                if (R <= 0.5) return;

                // F1-FIX (P1-2): a linha de centro é CONCÊNTRICA ao canto arredondado
                // (centros em (rr,rr), (w-rr,rr), (w-rr,h-rr), (rr,h-rr)) e os extremos
                // ficam a FacetWidth/2 (0,75) da borda — não mais em EdgeInset (0,5).
                // Assim R + FacetWidth/2 = rr: a borda externa do traço coincide com a
                // silhueta e não vaza nenhum pixel (antes vazava até 0,25 no canto).
                const double K = FacetWidth * 0.5;  // 0,75 — inset dos extremos da linha de centro
                var facet = new PathGeometry();
                AddArc(facet, R, new Point(rr, K), new Point(K, rr), SweepDirection.Counterclockwise);                       // sup-esq
                AddArc(facet, R, new Point(w - rr, K), new Point(w - K, rr), SweepDirection.Clockwise);                      // sup-dir
                AddArc(facet, R, new Point(w - K, h - rr), new Point(w - rr, h - K), SweepDirection.Clockwise);              // inf-dir
                AddArc(facet, R, new Point(rr, h - K), new Point(K, h - rr), SweepDirection.Clockwise);                      // inf-esq
                _facetGeo = facet;

                // ---- specular: a fatia de cima do arco sup-esquerdo (luz −24°) ----
                // Mesmo centro/raio do arco da faceta → flush e 100% dentro da silhueta.
                double ang = (270.0 - SpecularSpan * 90.0) * Math.PI / 180.0;   // y para baixo
                var end = new Point(rr + R * Math.Cos(ang), rr + R * Math.Sin(ang));
                var spec = new PathGeometry();
                AddArc(spec, R, new Point(rr, K), end, SweepDirection.Counterclockwise);
                _specGeo = spec;
            }

            private static void AddLine(PathGeometry g, Point a, Point b)
            {
                var f = new PathFigure { StartPoint = a, IsClosed = false, IsFilled = false };
                f.Segments.Add(new LineSegment(b, true));
                g.Figures.Add(f);
            }

            private static void AddArc(PathGeometry g, double r, Point from, Point to, SweepDirection sweep)
            {
                var f = new PathFigure { StartPoint = from, IsClosed = false, IsFilled = false };
                f.Segments.Add(new ArcSegment(to, new Size(r, r), 0, false, sweep, true));
                g.Figures.Add(f);
            }
        }

        // =====================================================================
        // FACE — fill octogonal real (Path), no lugar do retângulo do Border
        // =====================================================================

        public static readonly DependencyProperty FaceProperty =
            DependencyProperty.RegisterAttached("Face", typeof(string), typeof(Glass),
                new FrameworkPropertyMetadata("", OnFaceChanged));

        /// <summary>Chave de recurso do fill (ex.: "ThemeMediaGlassBrush"). Sempre aplicada.</summary>
        public static string GetFace(DependencyObject o) => (string?)o.GetValue(FaceProperty) ?? "";
        public static void SetFace(DependencyObject o, string v) => o.SetValue(FaceProperty, v);

        public static readonly DependencyProperty FaceFrostedProperty =
            DependencyProperty.RegisterAttached("FaceFrosted", typeof(string), typeof(Glass),
                new FrameworkPropertyMetadata("", OnFaceChanged));

        /// <summary>Chave alternativa quando o vidro está LIGADO (face neutra; R13).</summary>
        public static string GetFaceFrosted(DependencyObject o) => (string?)o.GetValue(FaceFrostedProperty) ?? "";
        public static void SetFaceFrosted(DependencyObject o, string v) => o.SetValue(FaceFrostedProperty, v);

        public static readonly DependencyProperty FallbackStrokeProperty =
            DependencyProperty.RegisterAttached("FallbackStroke", typeof(string), typeof(Glass),
                new FrameworkPropertyMetadata("", OnFaceChanged));

        /// <summary>
        /// Chave de traço usada SÓ com o vidro desligado — é o contorno atual de controles
        /// pequenos (UndockBtn, 30 px, sem Chamfer) que não têm aro para substituí-lo.
        /// </summary>
        public static string GetFallbackStroke(DependencyObject o) => (string?)o.GetValue(FallbackStrokeProperty) ?? "";
        public static void SetFallbackStroke(DependencyObject o, string v) => o.SetValue(FallbackStrokeProperty, v);

        private sealed class FaceState
        {
            public double W, H, C, R, Inset;
            public string FillKey = "", StrokeKey = "";
            public bool Has;
        }

        private static readonly ConditionalWeakTable<Path, FaceState> _faces = new();

        private static void OnFaceChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            if (o is not Path p) return;
            p.Loaded -= FaceLoaded;
            p.Loaded += FaceLoaded;
            p.SizeChanged -= FaceSizeChanged;
            p.SizeChanged += FaceSizeChanged;
            Track(p);
            ApplyFace(p);
        }

        private static void FaceLoaded(object sender, RoutedEventArgs e) => ApplyFace(sender as Path);
        private static void FaceSizeChanged(object sender, SizeChangedEventArgs e) => ApplyFace(sender as Path);

        private static void ApplyFace(Path? p)
        {
            if (p == null) return;
            double w = p.ActualWidth, h = p.ActualHeight;
            if (w < 4 || h < 4) return;

            bool glass = _mode != GlassMode.Off;
            string fillKey = glass ? GetFaceFrosted(p) : "";
            if (fillKey.Length == 0) fillKey = GetFace(p);
            if (fillKey.Length == 0) return;
            string strokeKey = glass ? "" : GetFallbackStroke(p);

            double c = 0, r = 0;
            if (FindHost(p) is Border host) { c = Chamfer.GetSize(host); r = host.CornerRadius.TopLeft; }
            double inset = strokeKey.Length > 0 ? 0.5 : 0;

            var st = _faces.GetOrCreateValue(p);
            if (!st.Has || st.W != w || st.H != h || st.C != c || st.R != r || st.Inset != inset)
            {
                p.Data = BuildFace(w, h, c, r, inset);
                st.W = w; st.H = h; st.C = c; st.R = r; st.Inset = inset; st.Has = true;
            }
            if (st.FillKey != fillKey) { p.SetResourceReference(Shape.FillProperty, fillKey); st.FillKey = fillKey; }
            if (st.StrokeKey != strokeKey)
            {
                if (strokeKey.Length > 0) { p.StrokeThickness = 1; p.SetResourceReference(Shape.StrokeProperty, strokeKey); }
                else p.ClearValue(Shape.StrokeProperty);
                st.StrokeKey = strokeKey;
            }
        }

        /// <summary>Octógono do Chamfer quando a superfície tem chanfro; senão o arredondado do Border.</summary>
        private static Geometry BuildFace(double w, double h, double c, double r, double inset)
        {
            if (c > 0 && w - 2 * inset > 2 * c && h - 2 * inset > 2 * c)
            {
                var g = Chamfer.Octagon(w - 2 * inset, h - 2 * inset, c);
                if (inset != 0) g.Transform = new TranslateTransform(inset, inset);
                return g;
            }

            // Controle pequeno (T3/chanfro proibido): mantém a silhueta arredondada nativa.
            double x0 = inset, y0 = inset, ww = w - 2 * inset, hh = h - 2 * inset;
            double rr = Math.Max(0, Math.Min(r, Math.Min(ww, hh) * 0.5));
            var f = new PathFigure { StartPoint = new Point(x0 + rr, y0), IsClosed = true, IsFilled = true };
            f.Segments.Add(new LineSegment(new Point(x0 + ww - rr, y0), true));
            f.Segments.Add(new ArcSegment(new Point(x0 + ww, y0 + rr), new Size(rr, rr), 0, false, SweepDirection.Clockwise, true));
            f.Segments.Add(new LineSegment(new Point(x0 + ww, y0 + hh - rr), true));
            f.Segments.Add(new ArcSegment(new Point(x0 + ww - rr, y0 + hh), new Size(rr, rr), 0, false, SweepDirection.Clockwise, true));
            f.Segments.Add(new LineSegment(new Point(x0 + rr, y0 + hh), true));
            f.Segments.Add(new ArcSegment(new Point(x0, y0 + hh - rr), new Size(rr, rr), 0, false, SweepDirection.Clockwise, true));
            f.Segments.Add(new LineSegment(new Point(x0, y0 + rr), true));
            f.Segments.Add(new ArcSegment(new Point(x0 + rr, y0), new Size(rr, rr), 0, false, SweepDirection.Clockwise, true));
            var gg = new PathGeometry();
            gg.Figures.Add(f);
            return gg;
        }

        private static Border? FindHost(DependencyObject? node)
        {
            while (node != null && node is not Border)
                node = VisualTreeHelper.GetParent(node);
            return node as Border;
        }

        // =====================================================================
        // VÉU DE ACENTO NA FACE — desligado quando o vidro está ativo (R13)
        // =====================================================================

        public static readonly DependencyProperty DimWhenGlassProperty =
            DependencyProperty.RegisterAttached("DimWhenGlass", typeof(bool), typeof(Glass),
                new FrameworkPropertyMetadata(false, OnDimChanged));

        /// <summary>
        /// Marca a camada de tint de acento (SurfaceTint) que existe na face do cartão de
        /// Configurações: com o vidro ligado ela sai, e a face fica só com o token neutro.
        /// </summary>
        public static bool GetDimWhenGlass(DependencyObject o) => (bool)o.GetValue(DimWhenGlassProperty);
        public static void SetDimWhenGlass(DependencyObject o, bool v) => o.SetValue(DimWhenGlassProperty, v);

        private static void OnDimChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            if (o is not FrameworkElement el) return;
            Track(el);
            ApplyDim(el);
        }

        private static void ApplyDim(FrameworkElement el)
        {
            if (!GetDimWhenGlass(el)) return;
            var want = _mode != GlassMode.Off ? Visibility.Collapsed : Visibility.Visible;
            if (el.Visibility != want) el.Visibility = want;
        }
    }
}
