using System;
using System.Windows;
using System.Windows.Media;

namespace KatieHoloDeck
{
    /// <summary>
    /// v9.8 (review Codex P1 churn): conjunto de cores de UM tema global. É um objeto
    /// reutilizável — a transição mantém duas pontas (from/to) + um scratch e só reescreve
    /// Color structs; nenhum Freezable é criado por frame.
    /// </summary>
    internal sealed class ThemeTokens
    {
        public Color Secondary;
        public Color TextStrong, Text, Label, TextDim;
        public Color GridLine, ScanLine;
        public Color Shell0, Shell1, Shell2;
        public Color VignetteEdge;
        public Color MediaGlass, FieldGlass, BarTrack, CoverPlaceholder;
        public Color ChipGlass, ChatExpanded, PanelSolid;
        public Color Island0, Island1;
        public Color PanelStrong0, PanelStrong1;
        public Color Scrim;
        public Color Fullscreen, FullscreenOpaque;
        // F1 — aro de cristal (T2). Opacos: Mix() descarta alpha em silêncio; o alpha
        // (edge 8-10%, faceta ~12%) entra via WithA() na montagem do brush, no ApplyTheme.
        public Color ThemeGlassEdge, ThemeGlassFacet;

        public void CopyFrom(ThemeTokens o)
        {
            Secondary = o.Secondary;
            TextStrong = o.TextStrong; Text = o.Text; Label = o.Label; TextDim = o.TextDim;
            GridLine = o.GridLine; ScanLine = o.ScanLine;
            Shell0 = o.Shell0; Shell1 = o.Shell1; Shell2 = o.Shell2;
            VignetteEdge = o.VignetteEdge;
            MediaGlass = o.MediaGlass; FieldGlass = o.FieldGlass; BarTrack = o.BarTrack;
            CoverPlaceholder = o.CoverPlaceholder; ChipGlass = o.ChipGlass;
            ChatExpanded = o.ChatExpanded; PanelSolid = o.PanelSolid;
            Island0 = o.Island0; Island1 = o.Island1;
            PanelStrong0 = o.PanelStrong0; PanelStrong1 = o.PanelStrong1;
            Scrim = o.Scrim;
            Fullscreen = o.Fullscreen; FullscreenOpaque = o.FullscreenOpaque;
            ThemeGlassEdge = o.ThemeGlassEdge; ThemeGlassFacet = o.ThemeGlassFacet;
        }

        /// <summary>Interpola a→b em k (0..1) reescrevendo ESTE objeto — zero new por tick.</summary>
        public void Lerp(ThemeTokens a, ThemeTokens b, double k)
        {
            Secondary = Mix(a.Secondary, b.Secondary, k);
            TextStrong = Mix(a.TextStrong, b.TextStrong, k);
            Text = Mix(a.Text, b.Text, k);
            Label = Mix(a.Label, b.Label, k);
            TextDim = Mix(a.TextDim, b.TextDim, k);
            GridLine = Mix(a.GridLine, b.GridLine, k);
            ScanLine = Mix(a.ScanLine, b.ScanLine, k);
            Shell0 = Mix(a.Shell0, b.Shell0, k);
            Shell1 = Mix(a.Shell1, b.Shell1, k);
            Shell2 = Mix(a.Shell2, b.Shell2, k);
            VignetteEdge = Mix(a.VignetteEdge, b.VignetteEdge, k);
            MediaGlass = Mix(a.MediaGlass, b.MediaGlass, k);
            FieldGlass = Mix(a.FieldGlass, b.FieldGlass, k);
            BarTrack = Mix(a.BarTrack, b.BarTrack, k);
            CoverPlaceholder = Mix(a.CoverPlaceholder, b.CoverPlaceholder, k);
            ChipGlass = Mix(a.ChipGlass, b.ChipGlass, k);
            ChatExpanded = Mix(a.ChatExpanded, b.ChatExpanded, k);
            PanelSolid = Mix(a.PanelSolid, b.PanelSolid, k);
            Island0 = Mix(a.Island0, b.Island0, k);
            Island1 = Mix(a.Island1, b.Island1, k);
            PanelStrong0 = Mix(a.PanelStrong0, b.PanelStrong0, k);
            PanelStrong1 = Mix(a.PanelStrong1, b.PanelStrong1, k);
            Scrim = Mix(a.Scrim, b.Scrim, k);
            Fullscreen = Mix(a.Fullscreen, b.Fullscreen, k);
            FullscreenOpaque = Mix(a.FullscreenOpaque, b.FullscreenOpaque, k);
            ThemeGlassEdge = Mix(a.ThemeGlassEdge, b.ThemeGlassEdge, k);
            ThemeGlassFacet = Mix(a.ThemeGlassFacet, b.ThemeGlassFacet, k);
        }

