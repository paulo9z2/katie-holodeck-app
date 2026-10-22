using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace KatieHoloDeck
{
    /// <summary>
    /// v7 iter5-v2 (refs do sir): borda TECH — corner brackets em L + tab de acento
    /// no canto sup-dir + nós de circuito. O clip octogonal do Chamfer continua
    /// ditando o formato; este Adorner DESENHA os elementos circuito por cima.
    /// Uso: <Border local:Chamfer.Size="12" local:TechFrame.Enabled="True" ...>
    /// </summary>
    public static class TechFrame
    {
        /// <summary>
        /// F1 (pintura): quanto o bracket de acento recua da borda quando o ARO DE CRISTAL
        /// está ligado. O aro neutro É a borda do vidro — nada de acento em cima dele (R14)
        /// e nada de segunda moldura grudada na hairline.
        /// </summary>
        private const double RingInset = 3.0;

        public static readonly DependencyProperty EnabledProperty =
            DependencyProperty.RegisterAttached("Enabled", typeof(bool), typeof(TechFrame),
                new FrameworkPropertyMetadata(false, OnEnabledChanged));

        public static bool GetEnabled(DependencyObject o) => (bool)o.GetValue(EnabledProperty);
        public static void SetEnabled(DependencyObject o, bool v) => o.SetValue(EnabledProperty, v);

        private static void OnEnabledChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            if (o is not FrameworkElement el) return;
            el.SizeChanged -= Recalc;
            el.SizeChanged += Recalc;
            el.Loaded -= RecalcLoaded;
            el.Loaded += RecalcLoaded;
            Apply(el);
        }

        private static void Recalc(object? s, SizeChangedEventArgs? e) => Apply(s as FrameworkElement);
        private static void RecalcLoaded(object? s, RoutedEventArgs? e) => Apply(s as FrameworkElement);

        // v9.3: API pública pro code-behind matar a "borda órfã" — adorners NÃO herdam
        // Opacity do elemento: durante fade-out o painel some e a moldura ficava nítida.
        // Clear antes do fade, Refresh ao (re)abrir.
        // v9.3-fix (Codex P3): Clear também SUPRIME (Enabled=false) até o próximo Refresh —
        // SizeChanged durante o fade chamava Apply e recriava a moldura (borda órfã no resize).
        public static void Refresh(FrameworkElement el) { el.SetValue(EnabledProperty, true); Apply(el); }
        public static void Clear(FrameworkElement el)
        {
            el.SetValue(EnabledProperty, false);
            var layer = AdornerLayer.GetAdornerLayer(el);
            if (layer == null) return;
            foreach (var a in layer.GetAdorners(el) ?? System.Array.Empty<Adorner>())
                if (a is TechAdorner) layer.Remove(a);
        }

        /// <summary>
        /// F1: re-renderiza a moldura SEM mexer no estado Enabled — o vidro liga/desliga em
        /// runtime e o bracket precisa recolocar-se na hora (recuo do aro), sem recriar a
        /// moldura e sem religar o que o chamador (blur/fade) apagou de propósito.
        /// </summary>
        public static void Invalidate(FrameworkElement el)
        {
            var layer = AdornerLayer.GetAdornerLayer(el);
            if (layer == null) return;
            foreach (var a in layer.GetAdorners(el) ?? System.Array.Empty<Adorner>())
                if (a is TechAdorner ta) { ta.InvalidateVisual(); break; }
        }

        private static void Apply(FrameworkElement? el)
        {
            if (el == null) return;
            // v9.3: Enabled=false agora LIMPA o adorner existente (antes o SetEnabled(false)
            // deixava a moldura desenhada — raiz de molduras "flutuando nítidas" sobre blur)
            if (!GetEnabled(el))
            {
                var dead = AdornerLayer.GetAdornerLayer(el);
                if (dead != null)
                    foreach (var a in dead.GetAdorners(el) ?? System.Array.Empty<Adorner>())
                        if (a is TechAdorner) dead.Remove(a);
                return;
            }
            var layer = AdornerLayer.GetAdornerLayer(el);
            if (layer == null)
            {
                // FIX v9 (18/10, causa-raiz do congelamento): o BeginInvoke(Loaded)
                // re-agendando Apply era um LOOP INFINITO com priority Loaded(8) >
                // Render(7) > Input(5) > Background(4) — starvation total: tela morta,
                // input morto, DispatcherTimer da telemetria nunca dispara, CPU 50-74%
                // (dump: UI thread grilada em ProcessQueue/TryPostMessage).
                // Agora: elemento não carregado → o evento Loaded (já assinado) cobre;
                // carregado → UMA segunda chance em Background (abaixo de Render/Input,
                // sem starvation) que desiste se o layer ainda não existir.
                if (!el.IsLoaded) return;
                el.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background,
                    (Action)(() => { if (AdornerLayer.GetAdornerLayer(el) != null) Apply(el); }));
                return;
            }
            TechAdorner? ad = null;
            foreach (var a in layer.GetAdorners(el) ?? System.Array.Empty<Adorner>())
                if (a is TechAdorner ta) { ad = ta; break; }
            if (ad == null) { ad = new TechAdorner(el); layer.Add(ad); }
            ad.InvalidateVisual();
        }

        private sealed class TechAdorner : Adorner
        {
            // F1 — zero alocação por render: pens/brushes privados reutilizados. Eles NÃO são
            // congelados de propósito: o brush do acento é compartilhado com o LivePalette
            // (congelar aqui selaria o brush vivo e quebraria a troca de acento in-place).
            private readonly Pen _bold = new() { Thickness = 3.0 };
            private readonly Pen _seg = new() { Thickness = 1.5 };
            private readonly Pen _segDim = new() { Thickness = 1.5 };
            private readonly SolidColorBrush _dim = new();
            private readonly SolidColorBrush _dot = new();
            private Brush? _accent;
            private Color _dimColor, _dotColor;

            public TechAdorner(FrameworkElement el) : base(el)
            {
                IsHitTestVisible = false;
            }

            protected override void OnRender(DrawingContext dc)
            {
                var el = (FrameworkElement)AdornedElement;
                double w = el.ActualWidth, h = el.ActualHeight;
                if (w < 60 || h < 40) return;
                const double off = 3;
                double arm = Math.Min(26, Math.Max(18, Math.Min(w, h) * 0.09)); // 8-10% (receita 1)

                var accent = el.GetValue(Border.BorderBrushProperty) as Brush;
                if (accent == null) return;

                if (!ReferenceEquals(accent, _accent))
                {
                    _accent = accent;
                    _bold.Brush = accent;
                    _seg.Brush = accent;
                    _segDim.Brush = _dim;
                }

                // cor sólida do acento (o BorderBrush dos alvos é sempre SolidColorBrush)
                var solid = (accent as SolidColorBrush)?.Color ?? Colors.Cyan;
                var dimColor = Color.FromArgb(0x59, solid.R, solid.G, solid.B); // rail do meio (35%)
                if (dimColor != _dimColor) { _dimColor = dimColor; _dim.Color = dimColor; }
                if (solid != _dotColor) { _dotColor = solid; _dot.Color = solid; }

                // ===== R11 (F1): o bracket NUNCA entra no vazio do chanfro =====
                // A silhueta octogonal CORTA o canto: o topo só existe de x = c em diante e a
                // lateral só de y = c em diante. O bracket antigo começava em x = c − off mas
                // CORRIDO PELA BORDA (y = 0) → os primeiros (c − off) px ficavam no vazio do
                // chanfro: pixels de acento FORA da silhueta (bracket órfão flutuando).
                // Agora o braço nasce onde a silhueta permite (+1 px de folga) e, com o aro de
                // cristal ligado, recua RingInset px da borda — o aro neutro É a borda do
                // vidro e não pode carregar acento (R14), nem virar uma segunda moldura.
                double c = Chamfer.GetSize(el);
                double inset = Glass.IsRingActive(el) ? RingInset : 0.0;
                double start = Math.Max(Math.Max(0, c - off), c - inset + 1.0);
                const double t3 = 3.0;
                // sup-esq
                dc.DrawRectangle(null, _bold, new Rect(start, inset, arm, t3));
                dc.DrawRectangle(null, _bold, new Rect(inset, start, t3, arm));
                // sup-dir
                dc.DrawRectangle(null, _bold, new Rect(w - start - arm, inset, arm, t3));
                dc.DrawRectangle(null, _bold, new Rect(w - inset - t3, start, t3, arm));
                // inf-esq
                dc.DrawRectangle(null, _bold, new Rect(start, h - inset - t3, arm, t3));
                dc.DrawRectangle(null, _bold, new Rect(inset, h - start - arm, t3, arm));
                // inf-dir
                dc.DrawRectangle(null, _bold, new Rect(w - start - arm, h - inset - t3, arm, t3));
                dc.DrawRectangle(null, _bold, new Rect(w - inset - t3, h - start - arm, t3, arm));

                // ===== RAILS com fade no meio (cantos 100% -> meio 25%) =====
                double rail = inset + 1.5;
                double startX = start + arm, endX = w - start - arm;
                double startY = start + arm, endY = h - start - arm;
                if (endX > startX && endY > startY)
                {
                    double mx1 = startX + (endX - startX) * 0.28;
                    double mx2 = endX - (endX - startX) * 0.28;
                    Rail(dc, new Point(startX, rail), new Point(mx1, rail), new Point(mx2, rail), new Point(endX, rail));
                    Rail(dc, new Point(startX, h - rail), new Point(mx1, h - rail), new Point(mx2, h - rail), new Point(endX, h - rail));
                    double my1 = startY + (endY - startY) * 0.28;
                    double my2 = endY - (endY - startY) * 0.28;
                    Rail(dc, new Point(rail, startY), new Point(rail, my1), new Point(rail, my2), new Point(rail, endY));
                    Rail(dc, new Point(w - rail, startY), new Point(w - rail, my1), new Point(w - rail, my2), new Point(w - rail, endY));
                }

                // ===== UM NÓ por painel (restraint): via no centro-baixo — LED de status.
                // Fica FORA da silhueta de propósito (identidade tech, não é bracket) e por
                // isso não toca a face do vidro (R13). =====
                dc.DrawEllipse(_dot, null, new Point(w * 0.5, h + off + 3), 3.2, 3.2);
            }

            private void Rail(DrawingContext d, Point a, Point m1, Point m2, Point b)
            {
                d.DrawLine(_seg, a, m1);
                d.DrawLine(_segDim, m1, m2);
                d.DrawLine(_seg, m2, b);
            }
        }
    }
}
