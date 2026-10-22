using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace KatieHoloDeck
{
    /// <summary>
    /// v7 iter5 (REFERENCIA_UI #1): cantos CHANFRADOS (cut-corner FUI).
    /// Aplica Clip octogonal ao elemento + traço da moldura nos diagonais via Adorner
    /// (o Border normal não desenha stroke em canto cortado — o Adorner completa).
    /// Uso: <Border local:Chamfer.Size="12" ...>  (0 = desligado)
    /// </summary>
    public static class Chamfer
    {
        public static readonly DependencyProperty SizeProperty =
            DependencyProperty.RegisterAttached("Size", typeof(double), typeof(Chamfer),
                new FrameworkPropertyMetadata(0.0, OnSizeChanged));

        public static double GetSize(DependencyObject o) => (double)o.GetValue(SizeProperty);
        public static void SetSize(DependencyObject o, double v) => o.SetValue(SizeProperty, v);

        private static void OnSizeChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
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

        private static void Apply(FrameworkElement? el)
        {
            if (el == null) return;
            double c = GetSize(el);
            double w = el.ActualWidth, h = el.ActualHeight;
            // v7 FIX: NÃO seta el.Clip — o clip do elemento cortava o TechAdorner inteiro
            // (brackets/traces sumiam no topo). O chanfro visual agora é desenhado pelo
            // ChamferAdorner (moldura octogonal).
            // F1: o fill das superfícies de vidro migrou para Path com geometria octogonal
            // (Glass.Face → Chamfer.Octagon), então não há mais fundo escapando pelo canto.
            if (c <= 0 || w < 2 * c || h < 2 * c || w <= 0 || h <= 0) return;

            var layer = AdornerLayer.GetAdornerLayer(el);
            if (layer == null) return; // tenta de novo no próximo SizeChanged/Loaded
            ChamferAdorner? adorner = null;
            foreach (var a in layer.GetAdorners(el) ?? System.Array.Empty<Adorner>())
                if (a is ChamferAdorner ca) { adorner = ca; break; }
            if (adorner == null) { adorner = new ChamferAdorner(el, () => GetSize(el)); layer.Add(adorner); }
            adorner.InvalidateVisual();
        }

        internal static PathGeometry Octagon(double w, double h, double c)
        {
            var f = new PathFigure { StartPoint = new Point(c, 0), IsClosed = true };
            f.Segments.Add(new LineSegment(new Point(w - c, 0), true));
            f.Segments.Add(new LineSegment(new Point(w, c), true));
            f.Segments.Add(new LineSegment(new Point(w, h - c), true));
            f.Segments.Add(new LineSegment(new Point(w - c, h), true));
            f.Segments.Add(new LineSegment(new Point(c, h), true));
            f.Segments.Add(new LineSegment(new Point(0, h - c), true));
            f.Segments.Add(new LineSegment(new Point(0, c), true));
            var g = new PathGeometry();
            g.Figures.Add(f);
            return g;
        }

        /// <summary>F1: re-aplica o adorner (usado quando o modo do vidro muda em runtime).</summary>
        public static void Refresh(FrameworkElement el) => Apply(el);

        private sealed class ChamferAdorner : Adorner
        {
            private readonly Func<double> _size;
            // F1 — zero alocação por render: o Pen do traço é reusado e só re-aponta o brush
            // quando a REFERÊNCIA muda (o brush do acento é vivo, mutado in-place pelo LivePalette).
            private readonly Pen _stroke = new() { Thickness = 1.2 };
            private Brush? _strokeBrush;
            private double _strokeThickness = 1.2;
            // F1-FIX (P1-1): a geometria vive no adorner e só é reconstruída quando
            // largura/altura/chanfro mudam — mesmo padrão de cache do Glass.cs Build.
            private Geometry? _geometry;
            private double _geoW = double.NaN, _geoH = double.NaN, _geoC = double.NaN;

            public ChamferAdorner(FrameworkElement el, Func<double> size) : base(el)
            {
                _size = size;
                IsHitTestVisible = false;
            }

            protected override void OnRender(DrawingContext dc)
            {
                var el = (FrameworkElement)AdornedElement;
                // F1: com o aro de cristal ligado, o traço de acento sai de cena — o aro
                // neutro (Glass.RingAdorner) é o novo contorno. Leitura ao vivo, sem cache.
                if (Glass.IsRingActive(el)) return;
                double c = _size();
                if (c <= 0 || el.ActualWidth < 2 * c || el.ActualHeight < 2 * c) return;
                var brush = el.GetValue(Border.BorderBrushProperty) as Brush;
                if (brush == null) return;
                double thick = el is Border b && b.BorderThickness.Left > 0 ? b.BorderThickness.Left : 1.2;
                thick = Math.Max(1.0, thick);
                if (!ReferenceEquals(brush, _strokeBrush)) { _strokeBrush = brush; _stroke.Brush = brush; }
                if (thick != _strokeThickness) { _strokeThickness = thick; _stroke.Thickness = thick; }

                // Zero alocação por frame: só reconstrói quando o tamanho ou o chanfro muda.
                double w = el.ActualWidth, h = el.ActualHeight;
                if (_geometry == null || w != _geoW || h != _geoH || c != _geoC)
                {
                    _geometry = Octagon(w, h, c);
                    _geoW = w; _geoH = h; _geoC = c;
                }
                dc.DrawGeometry(null, _stroke, _geometry);
            }
        }
    }
}