        private static Color Mix(Color a, Color b, double k) => Color.FromRgb(
            (byte)(a.R + (b.R - a.R) * k),
            (byte)(a.G + (b.G - a.G) * k),
            (byte)(a.B + (b.B - a.B) * k));
    }

    /// <summary>
    /// v9.8 (review Codex P1 — churn): paleta VIVA, criada UMA vez.
    /// v9.8.1 (fix boot 21/10 — crash XamlParseException no ctor do MainWindow): o WPF
    /// congela Freezables instalados em QUALQUER ResourceDictionary em uso (Application OU
    /// Window — a sonda do v9.8 só mediu antes do primeiro render da janela). Mutação
    /// in-place num brush congelado lançava InvalidOperationException no primeiro
    /// SetAccent do ctor e o handler de dispatcher engolia a janela inteira (app zumbi,
    /// hwnd=0, processo vivo). Fix: caminho comum continua mutação in-place (churn zero);
    /// se a plataforma congelou, Clone() devolve cópia mutável e o dicionário é atualizado
    /// — DynamicResource re-resolve a referência nova nos consumidores automaticamente.
    /// </summary>
    internal sealed class LivePalette
    {
        public static LivePalette Instance { get; } = new();

        /// <summary>Mesclar em Window.Resources (NUNCA em Application.Resources — congela antes).</summary>
        public ResourceDictionary Dictionary { get; } = new();

        /// <summary>Estado padrão (tema ciano do App.xaml) — lido 1x, nunca mutado.</summary>
        public ThemeTokens DefaultTheme { get; }

        // ---- grupo ACENTO (depende só da cor do acento) ----
        // v9.8.1: sem readonly — o fix precisa reatribuir quando a plataforma congela.
        private SolidColorBrush _accent = new();
        private SolidColorBrush _accentDim = new();
        private SolidColorBrush _accentMedium = new();
        private SolidColorBrush _accentFaint = new();
        private SolidColorBrush _accentGhost = new();
        private SolidColorBrush _accentInk = new();
        private SolidColorBrush _accentEdge = new();
        private SolidColorBrush _reactorAccent = new();
        private SolidColorBrush _reactorAccentDim = new();
        private LinearGradientBrush _reactorRim = new()
        { StartPoint = new Point(0.5, 0), EndPoint = new Point(0.5, 1) };
        private RadialGradientBrush _reactorCore = new()
        { GradientOrigin = new Point(0.42, 0.36), Center = new Point(0.5, 0.5), RadiusX = 0.65, RadiusY = 0.65 };
        private RadialGradientBrush _reactorOrb = new()
        { GradientOrigin = new Point(0.38, 0.32), Center = new Point(0.5, 0.5), RadiusX = 0.62, RadiusY = 0.62 };
        private RadialGradientBrush _reactorHalo = new()
        { Center = new Point(0.5, 0.5), RadiusX = 0.5, RadiusY = 0.5 };
        private SolidColorBrush _surfaceTint = new();
        private SolidColorBrush _panelEdgeLive = new();
        private RadialGradientBrush _musicGlow = new()
        { Center = new Point(0.5, 0.5), RadiusX = 0.5, RadiusY = 0.5 };

        // ---- grupo TEMA (superfícies/texto — depende só da cor da capa) ----
        private SolidColorBrush _themeSecondary = new();
        private SolidColorBrush _themeSecondaryDim = new();
        private SolidColorBrush _themeSecondaryFaint = new();
        private SolidColorBrush _themeSecondaryGhost = new();
        private SolidColorBrush _themeTextStrong = new();
        private SolidColorBrush _themeText = new();
        private SolidColorBrush _themeLabel = new();
        private SolidColorBrush _themeTextDim = new();
        private SolidColorBrush _gridPen = new();
        private SolidColorBrush _scanPen = new();
        private DrawingBrush _themeGrid;
        private DrawingBrush _themeScan;
        private RadialGradientBrush _themeShell = new()
        { GradientOrigin = new Point(0.5, 0.32), Center = new Point(0.5, 0.4), RadiusX = 0.78, RadiusY = 0.95 };
        private RadialGradientBrush _themeVignette = new()
        { Center = new Point(0.5, 0.5), RadiusX = 0.75, RadiusY = 0.8 };
        private SolidColorBrush _themeMediaGlass = new();
        private SolidColorBrush _themeFieldGlass = new();
        private SolidColorBrush _themeBarTrack = new();
        private SolidColorBrush _themeCoverPlaceholder = new();
        private SolidColorBrush _themeChipGlass = new();
        private SolidColorBrush _themeChatExpanded = new();
        private SolidColorBrush _themeChatExpandedDense = new(); // F2.4: face de leitura do chat expandido
        private SolidColorBrush _themePanelSolid = new();
        private SolidColorBrush _themeFullscreen = new();
        private SolidColorBrush _themeFullscreenOpaque = new();
        // F1: aro de cristal — brancos neutros (R13: zero acento na face/aro). O specular
        // não tem brush próprio: é o AccentBrushFaint (0x33 exato, grupo do acento).
        private SolidColorBrush _glassEdge = new();
        private SolidColorBrush _glassFacet = new();
        private LinearGradientBrush _islandGlass = new()
        { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
        private LinearGradientBrush _panelGlassStrong = new()
        { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
        private SolidColorBrush _scrim = new();

        private LivePalette()
        {
            var res = Application.Current.Resources;

            // Estruturas fixas (offsets/geometria) — criadas UMA vez.
            _reactorRim.GradientStops.Add(new GradientStop(Colors.Transparent, 0));
            _reactorRim.GradientStops.Add(new GradientStop(Colors.Transparent, 0.55));
            _reactorRim.GradientStops.Add(new GradientStop(Colors.Transparent, 1));
            _reactorCore.GradientStops.Add(new GradientStop(Colors.Transparent, 0));
            _reactorCore.GradientStops.Add(new GradientStop(Colors.Transparent, 0.55));
            _reactorCore.GradientStops.Add(new GradientStop(Colors.Transparent, 1));
            _reactorOrb.GradientStops.Add(new GradientStop(Colors.Transparent, 0));
            _reactorOrb.GradientStops.Add(new GradientStop(Colors.Transparent, 0.5));
            _reactorOrb.GradientStops.Add(new GradientStop(Colors.Transparent, 1));
            _reactorHalo.GradientStops.Add(new GradientStop(Colors.Transparent, 0));
            _reactorHalo.GradientStops.Add(new GradientStop(Colors.Transparent, 0.6));
            _reactorHalo.GradientStops.Add(new GradientStop(Colors.Transparent, 1));
            _musicGlow.GradientStops.Add(new GradientStop(Colors.Transparent, 0));
            _musicGlow.GradientStops.Add(new GradientStop(Colors.Transparent, 0.55));
            _musicGlow.GradientStops.Add(new GradientStop(Colors.Transparent, 1));

            _themeShell.GradientStops.Add(new GradientStop(Colors.Transparent, 0));
            _themeShell.GradientStops.Add(new GradientStop(Colors.Transparent, 0.55));
            _themeShell.GradientStops.Add(new GradientStop(Colors.Transparent, 1));
            _themeVignette.GradientStops.Add(new GradientStop(Colors.Transparent, 0.55));
            _themeVignette.GradientStops.Add(new GradientStop(Colors.Transparent, 1));
            _islandGlass.GradientStops.Add(new GradientStop(Colors.Transparent, 0));
            _islandGlass.GradientStops.Add(new GradientStop(Colors.Transparent, 1));
            _panelGlassStrong.GradientStops.Add(new GradientStop(Colors.Transparent, 0));
            _panelGlassStrong.GradientStops.Add(new GradientStop(Colors.Transparent, 1));

            var gridPen = new Pen(_gridPen, 1);
            var gridDrawing = new GeometryDrawing { Geometry = Geometry.Parse("M0,0 H44 M0,0 V44"), Pen = gridPen };
            _themeGrid = new DrawingBrush(gridDrawing)
            { TileMode = TileMode.Tile, Viewport = new Rect(0, 0, 44, 44), ViewportUnits = BrushMappingMode.Absolute };
            var scanPen = new Pen(_scanPen, 1);
            var scanDrawing = new GeometryDrawing { Geometry = Geometry.Parse("M0,1 H4"), Pen = scanPen };
            _themeScan = new DrawingBrush(scanDrawing)
            { TileMode = TileMode.Tile, Viewport = new Rect(0, 0, 4, 4), ViewportUnits = BrushMappingMode.Absolute };

            Dictionary["AccentBrush"] = _accent;
            Dictionary["AccentBrushDim"] = _accentDim;
            Dictionary["AccentBrushMedium"] = _accentMedium;
            Dictionary["AccentBrushFaint"] = _accentFaint;
            Dictionary["AccentBrushGhost"] = _accentGhost;
            Dictionary["AccentBrushInk"] = _accentInk;
            Dictionary["AccentEdgeBrush"] = _accentEdge;
            Dictionary["ReactorAccentBrush"] = _reactorAccent;
            Dictionary["ReactorAccentDimBrush"] = _reactorAccentDim;
            Dictionary["ReactorRim"] = _reactorRim;
            Dictionary["ReactorCoreFill"] = _reactorCore;
            Dictionary["ReactorOrbFill"] = _reactorOrb;
            Dictionary["ReactorHalo"] = _reactorHalo;
            Dictionary["SurfaceTintBrush"] = _surfaceTint;
            Dictionary["PanelEdgeLiveBrush"] = _panelEdgeLive;
            Dictionary["MusicGlowBrush"] = _musicGlow;

            Dictionary["ThemeSecondaryBrush"] = _themeSecondary;
            Dictionary["ThemeSecondaryDimBrush"] = _themeSecondaryDim;
            Dictionary["ThemeSecondaryFaintBrush"] = _themeSecondaryFaint;
            Dictionary["ThemeSecondaryGhostBrush"] = _themeSecondaryGhost;
            Dictionary["ThemeTextStrongBrush"] = _themeTextStrong;
            Dictionary["ThemeTextBrush"] = _themeText;
            Dictionary["ThemeLabelBrush"] = _themeLabel;
            Dictionary["ThemeTextDimBrush"] = _themeTextDim;
            Dictionary["ThemeGridBrush"] = _themeGrid;
            Dictionary["ThemeScanBrush"] = _themeScan;
            Dictionary["ThemeShellBrush"] = _themeShell;
            Dictionary["ThemeVignetteBrush"] = _themeVignette;
            Dictionary["ThemeMediaGlassBrush"] = _themeMediaGlass;
            Dictionary["ThemeFieldGlassBrush"] = _themeFieldGlass;
            Dictionary["ThemeBarTrackBrush"] = _themeBarTrack;
            Dictionary["ThemeCoverPlaceholderBrush"] = _themeCoverPlaceholder;
            Dictionary["ThemeChipGlassBrush"] = _themeChipGlass;
            Dictionary["ThemeChatExpandedBrush"] = _themeChatExpanded;
            Dictionary["ThemeChatExpandedDenseBrush"] = _themeChatExpandedDense;
            Dictionary["ThemePanelSolidBrush"] = _themePanelSolid;
            Dictionary["ThemeFullscreenBrush"] = _themeFullscreen;
            Dictionary["ThemeFullscreenOpaqueBrush"] = _themeFullscreenOpaque;
            Dictionary["GlassEdgeBrush"] = _glassEdge;
            Dictionary["GlassFacetBrush"] = _glassFacet;
            Dictionary["IslandGlass"] = _islandGlass;
            Dictionary["PanelGlassStrong"] = _panelGlassStrong;
            Dictionary["ScrimBrush"] = _scrim;

            DefaultTheme = ReadDefaultTheme(res);
            ApplyTheme(DefaultTheme);

            var accent = Solid(res, "AccentBrush", Color.FromRgb(0x00, 0xD9, 0xFF));
            var reactor = Solid(res, "ReactorAccentBrush", Color.FromRgb(0x00, 0xE5, 0xFF));
            SetAccent(accent, reactor);
        }

        public Brush? Get(string key) => Dictionary[key] as Brush;

        // ---- v9.8.1 helpers: mutação in-place quando vivo; quando congelado, construir/mutar
        // FORA e instalar POR ÚLTIMO — a inserção em dicionário em uso SELA o valor no ato
        // (comprovado 21/10: o clone instalado na linha anterior congelou e a mutação seguinte explodiu) ----

        private void Solid(ref SolidColorBrush b, string key, Color c)
        {
            if (!b.IsFrozen) { b.Color = c; return; }
            b = new SolidColorBrush(c);
            Dictionary[key] = b;
        }

        private void Grad<T>(ref T g, string key, Action<T> apply) where T : GradientBrush
        {
            if (!g.IsFrozen) { apply(g); return; }
            var n = (T)g.Clone();
            apply(n);
            g = n;
            Dictionary[key] = n;
        }

        /// <summary>DrawingBrush (grid/scan) segura o Pen cujo Brush é o campo. Clone()
        /// descongela a árvore inteira: mutar o pen do clone ANTES de instalar no
        /// dicionário (a inserção sela) e re-alinhar o campo pra próximas trocas.</summary>
        private void PenBrush(ref DrawingBrush d, string key, ref SolidColorBrush pen, Color c)
        {
            if (d.IsFrozen)
            {
                var n = (DrawingBrush)d.Clone();
                var pb = (SolidColorBrush)((GeometryDrawing)n.Drawing).Pen.Brush;
                pb.Color = c;
                pen = pb;
                d = n;
                Dictionary[key] = n;
                return;
            }
            // freeze é recursivo: pen só congela junto com d — guard de segurança
            if (pen.IsFrozen) { pen = new SolidColorBrush(c); return; }
            pen.Color = c;
        }

        /// <summary>Atualiza SÓ o grupo do acento (7 brushes + reator + tint/glow) — churn zero no caminho comum.</summary>
        public void SetAccent(Color c, Color bright)
        {
            c = Opaque(c);
            bright = Opaque(bright);

            Solid(ref _accent, "AccentBrush", c);
            Solid(ref _accentDim, "AccentBrushDim", WithA(c, 0x59));
            Solid(ref _accentMedium, "AccentBrushMedium", WithA(c, 0x88));
            Solid(ref _accentFaint, "AccentBrushFaint", WithA(c, 0x33));
            Solid(ref _accentGhost, "AccentBrushGhost", WithA(c, 0x1A));
            Solid(ref _accentInk, "AccentBrushInk", WithA(c, 0x0D));
            Solid(ref _accentEdge, "AccentEdgeBrush", WithA(c, 0x3D));

            Solid(ref _reactorAccent, "ReactorAccentBrush", bright);
            Solid(ref _reactorAccentDim, "ReactorAccentDimBrush", WithA(bright, 0x59));
            Grad(ref _reactorRim, "ReactorRim", g =>
            {
                g.GradientStops[0].Color = WithA(bright, 0xE6);
                g.GradientStops[1].Color = WithA(bright, 0x26);
                g.GradientStops[2].Color = WithA(bright, 0x0F);
            });
            Grad(ref _reactorCore, "ReactorCoreFill", g =>
            {
                g.GradientStops[0].Color = WithA(bright, 0x66);
                g.GradientStops[1].Color = WithA(bright, 0x33);
                g.GradientStops[2].Color = WithA(bright, 0x12);
            });
            Grad(ref _reactorOrb, "ReactorOrbFill", g =>
            {
                g.GradientStops[0].Color = WithA(bright, 0xE6);
                g.GradientStops[1].Color = WithA(bright, 0x8A);
                g.GradientStops[2].Color = WithA(bright, 0x38);
            });
            Grad(ref _reactorHalo, "ReactorHalo", g =>
            {
                g.GradientStops[0].Color = WithA(bright, 0x17);
                g.GradientStops[1].Color = WithA(bright, 0x0A);
                g.GradientStops[2].Color = Color.FromArgb(0, bright.R, bright.G, bright.B);
            });
            Grad(ref _musicGlow, "MusicGlowBrush", g =>
            {
                g.GradientStops[0].Color = WithA(bright, 0x40);
                g.GradientStops[1].Color = WithA(bright, 0x12);
                g.GradientStops[2].Color = Color.FromArgb(0, bright.R, bright.G, bright.B);
            });

            // tint de superfície: 5% do acento sobre o navy fixo, alpha 12%.
            var navy = Color.FromRgb(0x0E, 0x1B, 0x2E);
            Solid(ref _surfaceTint, "SurfaceTintBrush", Color.FromArgb(0x1F,
                (byte)(navy.R + (c.R - navy.R) * 0.05),
                (byte)(navy.G + (c.G - navy.G) * 0.05),
                (byte)(navy.B + (c.B - navy.B) * 0.05)));
            Solid(ref _panelEdgeLive, "PanelEdgeLiveBrush", WithA(c, 0x33));

            // 3 cores de efeito (Color resources) — únicos valores que não são brush;
            // continuam por DynamicResource nos DropShadowEffect.
            Application.Current.Resources["AccentColor"] = c;
            Application.Current.Resources["ReactorAccentColor"] = bright;
        }

        /// <summary>Atualiza SÓ o grupo do tema — churn zero no caminho comum.</summary>
        public void ApplyTheme(ThemeTokens t)
        {
            Solid(ref _themeSecondary, "ThemeSecondaryBrush", Opaque(t.Secondary));
            Solid(ref _themeSecondaryDim, "ThemeSecondaryDimBrush", WithA(t.Secondary, 0x59));
            Solid(ref _themeSecondaryFaint, "ThemeSecondaryFaintBrush", WithA(t.Secondary, 0x33));
            Solid(ref _themeSecondaryGhost, "ThemeSecondaryGhostBrush", WithA(t.Secondary, 0x1A));

            Solid(ref _themeTextStrong, "ThemeTextStrongBrush", Opaque(t.TextStrong));
            Solid(ref _themeText, "ThemeTextBrush", Opaque(t.Text));
            Solid(ref _themeLabel, "ThemeLabelBrush", Opaque(t.Label));
            Solid(ref _themeTextDim, "ThemeTextDimBrush", Opaque(t.TextDim));

            PenBrush(ref _themeGrid, "ThemeGridBrush", ref _gridPen, Opaque(t.GridLine));
            PenBrush(ref _themeScan, "ThemeScanBrush", ref _scanPen, Opaque(t.ScanLine));

            Grad(ref _themeShell, "ThemeShellBrush", g =>
            {
                g.GradientStops[0].Color = WithA(t.Shell0, 0xD0);
                g.GradientStops[1].Color = WithA(t.Shell1, 0xD8);
                g.GradientStops[2].Color = WithA(t.Shell2, 0xE0);
            });
            Grad(ref _themeVignette, "ThemeVignetteBrush", g =>
            {
                g.GradientStops[0].Color = Color.FromArgb(0, t.VignetteEdge.R, t.VignetteEdge.G, t.VignetteEdge.B);
                g.GradientStops[1].Color = WithA(t.VignetteEdge, 0x99);
            });

            Solid(ref _themeMediaGlass, "ThemeMediaGlassBrush", WithA(t.MediaGlass, 0x99));
            Solid(ref _themeFieldGlass, "ThemeFieldGlassBrush", WithA(t.FieldGlass, 0x17));
            Solid(ref _themeBarTrack, "ThemeBarTrackBrush", WithA(t.BarTrack, 0x14));
            Solid(ref _themeCoverPlaceholder, "ThemeCoverPlaceholderBrush", WithA(t.CoverPlaceholder, 0x33));
            Solid(ref _themeChipGlass, "ThemeChipGlassBrush", WithA(t.ChipGlass, 0x22));
            Solid(ref _themeChatExpanded, "ThemeChatExpandedBrush", WithA(t.ChatExpanded, 0x38));
            // F2.4: mesma base do token, alpha de LEITURA (0xE6) — medido no composto.
            Solid(ref _themeChatExpandedDense, "ThemeChatExpandedDenseBrush", WithA(t.ChatExpanded, 0xE6));
            Solid(ref _themePanelSolid, "ThemePanelSolidBrush", WithA(t.PanelSolid, 0xFF));
            Solid(ref _themeFullscreen, "ThemeFullscreenBrush", WithA(t.Fullscreen, 0xCC));
            Solid(ref _themeFullscreenOpaque, "ThemeFullscreenOpaqueBrush", WithA(t.FullscreenOpaque, 0xF5));

            Grad(ref _islandGlass, "IslandGlass", g =>
            {
                g.GradientStops[0].Color = WithA(t.Island0, 0x66);
                g.GradientStops[1].Color = WithA(t.Island1, 0x4D);
            });
            Grad(ref _panelGlassStrong, "PanelGlassStrong", g =>
            {
                g.GradientStops[0].Color = WithA(t.PanelStrong0, 0xF2);
                g.GradientStops[1].Color = WithA(t.PanelStrong1, 0xE6);
            });
            Solid(ref _scrim, "ScrimBrush", WithA(t.Scrim, 0xF5));

            // F1 — aro de cristal: tokens opacos + alpha na montagem (Mix descarta alpha).
            Solid(ref _glassEdge, "GlassEdgeBrush", WithA(t.ThemeGlassEdge, 0x19));   // 9,8% — 8-10%
            Solid(ref _glassFacet, "GlassFacetBrush", WithA(t.ThemeGlassFacet, 0x1F)); // 12,2% — faceta 45°

            Application.Current.Resources["ThemeSecondaryColor"] = Opaque(t.Secondary);
        }

        private static ThemeTokens ReadDefaultTheme(ResourceDictionary res) => new()
        {
            Secondary = Rgb(ColorOf(res, "ThemeSecondaryColor", Color.FromRgb(0x00, 0xFF, 0x88))),
            TextStrong = Rgb(Solid(res, "ThemeTextStrongBrush", Color.FromRgb(0xF3, 0xF4, 0xF4))),
            Text = Rgb(Solid(res, "ThemeTextBrush", Color.FromRgb(0xB3, 0xBB, 0xBD))),
            Label = Rgb(Solid(res, "ThemeLabelBrush", Color.FromRgb(0x8C, 0x99, 0x9B))),
            TextDim = Rgb(Solid(res, "ThemeTextDimBrush", Color.FromRgb(0x55, 0x60, 0x62))),
            GridLine = Rgb(Pen(res, "ThemeGridBrush", Color.FromRgb(0x0F, 0x38, 0x4B))),
            ScanLine = Rgb(Pen(res, "ThemeScanBrush", Color.FromRgb(0x0E, 0x32, 0x47))),
            Shell0 = Rgb(Stop(res, "ThemeShellBrush", 0, Color.FromRgb(0x0D, 0x2E, 0x43))),
            Shell1 = Rgb(Stop(res, "ThemeShellBrush", 1, Color.FromRgb(0x09, 0x20, 0x30))),
            Shell2 = Rgb(Stop(res, "ThemeShellBrush", 2, Color.FromRgb(0x07, 0x17, 0x23))),
            VignetteEdge = Rgb(Stop(res, "ThemeVignetteBrush", 1, Color.FromRgb(0x02, 0x15, 0x1E))),
            MediaGlass = Rgb(Solid(res, "ThemeMediaGlassBrush", Color.FromRgb(0x0E, 0x1B, 0x2E))),
            FieldGlass = Rgb(Solid(res, "ThemeFieldGlassBrush", Color.FromRgb(0x0B, 0x1E, 0x38))),
            BarTrack = Rgb(Solid(res, "ThemeBarTrackBrush", Color.FromRgb(0x00, 0xFF, 0x88))),
            CoverPlaceholder = Rgb(Solid(res, "ThemeCoverPlaceholderBrush", Color.FromRgb(0x1E, 0x4E, 0x8C))),
            ChipGlass = Rgb(Solid(res, "ThemeChipGlassBrush", Color.FromRgb(0x00, 0x0C, 0x14))),
            ChatExpanded = Rgb(Solid(res, "ThemeChatExpandedBrush", Color.FromRgb(0x0E, 0x1B, 0x2E))),
            PanelSolid = Rgb(Solid(res, "ThemePanelSolidBrush", Color.FromRgb(0x07, 0x0C, 0x14))),
            Island0 = Rgb(Stop(res, "IslandGlass", 0, Color.FromRgb(0x0E, 0x1B, 0x2E))),
            Island1 = Rgb(Stop(res, "IslandGlass", 1, Color.FromRgb(0x0E, 0x1B, 0x2E))),
            PanelStrong0 = Rgb(Stop(res, "PanelGlassStrong", 0, Color.FromRgb(0x0E, 0x1B, 0x2E))),
            PanelStrong1 = Rgb(Stop(res, "PanelGlassStrong", 1, Color.FromRgb(0x0E, 0x1B, 0x2E))),
            Scrim = Rgb(Solid(res, "ScrimBrush", Color.FromRgb(0x06, 0x0A, 0x12))),
            Fullscreen = Rgb(Solid(res, "ThemeFullscreenBrush", Color.FromRgb(0x0A, 0x13, 0x22))),
            FullscreenOpaque = Rgb(Solid(res, "ThemeFullscreenOpaqueBrush", Color.FromRgb(0x0A, 0x13, 0x22))),
            ThemeGlassEdge = Rgb(Solid(res, "GlassEdgeBrush", Colors.White)),
            ThemeGlassFacet = Rgb(Solid(res, "GlassFacetBrush", Colors.White))
        };

        private static Color ColorOf(ResourceDictionary res, string key, Color fallback) =>
            res[key] is Color c ? c : fallback;

        private static Color Solid(ResourceDictionary res, string key, Color fallback) =>
            res[key] is SolidColorBrush b ? b.Color : fallback;

        private static Color Stop(ResourceDictionary res, string key, int index, Color fallback) =>
            res[key] is GradientBrush g && g.GradientStops.Count > index ? g.GradientStops[index].Color : fallback;

        private static Color Pen(ResourceDictionary res, string key, Color fallback) =>
            res[key] is DrawingBrush { Drawing: GeometryDrawing { Pen.Brush: SolidColorBrush b } } ? b.Color : fallback;

        private static Color Rgb(Color c) => Color.FromRgb(c.R, c.G, c.B);

        private static Color Opaque(Color c) => Color.FromArgb(0xFF, c.R, c.G, c.B);

        private static Color WithA(Color c, byte a) => Color.FromArgb(a, c.R, c.G, c.B);
    }
}
