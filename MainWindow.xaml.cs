using System;
using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using KatieHoloDeck.Core;
using IOPath = System.IO.Path;

namespace KatieHoloDeck
{
    public partial class MainWindow : System.Windows.Window
    {
        // ===== Animação =====
        private readonly DispatcherTimer _renderTimer = new() { Interval = TimeSpan.FromMilliseconds(16) };
        /// <summary>rotações do reator: transform cacheado + velocidade (graus/s) — atualizados no RenderTick.</summary>
        private readonly List<(RotateTransform Rot, double DegPerSec)> _rotors = new();

        // ===== Reator 3D interativo (v3): parallax tilt + zoom com easing =====
        private double _tiltX, _tiltY, _tiltTargetX, _tiltTargetY;
        private double _zoomValue = 1.0, _zoomTarget = 1.0;
        private const double MaxTiltDeg = 8.0;
        private const double ZoomMin = 0.8, ZoomMax = 1.3;

        // ===== FASE 1 (v5): modo música FULLSCREEN + letra LRC =====
        private bool _fsMusicOpen;
        private string _fsArtKey = "";
        private string _fsTitleLast = "";
        private string _fsArtistLast = "";
        private LyricLine[]? _fsLyrics;
        private readonly List<TextBlock> _fsLineBlocks = new();
        private int _fsLyricIndex = -1;
        private TimeSpan _fsPosBase;
        private DateTime _fsPosBaseAt = DateTime.UtcNow;
        private bool _fsPlaying;
        private double _fsScrollTarget, _fsScrollCurrent;
        private readonly DispatcherTimer _lyricsTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
        private static readonly Brush FsLineActive = FrozenFsBrush(Color.FromRgb(0xF3, 0xF4, 0xF4));
        private static readonly Brush FsLineNear = FrozenFsBrush(Color.FromRgb(0xB3, 0xBB, 0xBD));
        private static readonly Brush FsLineFar = FrozenFsBrush(Color.FromRgb(0x8C, 0x99, 0x9B));

        private static Brush FrozenFsBrush(Color c)
        {
            var b = new SolidColorBrush(c);
            b.Freeze();
            return b;
        }

        /// <summary>v9.8: brush do tema vivo (dicionário próprio da janela, não congelado),
        /// com fallback congelado (letras do fullscreen).</summary>
        private static Brush ThemedBrush(string key, Brush fallback) =>
            LivePalette.Instance.Get(key) ?? fallback;
        private double _breath;
        private double _talkGlow;
        private double _corePulsePhase;

        // ===== Estado (state.json — API da KATIE) =====
        private readonly DispatcherTimer _stateTimer = new() { Interval = TimeSpan.FromSeconds(2) };
        private readonly FileSystemWatcher? _stateWatcher;
        private readonly string _statePath;
        private DateTime _lastStateWrite = DateTime.MinValue;
        private int _lastReplyId = -1;

        // ===== Telemetria =====
        private readonly DispatcherTimer _telemetryTimer = new() { Interval = TimeSpan.FromSeconds(2) };
        private double _lastCpu = -1;
        private long _prevNetRx, _prevNetTx;
        private DateTime _prevNetUtc = DateTime.MinValue;

        // ===== Relógio =====
        private readonly DispatcherTimer _clockTimer = new() { Interval = TimeSpan.FromSeconds(1) };

        private string _mode = "idle";
        private string _backdrop = "mica"; // Fase 2: campo "backdrop" do state.json (mica|acrylic|solid)
        private PlayerWindow? _player;
        private readonly MediaTracker _media = new();
        private readonly AudioSpectrum _spectrum = new();
        private bool _mediaPlaying;
        private string _sourceAumid = ""; // v9.7 P2 (escopo): AUMID já aplicado nos selos inline/fullscreen
        private bool _mediaPanelOpen;
        private DropShadowEffect? _coreGlowEffect;

        // ===== chatOnTop: reafirma HWND_TOPMOST a cada ~2 s (só com o setting ON) =====
        private readonly DispatcherTimer _topmostTimer = new() { Interval = TimeSpan.FromSeconds(2) };
        private bool _topmostApplied;

        // ===== coverAccent: cor dominante da capa + transição de 600 ms nos recursos vivos =====
        private readonly DispatcherTimer _paletteTimer = new() { Interval = TimeSpan.FromMilliseconds(16) };
        private const double PaletteTransitionMs = 600.0;
        private const double MinAccentLuminance = 0.22; // contraste ~5:1 com texto branco sobre fundo escuro
        private DateTime _paletteStartAt;
        private Color _paletteFrom;
        private Color _paletteTo;
        private Color _paletteCurrent = Color.FromRgb(0x00, 0xD9, 0xFF);
        private Color? _wheelAccent; // última cor enviada à roda (quantizada, review P1 churn)
        private string _coverAccentKey = "";
        private int _coverThumbHash; // v9.8 (review P3): a chave do cache inclui a capa
        private Color? _coverAccentColor;
        private Color? _coverThemeColor;
        private bool _coverAccentApplied;

        // ===== v9.8: tema GLOBAL da capa (grupo Theme* da LivePalette) =====
        // Brushes/gradientes/pens vivem num ResourceDictionary da JANELA (mutável) e são
        // reescritos no lugar — nenhum Freezable novo por frame (review P1 churn).
        private readonly ThemeTokens _themeCurrent = new();
        private readonly ThemeTokens _themeFrom = new();
        private readonly ThemeTokens _themeTo = new();
        private readonly ThemeTokens _themeScratch = new();
        private Color? _themeAppliedBase; // null = tema padrão; != null = RGB do tema atual
        private bool _themeAnimating;

        // ===== Configurações (settings.json) =====
        private readonly string _settingsPath;
        private AppSettings _settings;
        private readonly SettingsLoadStatus _settingsStatus;
        private bool _settingsOpen;

        // ===== Detecção de jogos (local, opcional) =====
        private GameDetectionService? _games;

        // ===== Passe Fluent (efeitos / entrada / ponto do logo) =====
        private double _motionScale = 1.0;   // 1.0 completo · 0.5 reduzido · 0 parado
        private double _glowScale = 1.0;     // amplitude do breathe/glow
        private bool _reactorPaused;         // F2.5: reator congelado sob o fullscreen (Z50)
        private ScaleTransform? _coreScale;  // cache do transform do core (zero new/frame)
        private double _wireAngle;  // reator v2: fase do spin dos meridianos
        private double _sweepT;     // reator v2: posição da banda de sweep

        // ===== v8 (hang): reator v2 TRANSFORM-ONLY =====
        // O SetMerid antigo escrevia Width + Canvas.Left a CADA FRAME (invalidação de LAYOUT
        // dentro do Viewport2DVisual3D — re-layout/re-render completo da cena 3D por frame) e
        // ainda usava offset "145" indevido para filhos do WireFrame (coords 0-130 locais).
        // Agora: geometria FIXA + ScaleX animado (mesma matemática do |cos λ|, zero layout).
        private readonly List<(System.Windows.Shapes.Ellipse El, ScaleTransform Sx, int Kind)> _merids = new();
        private TranslateTransform? _sweepXform; // reutilizada (zero alloc/frame)
        // quantização p/ não re-rasterizar efeitos quando o valor não muda de verdade
        private double _lastGlowBlur = double.NaN, _lastGlowOpacity = double.NaN, _lastDotGlow = double.NaN;

        // v9.3: blur REAL de fundo — um único BlurEffect no AppContent; Configurações e
        // fullscreen de música ficam NÍTIDOS por cima (fora da subárvore do blur).
        private readonly BlurEffect _backdropBlur = new BlurEffect { Radius = 0, KernelType = KernelType.Gaussian };

        // v9.3.2: TODOS os elementos com TechFrame DENTRO do AppContent — adorners desenham
        // acima do BlurEffect e ficavam nítidos "destacados" sobre o fundo borrado.
        // AppShell (moldura da janela) fica DE FORA: deve continuar nítida sempre.
        // (o contrato chama o 3º de "ChipFrame"; o Border do overlay de música já tem
        //  x:Name="MediaPanel" — não dá para duplicar x:Name, então usamos o nome existente.)
        private readonly List<FrameworkElement> _techFramedApp = new();

        private void InitTechFramedApp()
        {
            _techFramedApp.Add(IslandTelemetry);
            _techFramedApp.Add(IslandChat);
            _techFramedApp.Add(MediaPanel);
        }

        // v9.3.3: elementos que RECUAM (esmaecem) quando um painel de foco abre —
        // silhueta legível sob o blur ainda "grita" (bordas persistentes, report do sir).
        // AppShell (moldura da janela) fica de fora: nítida sempre.
        private readonly List<FrameworkElement> _recedeApp = new();

        private void InitRecedeApp()
        {
            _recedeApp.Add(IslandTelemetry);
            _recedeApp.Add(IslandChat);
            _recedeApp.Add(ChipPanel);
            _recedeApp.Add(MediaPanel);
        }

        private void RecedeApp(double opacity)
        {
            foreach (var el in _recedeApp)
            {
                // v9.3.3-fix (Codex P3): MediaPanel fechado usa a própria opacity pro
                // fade de entrada/saída (SetMediaPanel) — setar 1.0 nele mataria o fade
                // da próxima abertura. Recuo/restauração só tocam o painel ABERTO.
                if (ReferenceEquals(el, MediaPanel) && !_mediaPanelOpen) continue;
                if (MotionEnabled)
                {
                    var anim = new DoubleAnimation(opacity, TimeSpan.FromMilliseconds(180)) { EasingFunction = new QuadraticEase() };
                    el.BeginAnimation(OpacityProperty, anim);
                }
                else
                {
                    el.BeginAnimation(OpacityProperty, null);
                    el.Opacity = opacity;
                }
            }
        }

        // v9.3.4: o recuo v9.3.3 (opacity 0.35) + EnableFrames (v9.3.2) não matavam a
        // percepção de "bordas nítidas" porque o CONTORNO octogonal das ilhas/chips/MediaPanel
        // NÃO é o TechFrame (brackets) — é o ChamferAdorner (ver Chamfer.cs). Chamfer e TechFrame
        // são Adorners: desenham na AdornerLayer, ACIMA do BlurEffect do AppContent, e NÃO herdam
        // Opacity nem Effect do elemento adornado. Com o blur ligado, o interior borrava e recuava,
        // mas o traço do octógono (com o BorderBrush do elemento — ciano cheio nos chips, hairline
        // 0x59 nas ilhas) continuava nítido. Aqui colapsamos TODOS os adorners dos descendentes do
        // AppContent junto com as molduras. Colapsar (não remover) é estável: Chamfer.Apply reusa o
        // adorner existente em SizeChanged/Loaded, então ele não "renasce" visível no meio do blur.
        // Elementos FORA do AppContent (AppShell/moldura, FullscreenMusic, painel de Configurações)
        // não são tocados — seguem nítidos por design.
        private void SetAppContentAdornersVisible(bool visible) => SetSubtreeAdornersVisible(AppContent, visible);

        // v9.3.6: generalização do v9.3.4 — o painel de Configurações é IRMÃO do AppContent
        // (SettingsHost, linha do MainWindow.xaml), fora do walk, então seu ChamferAdorner
        // nunca era colapsado no fechamento: o fade levava o painel (Opacity) mas o octógono
        // é Adorner — não herda Opacity — e ficava órfão visível "pra trás" (report sir).
        private static void SetSubtreeAdornersVisible(FrameworkElement root, bool visible)
        {
            var layer = AdornerLayer.GetAdornerLayer(root);
            if (layer == null) return;
            Walk(root);

            void Walk(DependencyObject node)
            {
                if (node is FrameworkElement fe && layer.GetAdorners(fe) is { } ads)
                    foreach (var ad in ads)
                        ad.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
                int n = VisualTreeHelper.GetChildrenCount(node);
                for (int i = 0; i < n; i++) Walk(VisualTreeHelper.GetChild(node, i));
            }
        }

        private void SetBackdropBlur(double to, FrameworkElement? keepFrame = null)
        {
            if (AppContent.Effect != _backdropBlur) AppContent.Effect = _backdropBlur;
            if (MotionEnabled)
            {
                var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
                var ms = to > _backdropBlur.Radius ? 240 : 200;
                var blurAnim = new DoubleAnimation(to, TimeSpan.FromMilliseconds(ms)) { EasingFunction = ease };
                if (to <= 0)
                {
                    // v9.3.2-fix (Codex P2): re-ligar molduras só DEPOIS que o blur saiu —
                    // antes elas voltavam nítidas sobre o blur residual (e recriavam a do
                    // fullscreen no meio do fade-out, ficando "destacada").
                    blurAnim.Completed += (_, _) =>
                    {
                        if (_backdropBlur.Radius > 0.01) return; // novo blur entrou no meio
                        AppContent.Effect = null;
                        ApplyFrameVisibility(true);
                        RecedeApp(1.0);
                    };
                }
                _backdropBlur.BeginAnimation(BlurEffect.RadiusProperty, blurAnim);
            }
            else
            {
                _backdropBlur.BeginAnimation(BlurEffect.RadiusProperty, null);
                _backdropBlur.Radius = to;
                if (to <= 0) AppContent.Effect = null;
            }
            // v9.3.2: blur ligado → TODAS as molduras do AppContent somem (adorner não borra,
            // ficava "destacado"); painel em foco (keepFrame) mantém a sua. Desligar é IMEDIATO;
            // re-ligar (to<=0) espera o blur terminar (Completed acima).
            void EnableFrames(bool on)
            {
                foreach (var el in _techFramedApp)
                    TechFrame.SetEnabled(el, on || ReferenceEquals(el, keepFrame));
                TechFrame.SetEnabled(FullscreenMusic, on || ReferenceEquals(FullscreenMusic, keepFrame));
            }
            // v9.3.4: além das molduras (TechFrame), colapsa também os adorners internos do
            // AppContent (ChamferAdorner) — ver SetAppContentAdornersVisible. Fora do AppContent
            // (AppShell, fullscreen, Configurações) permanece tudo nítido.
            void ApplyFrameVisibility(bool on)
            {
                EnableFrames(on);
                SetAppContentAdornersVisible(on);
            }
            if (to > 0) ApplyFrameVisibility(false); // v9.3.6-fix: polaridade do v9.3.4 estava INVERTIDA — blur
                                                      // LIGANDO chamava com true e nada escondia ("bordas não
                                                      // ficam borradas", report sir). Esconde na hora; a restauração
                                                      // fica no Completed (guard) / instantâneo — nunca antes do blur sair.
            if (to > 0) RecedeApp(0.35);
            if (!MotionEnabled && to <= 0) { ApplyFrameVisibility(true); RecedeApp(1.0); } // instantâneo: sem animação a esperar
        }

        private bool _dotHover;
        private bool _entranceDone;
        private const double MediaPanelHeight = 124;

        private bool MotionEnabled => _motionScale > 0;
        private bool InstantMedia => !MotionEnabled || !_settings.Music.AnimateOpen;

        /// <summary>preset de sombra leve p/ as ilhas nos perfis Reduzido/Sem animações.</summary>
        private static readonly DropShadowEffect IslandShadowLight = CreateFrozenShadow(12, 2, 0.30);
        private static readonly DropShadowEffect MediaShadowFull = CreateFrozenShadow(20, 3, 0.45);
        private static readonly DropShadowEffect MediaShadowLight = CreateFrozenShadow(10, 2, 0.26);

        private static DropShadowEffect CreateFrozenShadow(double blur, double depth, double opacity)
        {
            var fx = new DropShadowEffect
            {
                Color = Colors.Black,
                BlurRadius = blur,
                ShadowDepth = depth,
                Direction = 270,
                Opacity = opacity,
                RenderingBias = RenderingBias.Performance
            };
            fx.Freeze();
            return fx;
        }

        public MainWindow()
        {
            InitializeComponent();

            // v9.8: brushes VIVOS do tema/acento vivem num dicionário da JANELA (o do Application
            // congela Freezables); o DynamicResource XAML resolve para eles e a mutação propaga.
            Resources.MergedDictionaries.Add(LivePalette.Instance.Dictionary);
            _themeCurrent.CopyFrom(LivePalette.Instance.DefaultTheme);

            _settingsPath = IOPath.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
            _settings = SettingsService.Load(_settingsPath, out _settingsStatus);

            SettingsUI.Initialize(_settings, _settingsPath);
            SettingsUI.Changed += OnSettingsChanged;

            // Fase 4.5 (entrega 1): glow de foco na borda do compositor (fade 120 ms, mesma pegada do hover dos chips)
            CmdInput.GotKeyboardFocus += (_, _) => AnimateChatInputGlow(focused: true);
            CmdInput.LostKeyboardFocus += (_, _) => AnimateChatInputGlow(focused: false);

            ApplyAccent(_settings.AccentHex);
            ApplyEffectsProfile(_settings.EffectsProfile, firstRun: true);
            ApplyVisibilityPrefs();
            ApplyTelemetryInterval();
            ApplyMusicPrefs(_settings);
            ApplyReactorPrefs(_settings);

            _paletteTimer.Tick += PaletteTick;
            _topmostTimer.Tick += (_, _) =>
            {
                if (_settings.Chat.ChatOnTop) DwmCompat.SetTopmost(this, topmost: true);
            };

            if (_settings.General.StartMinimized) WindowState = WindowState.Minimized;

            Loaded += MainWindow_Loaded;
            SourceInitialized += MainWindow_SourceInitialized;
            StateChanged += MainWindow_StateChanged;
            // T3.2: resize => relayout => o Viewbox do backdrop fica stale; re-monta
            // (idempotente, ~zero custo: só recalcula rect e troca o brush).
            SizeChanged += (_, _) => { if (_mediaPanelOpen && _mediaGlassFx is not null) MountMediaGlass(); };

            _statePath = IOPath.Combine(AppDomain.CurrentDomain.BaseDirectory, "state.json");
            try
            {
                _stateWatcher = new FileSystemWatcher(AppDomain.CurrentDomain.BaseDirectory, "state.json")
                { NotifyFilter = NotifyFilters.LastWrite };
                _stateWatcher.Changed += (s, e) => Dispatcher.Invoke(LoadState);
                _stateWatcher.EnableRaisingEvents = true;
            }
            catch { _stateWatcher = null; }

            _stateTimer.Tick += (s, e) =>
            {
                try { if (File.Exists(_statePath) && File.GetLastWriteTimeUtc(_statePath) > _lastStateWrite) LoadState(); }
                catch { }
            };
            _stateTimer.Start();

            // FASE 1: sincronização da letra (250ms — roda com o perfil "Sem animações" também;
            // é dado, não decoração; o scroll suave fica no RenderTick quando MotionEnabled)
            _lyricsTimer.Tick += (_, _) => SyncLyricsTick();

            InitTechFramedApp();
            InitRecedeApp();
            MusicWheelView.Clicked += OpenPlayerFromWheel;
            _spectrum.BandsChanged += OnBandsChanged;
        }

        private void OpenPlayerFromWheel()
        {
            if (_player is { IsLoaded: true }) { _player.Activate(); return; }
            _player = new PlayerWindow(_media); // sem Owner: mesma janela flutuante existente
            _player.Show();
        }

        // Codex P2 (v9.5.0 review): o ReactorHost 460px cobre o miolo da roda e engolia o clique
        // (reator precisa de hit-test pro tilt/zoom) — clique central abre o player igual à roda.
        private void ReactorHost_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => OpenPlayerFromWheel();

        // v9.6: a energia total do spectrum (TotalPeak) viaja junto com as bands —
        // a roda usa esses números para girar e brilhar (pulso/giro/brilho por energia).
        private void OnBandsChanged(float[] bands)
        {
            float energy = _spectrum.TotalPeak;
            Dispatcher.InvokeAsync(() => MusicWheelView.SetBands(bands, energy));
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Fase 4.5 (entrega 2): abertura da janela — fade + scale 300 ms (CubicEase EaseOut)
            AnimateWindowOpen();
            InitClipboardWatcher(); // iter3: KATIE vê o que o sir copia
            ApplyTopmostPref();

            _coreGlowEffect = (DropShadowEffect)Core.Effect;
            BuildReactor();
            _renderTimer.Tick += RenderTick;
            if (MotionEnabled) _renderTimer.Start();

            _clockTimer.Tick += (s, ev) => ClockText.Text = DateTime.Now.ToString("HH:mm:ss");
            _clockTimer.Start();

            _telemetryTimer.Tick += async (s, ev) => await UpdateTelemetryAsync();
            _telemetryTimer.Start();

            _ = StartMediaAsync();
            if (_settings.Music.Wheel) _spectrum.Start();
            StartGameDetection();

            LoadState();
            ApplyMode();
            AppendChat("KATIE", "Holodeck online. Digita aqui no chat — eu leio via command.json.", "#FF00D9FF");
            if (_settingsStatus == SettingsLoadStatus.Corrupted)
                AppendChat("KATIE", "settings.json ilegível — apliquei os padrões; seus ajustes antigos não foram usados.", "#FFFFB454");

            if (!_entranceDone)
            {
                _entranceDone = true;
                AnimateEntrance();
            }
        }

        // ===== Fase 4.5 — abertura da janela (fade + scale, 300 ms) =====
        /// <summary>Fade 0→1 + escala 0.96→1 (CubicEase EaseOut, 300 ms) num Storyboard único.
        /// Gate do perfil de efeitos: "Sem animações" pula; "Reduzido" mantém (leve).</summary>
        private void AnimateWindowOpen()
        {
            if (!MotionEnabled) return;

            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            var dur = TimeSpan.FromMilliseconds(300);
            var sb = new Storyboard();

            var fade = new DoubleAnimation { From = 0, To = 1, Duration = dur, EasingFunction = ease };
            Storyboard.SetTarget(fade, this);
            Storyboard.SetTargetProperty(fade, new PropertyPath(OpacityProperty));
            sb.Children.Add(fade);

            var sx = new DoubleAnimation { From = 0.96, To = 1, Duration = dur, EasingFunction = ease };
            Storyboard.SetTarget(sx, WindowScale);
            Storyboard.SetTargetProperty(sx, new PropertyPath(ScaleTransform.ScaleXProperty));
            sb.Children.Add(sx);

            var sy = (DoubleAnimation)sx.Clone();
            Storyboard.SetTarget(sy, WindowScale);
            Storyboard.SetTargetProperty(sy, new PropertyPath(ScaleTransform.ScaleYProperty));
            sb.Children.Add(sy);

            sb.Completed += (_, _) =>
            {
                BeginAnimation(OpacityProperty, null); // libera o clock (base volta a 1)
                WindowScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                WindowScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            };
            sb.Begin();
        }

        // ================= CHROME WIN11 (Fase 1/2) =================
        private void MainWindow_SourceInitialized(object? sender, EventArgs e)
        {
            DwmCompat.Apply(this);
            DwmCompat.ApplyBackdrop(this, _backdrop);
        }

        private void MainWindow_StateChanged(object? sender, EventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                var resize = SystemParameters.WindowResizeBorderThickness;
                double padded = GetPaddedBorder();
                AppShell.Margin = new Thickness(resize.Left + padded, resize.Top + padded, resize.Right + padded, resize.Bottom + padded);
                AppShell.CornerRadius = new CornerRadius(0);
                MaxBtnIcon.Text = "\uE923"; // ChromeRestore
            }
            else
            {
                AppShell.Margin = new Thickness(0);
                AppShell.CornerRadius = new CornerRadius(8);
                MaxBtnIcon.Text = "\uE922"; // ChromeMaximize
            }
        }

        // ================= RENDER LOOP 60fps =================
        private void RenderTick(object? s, EventArgs e)
        {
            double m = _motionScale;
            if (_settings.Reactor.IdleMotion && !_reactorPaused)
            {
                foreach (var r in _rotors)
                    r.Rot.Angle = (r.Rot.Angle + m * r.DegPerSec / 60.0) % 360;
            }

            // parallax/zoom do reator 3D: easing exponencial (sem física, ~60fps)
            _tiltX += (_tiltTargetX - _tiltX) * 0.10;
            _tiltY += (_tiltTargetY - _tiltY) * 0.10;
            _zoomValue += (_zoomTarget - _zoomValue) * 0.12;
            ApplyReactor3D();

            // ===== pulso do core: SÓ quando "vivo" (hover/foco/falando/pensando) =====
            // idle real e parado: sem heartbeat, sem respiração — o reator responde
            // a input/estado, não fica "respirando" sozinho no standby.
            bool coreAlive = _settings.Reactor.IdleMotion && !_reactorPaused
                             && (_mode == "thinking" || _mode == "speaking" || _talkGlow > 0.01
                                 || _dotHover || _fsMusicOpen);
            if (coreAlive)
            {
                _corePulsePhase += Math.PI * 2 / (2.4 * 60);
                _breath = 0.5 + 0.5 * Math.Sin(_corePulsePhase);

                // ===== REATOR v2 (T1): spin dos meridianos — ScaleX = |cos(λ0+ωt)| (transform-only) =====
                _wireAngle += 0.010;  // rad/frame
                double l30 = _wireAngle + Math.PI / 6, l60 = _wireAngle + Math.PI / 3;
                double m30 = Math.Max(0.02, Math.Abs(Math.Cos(l30)));
                double m60 = Math.Max(0.02, Math.Abs(Math.Cos(l60)));
                foreach (var md in _merids)
                {
                    md.Sx.ScaleX = md.Kind switch
                    {
                        1 => m30,    // Merid30L
                        2 => m60,    // Merid60L
                        _ => -m60,   // Merid60R (espelho vive no sinal do ScaleX)
                    };
                }

                // ===== REATOR v2 (T4): sweep — MESMA TranslateTransform reutilizada (zero alloc/frame) =====
                _sweepT += 0.004;
                if (_sweepT > 1.0) _sweepT = 0;
                if (_sweepXform != null) _sweepXform.X = _sweepT;
            }
            else
            {
                // decai suavemente pro repouso (sem salto seco)
                _breath += (0.0 - _breath) * 0.06;
                _corePulsePhase = 0;
            }
            _talkGlow = Math.Max(0, _talkGlow - 0.045);

            // cache do transform: zero alocação por frame (lesson v4 cinematográfico)
            if (_coreScale == null) { _coreScale = new ScaleTransform(1, 1); Core.RenderTransform = _coreScale; Core.RenderTransformOrigin = new Point(0.5, 0.5); }
            double scale = 1.0 + (0.045 * _breath + 0.10 * _talkGlow) * _glowScale;
            _coreScale.ScaleX = _coreScale.ScaleY = scale;

            if (_coreGlowEffect != null)
            {
                // v8: quantiza (0,5px / 0,02) e só escreve quando muda — escrever BlurRadius a
                // cada frame força re-rasterização do efeito a cada frame sem ganho visível.
                double blur = Math.Round((40 + (30 * _breath + 42 * _talkGlow) * _glowScale) * 2) / 2;
                if (double.IsNaN(_lastGlowBlur) || Math.Abs(blur - _lastGlowBlur) >= 0.5)
                {
                    _coreGlowEffect.BlurRadius = blur;
                    _lastGlowBlur = blur;
                }
                double glowOp = Math.Round((0.65 + (0.2 * _breath + 0.15 * _talkGlow) * _glowScale) * 50) / 50;
                if (double.IsNaN(_lastGlowOpacity) || Math.Abs(glowOp - _lastGlowOpacity) >= 0.02)
                {
                    _coreGlowEffect.Opacity = glowOp;
                    _lastGlowOpacity = glowOp;
                }
            }

            // FASE 1: scroll suave da letra (60fps; no perfil sem animações o tick de 250ms salta direto)
            if (_fsMusicOpen && MotionEnabled && _fsLineBlocks.Count > 0) UpdateFsScroll(1.0 / 60.0);

            // pulso do ponto do logo (+ boost no hover) — quantizado (v8)
            if (BrandDotGlow != null)
            {
                double dotOp = Math.Round(((_dotHover ? 0.8 : 0.5) + 0.25 * _breath) * 50) / 50;
                if (double.IsNaN(_lastDotGlow) || Math.Abs(dotOp - _lastDotGlow) >= 0.02)
                {
                    BrandDotGlow.Opacity = dotOp;
                    _lastDotGlow = dotOp;
                }
            }
        }

        // ===== Reator 3D interativo (v3): tilt segue o mouse (≤8°) + zoom no scroll =====
        private void ReactorHost_MouseMove(object sender, MouseEventArgs e)
        {
            var p = e.GetPosition(ReactorHost);
            double half = ReactorHost.ActualWidth > 0 ? ReactorHost.ActualWidth / 2 : 230;
            double nx = Math.Clamp((p.X - half) / half, -1, 1);
            double ny = Math.Clamp((p.Y - half) / half, -1, 1);
            _tiltTargetY = nx * MaxTiltDeg;   // cursor à direita → a face gira pra direita
            _tiltTargetX = -ny * MaxTiltDeg;  // cursor em cima → topo vem pra frente
            if (!MotionEnabled) { _tiltX = _tiltTargetX; _tiltY = _tiltTargetY; ApplyReactor3D(); }
        }

        private void ReactorHost_MouseLeave(object sender, MouseEventArgs e)
        {
            _tiltTargetX = 0;
            _tiltTargetY = 0;
            if (!MotionEnabled) { _tiltX = 0; _tiltY = 0; ApplyReactor3D(); }
        }

        private void ReactorHost_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            _zoomTarget = Math.Clamp(_zoomTarget + (e.Delta > 0 ? 0.06 : -0.06), ZoomMin, ZoomMax);
            if (!MotionEnabled) { _zoomValue = _zoomTarget; ApplyReactor3D(); }
            e.Handled = true; // zoom do reator não vaza pro resto da UI
        }

        /// <summary>Escreve o estado atual (tilt/zoom) na CÂMERA 3D — chamado a cada frame e em trocas de perfil.
        /// Câmera orbitando o alvo (0,0,0) = parallax correto entre os planos, sem transforms compartilhados.</summary>
        private void ApplyReactor3D()
        {
            double yaw = _tiltY * Math.PI / 180.0;    // cursor à direita → câmera vai pra direita
            double pitch = _tiltX * Math.PI / 180.0;  // cursor em cima → câmera sobe
            double d = 802.0 / _zoomValue;
            var pos = new Point3D(
                d * Math.Sin(yaw) * Math.Cos(pitch),
                d * Math.Sin(pitch),
                d * Math.Cos(yaw) * Math.Cos(pitch));
            ReactorCamera.Position = pos;
            ReactorCamera.LookDirection = new Vector3D(-pos.X, -pos.Y, -pos.Z);
        }

        /// <summary>Reator v2: geometria dos ticks (72, majors a cada 30°) + registro dos rotors.</summary>
        private void BuildReactor()
        {
            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                for (int i = 0; i < 72; i++)
                {
                    double ang = i * 5.0 * Math.PI / 180.0;
                    bool major = i % 6 == 0;
                    double r1 = major ? 164 : 174;
                    double r2 = 188;
                    ctx.BeginFigure(new Point(210 + r1 * Math.Cos(ang), 210 + r1 * Math.Sin(ang)), false, false);
                    ctx.LineTo(new Point(210 + r2 * Math.Cos(ang), 210 + r2 * Math.Sin(ang)), true, false);
                }
            }
            geo.Freeze();
            ReactorTicks.Data = geo;

            AddRotor(ReactorBeads, 208, 208, 3.0);     // coroa de pontos, devagar
            AddRotor(ReactorTicks, 210, 210, -2.0);    // ticks em contra-rotação
            AddRotor(RingOuterDash, 172, 172, 9.0);
            AddRotor(RingMid, 140, 140, -6.0);
            AddRotor(RingInner, 118, 118, 14.0);
            AddRotor(ArcDim, 210, 210, -3.0);
            AddRotor(ArcBright, 210, 210, 6.0);
            AddRotor(AccentArc, 210, 210, 10.0);       // vermelho: órbita lenta e elegante

            // v8: meridianos transform-only — elipse em largura CHEIA centrada no WireFrame
            // (coords locais 0-130) + ScaleX animado; o espelho do 60R vive no sinal do ScaleX.
            AddMerid(Merid30L, 130, kind: 1);
            AddMerid(Merid60L, 65, kind: 2);
            AddMerid(Merid60R, 65, kind: 3);

            // v8: sweep reutiliza UMA TranslateTransform (antes: new por frame no OpacityMask)
            _sweepXform = new TranslateTransform(0, 0);
            SweepMask.Transform = _sweepXform;
        }

        /// <summary>Meridiano v2 (v8): geometria fixa + ScaleX animado no RenderTick —
        /// zero invalidação de layout por frame dentro do Viewport2DVisual3D.</summary>
        private void AddMerid(System.Windows.Shapes.Ellipse e, double fullW, int kind)
        {
            if (e == null) return;
            e.Width = fullW;
            e.SetValue(Canvas.LeftProperty, (130 - fullW) / 2);
            e.RenderTransformOrigin = new Point(0.5, 0.5);
            var sx = new ScaleTransform(kind == 3 ? -1 : 1, 1);
            e.RenderTransform = sx;
            _merids.Add((e, sx, kind));
        }

        private void AddRotor(FrameworkElement el, double cx, double cy, double degPerSec)
        {
            var rot = new RotateTransform(0, cx, cy);
            el.RenderTransform = rot;
            _rotors.Add((rot, degPerSec));
        }

        public void PulseSpeech() => _talkGlow = 1.0;

        // ================= CONFIGURAÇÕES (aplicação viva) =================
        private void OnSettingsChanged(AppSettings s)
        {
            _settings = s;

            // v9.7: acento e TEMA GLOBAL seguem a capa de forma independente.
            // coverAccent ON com faixa ativa: a capa manda no acento;
            // coverTheme ON com faixa ativa: a capa manda em TODOS os tokens de superfície.
            // OFF/sem faixa: volta ao AccentHex / tema ciano padrão.
            bool hasCover = _coverAccentColor is Color && _coverAccentKey.Length > 0;
            Color accentTarget;
            if (s.Music.CoverAccent && hasCover)
            {
                _coverAccentApplied = true;
                accentTarget = _coverAccentColor!.Value;
            }
            else
            {
                _coverAccentApplied = false;
                accentTarget = SettingsAccentColor();
            }
            Color? themeTarget = s.Music.CoverTheme && hasCover ? _coverThemeColor : null;
            TransitionAccentTo(accentTarget, themeTarget);

            ApplyTopmostPref();
            ApplyEffectsProfile(s.EffectsProfile, firstRun: false);
            ApplyVisibilityPrefs();
            ApplyTelemetryInterval();
            ApplyMusicPrefs(s);
            ApplyReactorPrefs(s);

            if (s.GameDetectionEnabled) StartGameDetection();
            else StopGameDetection();

            // review Codex #2: CoverAll trocado com o fullscreen de música aberto —
            // re-sincroniza camadas e blur na hora (senão ficam estados mistos)
            if (_fsMusicOpen)
            {
                SetFsBackdropBlur(false);
                SetFsBackdropBlur(true);
            }

            if (!SettingsService.Save(_settingsPath, s))
                StatusText.Text = "não consegui salvar settings.json (permissão?) — os ajustes valem nesta sessão";
        }

        /// <summary>FASE 4: preferências da roda/espectro — aplicadas na hora, persistidas pelas settings.</summary>
        private void ApplyMusicPrefs(AppSettings s)
        {
            _spectrum.Sensitivity = Math.Clamp(s.Music.Sensitivity, 1.0, 20.0);
            _spectrum.CalculationType = s.Music.CalculationType;
            MusicWheelView.SetEnabled(s.Music.Wheel);
            MusicWheelView.SetRingsVisible(s.Music.Rings);
            bool audioLive = _spectrum.HasAudio || _mediaPlaying;
            MusicWheelView.SetPlaying(audioLive && s.Music.Wheel);

            if (!IsLoaded) return;
            if (s.Music.Wheel) _spectrum.Start();
            else _spectrum.Stop();
        }

        /// <summary>Movimento autônomo do reator: o RenderTick lê a flag direto; ao desligar,
        /// congela o núcleo em repouso na hora (anéis param no ângulo atual).</summary>
        private void ApplyReactorPrefs(AppSettings s)
        {
            if (!s.Reactor.IdleMotion)
            {
                _breath = 0;
                _corePulsePhase = 0;
            }
        }

        /// <summary>F2.5: pausa o reator enquanto o fullscreen (Z50) cobre o AppContent borrado.
        /// Reusa EXATAMENTE o repouso do IdleMotion-off (anéis param no ângulo atual, core
        /// volta a 1.0 e heartbeat zerado) — nenhum mecanismo novo de compositor.</summary>
        private void SetReactorPaused(bool paused)
        {
            if (_reactorPaused == paused) return;
            _reactorPaused = paused;
            if (paused)
            {
                _breath = 0;
                _corePulsePhase = 0;
            }
        }

        /// <summary>Troca o acento em runtime: reescreve o grupo de acento da paleta viva.</summary>
        private void ApplyAccent(string hex)
        {
            _paletteTimer.Stop();
            if (!SettingsService.TryParseAccent(hex, out var c)) c = Color.FromRgb(0x00, 0xD9, 0xFF);
            SetAccentColors(Color.FromRgb(c.R, c.G, c.B));
        }

        /// <summary>v9.8: aplica SÓ o grupo do acento (brushes vivos, mutação in-place).
        /// review P1 churn: nada de brush/gradiente novo por chamada — o timer de 16 ms reusa
        /// os mesmos objetos. O tema da capa é tratado à parte (TransitionAccentTo).</summary>
        private void SetAccentColors(Color c, bool deferWheel = false)
        {
            _paletteCurrent = c;
            var bright = BrightenAccent(c);
            LivePalette.Instance.SetAccent(c, bright);

            // review P1 churn: a roda reconstrói 26 pens no OnRender; só vale a pena quando a
            // cor muda de verdade na quantização (delta >= 6/255 por canal).
            // review P1 churn (revalida): durante a transição (deferWheel) o acento da roda fica
            // congelado — aplicado 1x no tick final, sem recriar pens a cada 16 ms.
            if (!deferWheel &&
                (_wheelAccent is not Color prev ||
                Math.Abs(prev.R - bright.R) >= 6 || Math.Abs(prev.G - bright.G) >= 6 || Math.Abs(prev.B - bright.B) >= 6))
            {
                _wheelAccent = bright;
                MusicWheelView.SetAccent(bright);
            }
        }

        // ================= v9.8: TEMA GLOBAL DA CAPA =================

        /// <summary>Deriva os tokens de superfície da cor dominante da capa em OBJETOS reusáveis
        /// (review P1 churn): nada de brush/gradiente/Pen/GeometryDrawing novo — só Color structs.
        /// review P1 contraste: o texto não usa mais lightness HSL fixa; claro/escuro é escolhido
        /// pelo contraste WCAG medido contra as superfícies COMPOSTAS (Over) onde ele aparece.</summary>
        private static void BuildCoverTheme(Color c, ThemeTokens t)
        {
            var (h, s, l) = ToHsl(c);
            // Capa acinzentada: matiz indefinido — mantém a identidade ciano do app.
            if (s < 0.08) { h = 199.0; s = 0.55; }
            double sat = Math.Clamp(s, 0.25, 0.90);

            // Secundária: variação de matiz complementar, sempre legível.
            t.Secondary = EnsureReadableAccent(FromHsl(
                h + 145.0,
                Math.Clamp(sat + 0.15, 0.45, 0.95),
                Math.Clamp(Math.Max(l, 0.50), 0.50, 0.64)));

            t.GridLine = FromHsl(h, sat * 0.70, 0.20);
            t.ScanLine = FromHsl(h, sat * 0.70, 0.17);
            t.Shell0 = FromHsl(h, sat * 0.75, 0.20);
            t.Shell1 = FromHsl(h, sat * 0.80, 0.14);
            t.Shell2 = FromHsl(h, sat * 0.85, 0.09);
            t.VignetteEdge = FromHsl(h, sat * 0.80, 0.05);
            t.MediaGlass = FromHsl(h, sat * 0.70, 0.20);
            t.FieldGlass = FromHsl(h, sat * 0.70, 0.22);
            t.BarTrack = t.Secondary;
            t.CoverPlaceholder = FromHsl(h, sat * 0.45, 0.35);
            t.ChipGlass = FromHsl(h, sat * 0.65, 0.10);
            t.ChatExpanded = FromHsl(h, sat * 0.65, 0.15);
            t.PanelSolid = FromHsl(h, sat * 0.60, 0.035);
            t.Island0 = FromHsl(h, sat * 0.70, 0.18);
            t.Island1 = FromHsl(h, sat * 0.72, 0.15);
            t.PanelStrong0 = FromHsl(h, sat * 0.70, 0.15);
            t.PanelStrong1 = FromHsl(h, sat * 0.72, 0.13);
            t.Scrim = FromHsl(h, sat * 0.60, 0.05);
            t.Fullscreen = FromHsl(h, sat * 0.70, 0.07);
            t.FullscreenOpaque = t.Fullscreen;

            // Superfícies efetivamente renderizadas (compostas sobre o painel sólido) — o texto
            // precisa passar o contraste em TODAS elas, não só na mais escura.
            var solid = Opaque(t.PanelSolid);
            var shell = Over(WithA(t.Shell1, 0xD8), solid);
            var islandTop = Over(WithA(t.Island0, 0x66), shell);
            var islandBottom = Over(WithA(t.Island1, 0x4D), shell);
            var media = Over(WithA(t.MediaGlass, 0x99), shell);
            var chip = Over(WithA(t.ChipGlass, 0x22), islandTop);
            var field = Over(WithA(t.FieldGlass, 0x17), shell);
            var panelGlass = Over(WithA(t.PanelStrong0, 0xF2), solid);
            var fullscreen = Over(WithA(t.Fullscreen, 0xCC), solid);
            // v9.8.2: Color NÃO é unmanaged (carrega ColorContext) — stackalloc Color explodia em
            // runtime com ArgumentException dentro do UpdateCoverAccent, matando a cadeia inteira
            // de música a cada update com capa. Array simples: roda 1x por troca de faixa, não por frame.
            ReadOnlySpan<Color> surfaces = new[]
                { solid, shell, islandTop, islandBottom, media, chip, field, panelGlass, fullscreen };

            // Papéis de texto: lightness vira só o PONTO DE PARTIDA; a decisão claro/escuro é
            // tomada pelo contraste medido (WCAG: strong 7:1, corpo/label 4.5:1, dim 3:1).
            t.TextStrong = PickTextColor(h, 0.10 + sat * 0.20, 0.94, 0.10 + sat * 0.20, 0.10, 7.0, surfaces);
            t.Text = PickTextColor(h, 0.12 + sat * 0.24, 0.80, 0.12 + sat * 0.24, 0.20, 4.5, surfaces);
            t.Label = PickTextColor(h, 0.16 + sat * 0.28, 0.66, 0.16 + sat * 0.28, 0.30, 4.5, surfaces);
            t.TextDim = PickTextColor(h, 0.20 + sat * 0.30, 0.46, 0.20 + sat * 0.30, 0.40, 3.0, surfaces);
        }

        /// <summary>Escolhe claro ou escuro pelo MAIOR contraste medido; se nenhum atinge o alvo,
        /// empurra a cor para branco/preto até atingir (sem inverter o sentido do papel).</summary>
        private static Color PickTextColor(double h, double lightSat, double lightL,
            double darkSat, double darkL, double target, ReadOnlySpan<Color> surfaces)
        {
            var light = FromHsl(h, lightSat, lightL);
            var dark = FromHsl(h, darkSat, darkL);
            double lc = MinContrast(light, surfaces);
            double dc = MinContrast(dark, surfaces);
            if (lc >= target && lc >= dc) return light;
            if (dc >= target && dc >= lc) return dark;
            return PushContrast(lc >= dc ? light : dark, surfaces, target);
        }

        /// <summary>Menor razão de contraste (WCAG 2.x) da cor de frente contra todas as superfícies.</summary>
        private static double MinContrast(Color fg, ReadOnlySpan<Color> surfaces)
        {
            double lf = RelativeLuminance(fg);
            double min = double.MaxValue;
            foreach (var bg in surfaces)
            {
                double lb = RelativeLuminance(bg);
                double ratio = lf > lb ? (lf + 0.05) / (lb + 0.05) : (lb + 0.05) / (lf + 0.05);
                if (ratio < min) min = ratio;
            }
            return min;
        }

        /// <summary>Empurra a cor na direção (branco/preto) que mais sobe o contraste, por bisseção.</summary>
        private static Color PushContrast(Color c, ReadOnlySpan<Color> surfaces, double target)
        {
            var goal = MinContrast(Colors.White, surfaces) >= MinContrast(Colors.Black, surfaces)
                ? Colors.White
                : Colors.Black;
            double lo = 0, hi = 1;
            for (int i = 0; i < 12; i++)
            {
                double mid = (lo + hi) * 0.5;
                if (MinContrast(Blend(c, goal, mid), surfaces) < target) lo = mid;
                else hi = mid;
            }
            return Blend(c, goal, hi);
        }

        private static Color Blend(Color a, Color b, double t) => Color.FromRgb(
            (byte)(a.R + (b.R - a.R) * t),
            (byte)(a.G + (b.G - a.G) * t),
            (byte)(a.B + (b.B - a.B) * t));

        /// <summary>Composição src-over (alpha do topo sobre base opaca).</summary>
        private static Color Over(Color src, Color dst)
        {
            double a = src.A / 255.0;
            return Color.FromRgb(
                (byte)Math.Round(src.R * a + dst.R * (1 - a)),
                (byte)Math.Round(src.G * a + dst.G * (1 - a)),
                (byte)Math.Round(src.B * a + dst.B * (1 - a)));
        }

        private static Color Opaque(Color c) => Color.FromArgb(0xFF, c.R, c.G, c.B);

        private static Color WithA(Color c, byte a) => Color.FromArgb(a, c.R, c.G, c.B);

        private static (double H, double S, double L) ToHsl(Color c)
        {
            double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            double l = (max + min) * 0.5;
            double h = 0, s = 0, d = max - min;
            if (d > 0.00001)
            {
                s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
                if (max == r) h = (g - b) / d + (g < b ? 6 : 0);
                else if (max == g) h = (b - r) / d + 2;
                else h = (r - g) / d + 4;
                h *= 60;
            }
            return (h, s, l);
        }

        private static Color FromHsl(double h, double s, double l, byte a = 255)
        {
            h = ((h % 360.0) + 360.0) % 360.0;
            s = Math.Clamp(s, 0, 1);
            l = Math.Clamp(l, 0, 1);
            double c = (1 - Math.Abs(2 * l - 1)) * s;
            double x = c * (1 - Math.Abs((h / 60.0) % 2 - 1));
            double m = l - c * 0.5;
            double r, g, b;
            if (h < 60) { r = c; g = x; b = 0; }
            else if (h < 120) { r = x; g = c; b = 0; }
            else if (h < 180) { r = 0; g = c; b = x; }
            else if (h < 240) { r = 0; g = x; b = c; }
            else if (h < 300) { r = x; g = 0; b = c; }
            else { r = c; g = 0; b = x; }
            return Color.FromArgb(a,
                (byte)Math.Round(Math.Clamp(r + m, 0, 1) * 255),
                (byte)Math.Round(Math.Clamp(g + m, 0, 1) * 255),
                (byte)Math.Round(Math.Clamp(b + m, 0, 1) * 255));
        }

        /// <summary>Transição ~600 ms nos brushes VIVOS (review P1 churn): as duas pontas do acento
        /// e do tema ficam em Color structs reusáveis e cada tick só reescreve Color/GradientStop.Color
        /// dos MESMOS objetos — nenhum Freezable novo por frame.
        /// v9.8 (review P2): o tema padrão também faz crossfade a partir do estado atual, tanto na
        /// primeira aplicação quanto ao voltar da capa — não pula mais.</summary>
        private void TransitionAccentTo(Color target, Color? themeTarget = null)
        {
            target = Color.FromRgb(target.R, target.G, target.B);
            Color? theme = themeTarget is Color tt ? Color.FromRgb(tt.R, tt.G, tt.B) : null;

            bool accentSame = target == _paletteCurrent;
            bool themeSame = theme is Color tc ? _themeAppliedBase == tc : _themeAppliedBase == null;
            if (accentSame && themeSame) return;

            _paletteFrom = _paletteCurrent;
            _paletteTo = target;

            if (!themeSame)
            {
                _themeAppliedBase = theme;
                _themeFrom.CopyFrom(_themeCurrent);
                if (theme is Color next) BuildCoverTheme(next, _themeTo);
                else _themeTo.CopyFrom(LivePalette.Instance.DefaultTheme);
                _themeAnimating = true;
            }
            else if (_themeAnimating)
            {
                // review P2 (revalida): só o acento mudou no meio de uma transição de tema —
                // reancorar a origem no estado visual ATUAL, senão o próximo tick pula o tema
                // de volta em direção à origem antiga
                _themeFrom.CopyFrom(_themeCurrent);
            }

            _paletteStartAt = DateTime.UtcNow;
            if (!_paletteTimer.IsEnabled) _paletteTimer.Start();
        }

        private void PaletteTick(object? sender, EventArgs e)
        {
            double t = Math.Clamp((DateTime.UtcNow - _paletteStartAt).TotalMilliseconds / PaletteTransitionMs, 0, 1);
            double k = t * t * (3 - 2 * t);

            SetAccentColors(LerpColor(_paletteFrom, _paletteTo, k), deferWheel: t < 1.0);
            if (_themeAnimating)
            {
                _themeScratch.Lerp(_themeFrom, _themeTo, k);
                LivePalette.Instance.ApplyTheme(_themeScratch);
                _themeCurrent.CopyFrom(_themeScratch);
            }

            if (t >= 1)
            {
                _paletteTimer.Stop();
                SetAccentColors(_paletteTo);
                if (_themeAnimating)
                {
                    LivePalette.Instance.ApplyTheme(_themeTo);
                    _themeCurrent.CopyFrom(_themeTo);
                    _themeAnimating = false;
                }
            }
        }

        private static Color LerpColor(Color a, Color b, double k) => Color.FromArgb(
            (byte)(a.A + (b.A - a.A) * k),
            (byte)(a.R + (b.R - a.R) * k),
            (byte)(a.G + (b.G - a.G) * k),
            (byte)(a.B + (b.B - a.B) * k));

        private Color SettingsAccentColor() =>
            SettingsService.TryParseAccent(_settings.AccentHex, out var c)
                ? c
                : Color.FromRgb(0x00, 0xD9, 0xFF);

        // ===== chatOnTop =====
        /// <summary>ON: reafirma HWND_TOPMOST no load e a cada ~2 s. OFF: devolve ao normal (sem timer).</summary>
        private void ApplyTopmostPref()
        {
            if (_settings.Chat.ChatOnTop)
            {
                DwmCompat.SetTopmost(this, topmost: true);
                _topmostApplied = true;
                if (!_topmostTimer.IsEnabled) _topmostTimer.Start();
            }
            else
            {
                if (_topmostTimer.IsEnabled) _topmostTimer.Stop();
                if (_topmostApplied)
                {
                    DwmCompat.SetTopmost(this, topmost: false);
                    _topmostApplied = false;
                }
            }
        }

        // ===== coverAccent =====
        /// <summary>Extrai 1 cor dominante da capa (mesma técnica do Translucid: 16px + média RGB).</summary>
        private static Color? ExtractDominantColor(byte[] bytes)
        {
            try
            {
                using var stream = new MemoryStream(bytes);
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.DecodePixelWidth = 16;
                image.StreamSource = stream;
                image.EndInit();
                image.Freeze();

                // Codex P2: CopyPixels usa o formato NATIVO do decoder (BGRA/BGR/Pbgra variam) —
                // converter explícito pra Bgra32 antes de ler (bytes = B,G,R,A) e indexar na ordem certa.
                var conv = new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0);
                conv.Freeze();

                int w = conv.PixelWidth, h = conv.PixelHeight;
                if (w <= 0 || h <= 0) return null;

                var pixels = new byte[w * h * 4];
                conv.CopyPixels(pixels, w * 4, 0);

                // Codex P2: cor DOMINANTE de verdade — histograma grosso (4 bits/canal) ponderado
                // pelo alpha. A média é feita só dentro do bucket vencedor: sem isso, uma média
                // global mistura todas as cores da capa e vira "lodo" acinzentado; e sem o peso de
                // alpha, pixels transparentes (borda/sem arte) puxam a cor para o preto.
                var buckets = new int[16 * 16 * 16];
                for (int i = 0; i < pixels.Length; i += 4)
                {
                    int a = pixels[i + 3];
                    if (a == 0) continue; // transparente: não representa nada na capa
                    buckets[BucketIndex(pixels, i)] += a;
                }

                int best = 0;
                for (int i = 1; i < buckets.Length; i++)
                    if (buckets[i] > buckets[best]) best = i;
                if (buckets[best] == 0) return null;

                double r = 0, g = 0, b = 0, weight = 0;
                for (int i = 0; i < pixels.Length; i += 4)
                {
                    int a = pixels[i + 3];
                    if (a == 0 || BucketIndex(pixels, i) != best) continue;
                    double wa = a / 255.0;
                    b += pixels[i] * wa;
                    g += pixels[i + 1] * wa;
                    r += pixels[i + 2] * wa;
                    weight += wa;
                }
                if (weight <= 0.0001) return null;
                return Color.FromRgb(
                    (byte)Math.Min(255, Math.Round(r / weight)),
                    (byte)Math.Min(255, Math.Round(g / weight)),
                    (byte)Math.Min(255, Math.Round(b / weight)));
            }
            catch { return null; }
        }

        /// <summary>Bucket 4 bits/canal (4096 caixas) de um pixel Bgra32 — índice = 16*16*16.</summary>
        private static int BucketIndex(byte[] bgra, int i) =>
            ((bgra[i] >> 4) << 8) | ((bgra[i + 1] >> 4) << 4) | (bgra[i + 2] >> 4);

        /// <summary>FNV-1a do thumbnail (chave de cache da cor da capa, Codex P3).</summary>
        private static int ThumbnailHash(byte[]? bytes)
        {
            if (bytes == null) return 0;
            uint h = 2166136261;
            for (int i = 0; i < bytes.Length; i++) h = (h ^ bytes[i]) * 16777619;
            return (int)h;
        }

        /// <summary>Luminância relativa WCAG — garante acento legível (texto/realces) sobre o fundo escuro.</summary>
        private static double RelativeLuminance(Color c) =>
            0.2126 * ToLinear(c.R) + 0.7152 * ToLinear(c.G) + 0.0722 * ToLinear(c.B);

        private static double ToLinear(byte v)
        {
            double s = v / 255.0;
            return s <= 0.04045 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }

        private static Color BlendWhite(Color c, double t) => Color.FromRgb(
            (byte)(c.R + (255 - c.R) * t),
            (byte)(c.G + (255 - c.G) * t),
            (byte)(c.B + (255 - c.B) * t));

        private static Color EnsureReadableAccent(Color c)
        {
            var opaque = Color.FromRgb(c.R, c.G, c.B);
            if (RelativeLuminance(opaque) >= MinAccentLuminance) return opaque;

            double lo = 0, hi = 1;
            for (int i = 0; i < 14; i++)
            {
                double mid = (lo + hi) * 0.5;
                if (RelativeLuminance(BlendWhite(opaque, mid)) < MinAccentLuminance) lo = mid;
                else hi = mid;
            }
            return BlendWhite(opaque, hi);
        }

        /// <summary>Aplica no máximo UMA vez por faixa. O acento e o TEMA GLOBAL têm toggles
        /// independentes: a mesma extração alimenta os dois. OFF/sem música → AccentHex + tema padrão.</summary>
        private void UpdateCoverAccent(MediaUpdate u)
        {
            bool accentOn = _settings.Music.CoverAccent;
            bool themeOn = _settings.Music.CoverTheme;

            if (!accentOn && !themeOn)
            {
                if (_coverAccentApplied || _themeAppliedBase != null)
                {
                    _coverAccentApplied = false;
                    TransitionAccentTo(SettingsAccentColor());
                }
                // sem toggles: descarta o cache para não aplicar capa antiga ao religar
                _coverAccentKey = "";
                _coverThumbHash = 0;
                _coverAccentColor = null;
                _coverThemeColor = null;
                return;
            }

            bool hasTrack = !string.IsNullOrWhiteSpace(u.Title)
                            && !string.Equals(u.Title, "Nada tocando", StringComparison.Ordinal)
                            && u.Thumbnail is { Length: > 0 };

            if (!hasTrack)
            {
                bool wasActive = _coverAccentApplied || _themeAppliedBase != null;
                _coverAccentKey = "";
                _coverThumbHash = 0;
                _coverAccentColor = null;
                _coverThemeColor = null;
                _coverAccentApplied = false;
                if (wasActive) TransitionAccentTo(SettingsAccentColor());
                return;
            }

            // Codex P3: a capa faz parte da chave — mesmo título/artista com thumbnail nova
            // (upload tardio, troca de álbum) recalcula a cor em vez de reusar a antiga.
            var key = $"{u.Title}|{u.Artist}";
            int thumbHash = ThumbnailHash(u.Thumbnail);
            if (key == _coverAccentKey && thumbHash == _coverThumbHash)
            {
                if (!_coverAccentApplied && _coverAccentColor is Color cached)
                {
                    _coverAccentApplied = true;
                    TransitionAccentTo(
                        accentOn ? cached : SettingsAccentColor(),
                        themeOn ? (_coverThemeColor ?? cached) : (Color?)null);
                }
                return;
            }

            _coverAccentKey = key;
            _coverThumbHash = thumbHash;
            if (ExtractDominantColor(u.Thumbnail!) is Color dominant)
            {
                var readable = EnsureReadableAccent(dominant);
                _coverAccentColor = readable;
                _coverThemeColor = readable;
                _coverAccentApplied = true;
                TransitionAccentTo(
                    accentOn ? readable : SettingsAccentColor(),
                    themeOn ? readable : (Color?)null);
            }
            else
            {
                bool wasActive = _coverAccentApplied || _themeAppliedBase != null;
                _coverAccentColor = null;
                _coverThemeColor = null;
                _coverAccentApplied = false;
                if (wasActive) TransitionAccentTo(SettingsAccentColor());
            }
        }

        private static Color BrightenAccent(Color c) => Color.FromArgb(
            c.A,
            (byte)Math.Min(255, c.R * 1.04),
            (byte)Math.Min(255, c.G * 1.04),
            (byte)Math.Min(255, c.B * 1.04));

        /// <summary>Perfis Completo / Reduzido / Sem animações — sombras, glow e loop do radar.</summary>
        private void ApplyEffectsProfile(string profile, bool firstRun)
        {
            _motionScale = profile == "none" ? 0.0 : profile == "reduced" ? 0.5 : 1.0;
            _glowScale = profile == "full" ? 1.0 : 0.5;
            MusicWheelView.MotionScale = _motionScale; // v9.6: "Sem animações" = roda sem pulso/giro

            foreach (var island in new FrameworkElement[] { IslandTelemetry, IslandChat })
            {
                if (profile == "full") island.ClearValue(EffectProperty); // volta ao valor do estilo
                else island.Effect = IslandShadowLight;
            }
            MediaPanel.Effect = profile == "full" ? MediaShadowFull : MediaShadowLight;

            // F1: modo efetivo do vidro (Off/Vector/Shader) — OptNone força Vector.
            // Lido do _settings vigente: OnSettingsChanged troca a instância antes daqui.
            Glass.Apply(_settings.EnableGlass, profile);

            if (!firstRun && IsLoaded)
            {
                if (MotionEnabled && !_renderTimer.IsEnabled) _renderTimer.Start();
                else if (!MotionEnabled) _renderTimer.Stop();
            }
            if (!MotionEnabled)
            {
                // deixa o núcleo estático e legível
                Core.RenderTransform = new ScaleTransform(1, 1);
                Core.RenderTransformOrigin = new Point(0.5, 0.5);
                if (_coreGlowEffect != null) { _coreGlowEffect.BlurRadius = 44; _coreGlowEffect.Opacity = 0.75; }
                BrandDotGlow.Opacity = 0.6;

                // reator 3D: sem loop → sem tilt pendurado (volta ao frontal, zoom 1x)
                _tiltX = _tiltY = _tiltTargetX = _tiltTargetY = 0;
                _zoomValue = _zoomTarget = 1.0;
                ApplyReactor3D();
            }
        }

        private void ApplyVisibilityPrefs()
        {
            GpuRow.Visibility = _settings.Telemetry.ShowGpu ? Visibility.Visible : Visibility.Collapsed;
            NetRow.Visibility = _settings.Telemetry.ShowNet ? Visibility.Visible : Visibility.Collapsed;
            UndockBtn.Visibility = _settings.Music.ShowUndockButton ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ApplyTelemetryInterval()
        {
            int secs = Math.Clamp(_settings.Telemetry.IntervalSeconds, 1, 30);
            _telemetryTimer.Interval = TimeSpan.FromSeconds(secs);
        }

        // ===== entrada das ilhas (Fase "polimento": subida + fade, escalonada) =====
        private void AnimateEntrance()
        {
            if (!MotionEnabled) return;
            AnimateIsland(IslandTelemetry, 0, rise: true);
            AnimateIsland(IslandChat, 90, rise: true);
            // v7: chips da bottom bar sobem com fadeIn em cascata (depois das ilhas)
            AnimateChip(ChipAgenda, 260);
            AnimateChip(ChipEmails, 320);
            AnimateChip(ChipMusic, 380);
        }

        private void AnimateIsland(FrameworkElement el, int delayMs, bool rise)
        {
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            var dur = TimeSpan.FromMilliseconds(300);
            var fade = new DoubleAnimation { From = 0, To = 1, Duration = dur, BeginTime = TimeSpan.FromMilliseconds(delayMs), EasingFunction = ease };
            fade.Completed += (_, _) => el.BeginAnimation(OpacityProperty, null);
            el.BeginAnimation(OpacityProperty, fade);

            if (!rise) return;
            var t = new TranslateTransform(0, 16);
            el.RenderTransform = t;
            var slide = new DoubleAnimation { From = 16, To = 0, Duration = dur, BeginTime = TimeSpan.FromMilliseconds(delayMs), EasingFunction = ease };
            slide.Completed += (_, _) => { t.BeginAnimation(TranslateTransform.YProperty, null); el.RenderTransform = null; };
            t.BeginAnimation(TranslateTransform.YProperty, slide);
        }

        /// <summary>v7: fadeIn suave de um chip — 260ms, pequena subida de 6px, sem salto.
        /// NÃO zera o RenderTransform no fim (o EventTrigger de press do ChipButtonBaseStyle
        /// anima (RenderTransform).(ScaleTransform) — transform null = InvalidOperationException = CRASH).
        /// Deixa o TranslateTransform finalizado em Y=0 (inócuo).</summary>
        private void AnimateChip(FrameworkElement el, int delayMs)
        {
            if (el == null) return;
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            var dur = TimeSpan.FromMilliseconds(260);
            var fade = new DoubleAnimation { From = 0, To = 1, Duration = dur, BeginTime = TimeSpan.FromMilliseconds(delayMs), EasingFunction = ease };
            fade.Completed += (_, _) => el.BeginAnimation(OpacityProperty, null);
            el.BeginAnimation(OpacityProperty, fade);
            var t = new TranslateTransform(0, 6);
            el.RenderTransform = t;
            var slide = new DoubleAnimation { From = 6, To = 0, Duration = dur, BeginTime = TimeSpan.FromMilliseconds(delayMs), EasingFunction = ease };
            slide.Completed += (_, _) => { t.BeginAnimation(TranslateTransform.YProperty, null); }; // transform PERMANECE
            t.BeginAnimation(TranslateTransform.YProperty, slide);
        }

        // ===== painel de Configurações (flutuante, com scrim) =====
        private void SettingsBtn_Click(object sender, RoutedEventArgs e) => OpenSettings();

        private void SettingsBtn_MouseEnter(object sender, MouseEventArgs e) => _dotHover = true;

        private void SettingsBtn_MouseLeave(object sender, MouseEventArgs e) => _dotHover = false;

        private void SettingsScrim_Click(object sender, MouseButtonEventArgs e) => CloseSettings();

        private void OpenSettings()
        {
            if (_settingsOpen) return;
            _settingsOpen = true;
            SettingsHost.Visibility = Visibility.Visible;
            TechFrame.Refresh(SettingsUI.Card);
            SetSubtreeAdornersVisible(SettingsUI, true); // v9.3.6: restaura o chanfro colapsado no fechamento
            SetBackdropBlur(10);
            if (!MotionEnabled)
            {
                SettingsScrim.Opacity = 1;
                SettingsUI.Opacity = 1;
                SettingsScale.ScaleX = SettingsScale.ScaleY = 1;
                SettingsUI.FocusFirst();
                return;
            }
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            SettingsScrim.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(180)) { EasingFunction = ease });
            SettingsUI.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(180)) { EasingFunction = ease });
            // v9.2: sem escala na entrada — a moldura (Chamfer/TechFrame, Adorners) não
            // acompanha RenderTransform e ficava deslocada ("borda órfã"). Fade apenas.
            SettingsScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            SettingsScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            SettingsScale.ScaleX = SettingsScale.ScaleY = 1;
            SettingsUI.FocusFirst();
        }

        private void CloseSettings()
        {
            if (!_settingsOpen) return;
            _settingsOpen = false;
            TechFrame.Clear(SettingsUI.Card);
            // v9.3.6: chanfro (ChamferAdorner) do painel some JUNTO com os brackets —
            // antes ele não herdava o Opacity do fade e ficava órfão na tela.
            SetSubtreeAdornersVisible(SettingsUI, false);

            if (!MotionEnabled)
            {
                SettingsHost.Visibility = Visibility.Collapsed;
                // v9.3-fix (Codex P2): blur composto — fullscreen pode seguir aberto
                if (_fsMusicOpen) SetFsBackdropBlur(true); else SetBackdropBlur(0);
                SettingsScrim.BeginAnimation(OpacityProperty, null); SettingsScrim.Opacity = 0;
                SettingsUI.BeginAnimation(OpacityProperty, null); SettingsUI.Opacity = 0;
                SettingsScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                SettingsScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                SettingsScale.ScaleX = SettingsScale.ScaleY = 1;
                SettingsBtn.Focus();
                return;
            }

            var ease = new CubicEase { EasingMode = EasingMode.EaseIn };
            var dur = TimeSpan.FromMilliseconds(150);
            var fadeOut = new DoubleAnimation(0, dur) { EasingFunction = ease };
            fadeOut.Completed += (_, _) =>
            {
                SettingsHost.Visibility = Visibility.Collapsed;
                // v9.3-fix (Codex P2): blur composto — fullscreen pode seguir aberto
                if (_fsMusicOpen) SetFsBackdropBlur(true); else SetBackdropBlur(0);
                SettingsScrim.BeginAnimation(OpacityProperty, null); SettingsScrim.Opacity = 0;
                SettingsUI.BeginAnimation(OpacityProperty, null); SettingsUI.Opacity = 0;
                SettingsScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                SettingsScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                SettingsScale.ScaleX = SettingsScale.ScaleY = 1;
                SettingsBtn.Focus();
            };
            SettingsScrim.BeginAnimation(OpacityProperty, new DoubleAnimation(0, dur) { EasingFunction = ease });
            SettingsUI.BeginAnimation(OpacityProperty, fadeOut);
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape) return;
            if (_settingsOpen) { CloseSettings(); e.Handled = true; }
            else if (_chatExpanded) { SetChatExpanded(false, instant: !MotionEnabled); e.Handled = true; }
            else if (_fsMusicOpen) { ExitFullscreenMusic(); e.Handled = true; }
            else if (_mediaPanelOpen) { SetMediaPanel(false, InstantMedia); e.Handled = true; }
        }

        // ================= CHAT =================
        // ================= v7 FASE E: TYPEWRITER (streaming visual do reply) =================
        private readonly System.Windows.Threading.DispatcherTimer _typeTimer =
            new() { Interval = TimeSpan.FromMilliseconds(30) };
        private string? _typeFull;      // texto completo pendente
        private TextBlock? _typeTarget; // TextBlock que está sendo escrito
        private int _typePos;

        /// <summary>Enfileira uma resposta pra "digitar" na tela (efeito máquina de escrever).
        /// MotionEnabled=false → cai pro AppendChat instantâneo.</summary>
        private void AppendChatTypewriter(string who, string text, string colorHex)
        {
            if (!MotionEnabled) { AppendChat(who, text, colorHex); return; }
            _typeFull = text;
            _typePos = 0;
            // cria a bolha agora (vazia) e escreve dentro dela
            AppendChat(who, "", colorHex);
            if (ChatPanel.Children.Count > 0 && ChatPanel.Children[^1] is StackPanel sp &&
                sp.Children.Count > 1 && sp.Children[^1] is TextBlock tb)
            {
                _typeTarget = tb;
                _typeTimer.Tick -= TypeTimer_Tick;
                _typeTimer.Tick += TypeTimer_Tick;
                _typeTimer.Start();
            }
        }

        private void TypeTimer_Tick(object? sender, EventArgs e)
        {
            if (_typeFull == null || _typeTarget == null) { _typeTimer.Stop(); return; }
            // 2 chars por tick (30ms) = ~66 chars/s — legível e rápido
            _typePos = Math.Min(_typePos + 2, _typeFull.Length);
            _typeTarget.Text = _typeFull[.._typePos];
            if (_typePos >= _typeFull.Length)
            {
                _typeTimer.Stop();
                _typeFull = null;
                _typeTarget = null;
                ChatScroll.ScrollToEnd();
            }
        }

        private void AppendChat(string who, string text, string colorHex)
        {
            var sp = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
            var label = new TextBlock
            {
                Text = who == "KATIE" ? "KATIE \u25B8" : "VOC\u00CA \u25B8",
                FontFamily = new FontFamily("Consolas"),
                FontSize = 9,
                Foreground = new SolidColorBrush(Color.FromRgb(0x40, 0x54, 0x6E)),
                Margin = new Thickness(0, 0, 0, 2)
            };
            var tb = new TextBlock
            {
                Text = text,
                FontFamily = new FontFamily("Segoe UI Variable Text"),
                FontSize = 13,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex)),
                TextWrapping = TextWrapping.Wrap
            };
            sp.Children.Add(label);
            sp.Children.Add(tb);
            ChatPanel.Children.Add(sp);
            if (_settings.Chat.AutoScroll) ChatScroll.ScrollToEnd();
        }

        private void SendChat()
        {
            string msg = CmdInput.Text.Trim();
            if (msg.Length == 0) return;
            AppendChat("VOCE", msg, "#FFF2F6FF");
            try
            {
                var cmdPath = IOPath.Combine(AppDomain.CurrentDomain.BaseDirectory, "command.json");
                var payload = JsonSerializer.Serialize(new
                {
                    type = "chat",
                    text = msg,
                    reply_id = _lastReplyId + 1,
                    ts = DateTime.Now.ToString("HH:mm:ss")
                });
                File.WriteAllText(cmdPath, payload);
            }
            catch { }
            CmdInput.Text = "";
            if (_settings.Chat.KeepFocus) CmdInput.Focus();
            StatusText.Text = "mensagem enviada \u2192 KATIE responde via state.json";
        }

        private void CmdInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) SendChat();
        }

        private void CmdInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            ChatPlaceholder.Visibility = string.IsNullOrEmpty(CmdInput.Text) ? Visibility.Visible : Visibility.Collapsed;
        }

        // ===== Fase 4.5 — glow de foco da borda do compositor (borda do Border pai) =====
        private SolidColorBrush? _chatGlowBrush;

        /// <summary>Acende/apaga a borda do ChatInputBorder com fade de 120 ms (CubicEase EaseOut).
        /// Brush LOCAL temporário (evita mutar o AccentBrushFaint compartilhado) e devolve o
        /// DynamicResource no fim do blur — assim a troca de acento continua viva fora do foco.</summary>
        private void AnimateChatInputGlow(bool focused)
        {
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            if (focused)
            {
                var accent = Application.Current.Resources["AccentColor"] is Color c
                    ? c
                    : Color.FromRgb(0x00, 0xD9, 0xFF);
                _chatGlowBrush = new SolidColorBrush(accent) { Opacity = 0.35 };
                ChatInputBorder.BorderBrush = _chatGlowBrush;
                _chatGlowBrush.BeginAnimation(SolidColorBrush.OpacityProperty,
                    new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(120)) { EasingFunction = ease });
            }
            else if (_chatGlowBrush is { } brush)
            {
                var fade = new DoubleAnimation(0.35, TimeSpan.FromMilliseconds(120)) { EasingFunction = ease };
                fade.Completed += (_, _) =>
                {
                    if (!CmdInput.IsKeyboardFocused && ReferenceEquals(ChatInputBorder.BorderBrush, brush))
                    {
                        // devolve o DynamicResource (ClearValue apagaria a expressão inteira → borda null;
                        // SetResourceReference reinstala a referência viva do acento)
                        ChatInputBorder.SetResourceReference(Border.BorderBrushProperty, "AccentBrushFaint");
                        _chatGlowBrush = null;
                    }
                };
                brush.BeginAnimation(SolidColorBrush.OpacityProperty, fade);
            }
        }

        // ================= MEDIA PAINEL INLINE =================
        private async System.Threading.Tasks.Task StartMediaAsync()
        {
            try
            {
                await _media.StartAsync();
                _media.Updated += OnMediaUpdate;
            }
            catch { }
        }

        /// <summary>Capa neutra do design (mesmo gradiente do PlayerWindow._coverDefault) — fallback da Fase 0.</summary>
        private static readonly Brush CoverDefault = CreateCoverDefault();

        /// <summary>v9.7: placeholder do tema (DynamicResource resolvido na hora da troca).</summary>
        private static Brush CoverPlaceholder =>
            LivePalette.Instance.Get("ThemeCoverPlaceholderBrush") ?? CoverDefault;

        private static Brush CreateCoverDefault()
        {
            var b = new LinearGradientBrush(Color.FromRgb(0x1E, 0x4E, 0x8C), Color.FromRgb(0x0A, 0x12, 0x20), 90.0);
            b.Freeze();
            return b;
        }

        private void OnMediaUpdate(MediaUpdate u)
        {
            Dispatcher.Invoke(() =>
            {
                InlineTitle.Text = string.IsNullOrWhiteSpace(u.Title) ? "Nada tocando" : u.Title;
                InlineArtist.Text = string.IsNullOrWhiteSpace(u.Artist) ? "KATIE · SMTC" : u.Artist;
                InlinePlayIcon.Text = u.IsPlaying ? "\uE769" : "\uE768"; // Pause / Play (MDL2)
                if (u.Thumbnail is { Length: > 0 } bytes)
                {
                    try
                    {
                        var img = new BitmapImage();
                        using var ms = new System.IO.MemoryStream(bytes);
                        img.BeginInit();
                        img.CacheOption = BitmapCacheOption.OnLoad;
                        img.StreamSource = ms;
                        img.EndInit();
                        img.Freeze();
                        InlineCover.Fill = new ImageBrush(img) { Stretch = Stretch.UniformToFill };
                    }
                    catch { InlineCover.Fill = CoverPlaceholder; } // decode falhou → neutro (espelho do PlayerWindow)
                }
                else
                {
                    // FASE 0: sem thumbnail (SMTC vazio / faixa nova sem arte ainda) → restaura o neutro.
                    // Antes, o ImageBrush da faixa anterior ficava para sempre (capa fantasma).
                    InlineCover.Fill = CoverPlaceholder;
                }

                // ===== FASE 1: espelho no fullscreen (capa/título/transporte) =====
                bool fsEmpty = string.IsNullOrWhiteSpace(u.Title);
                FsTitle.Text = fsEmpty ? "Nada tocando" : u.Title;
                FsArtist.Text = string.IsNullOrWhiteSpace(u.Artist) ? "—" : u.Artist;
                FsPlayIcon.Text = u.IsPlaying ? "\uE769" : "\uE768";
                FsPos.Text = $"{(int)u.Position.TotalMinutes}:{u.Position.Seconds:D2}";
                FsDur.Text = $"{(int)u.Duration.TotalMinutes}:{u.Duration.Seconds:D2}";
                if (u.Thumbnail is { Length: > 0 } fsBytes)
                {
                    try
                    {
                        var fsImg = new BitmapImage();
                        using var fsMs = new System.IO.MemoryStream(fsBytes);
                        fsImg.BeginInit();
                        fsImg.CacheOption = BitmapCacheOption.OnLoad;
                        fsImg.StreamSource = fsMs;
                        fsImg.EndInit();
                        fsImg.Freeze();
                        FsCover.Fill = new ImageBrush(fsImg) { Stretch = Stretch.UniformToFill };
                    }
                    catch { FsCover.Fill = CoverPlaceholder; }
                }
                else FsCover.Fill = CoverPlaceholder;

                var fsKey = $"{u.Title}|{u.Artist}";
                if (fsKey != _fsArtKey)
                {
                    _fsArtKey = fsKey;
                    _fsTitleLast = u.Title;
                    _fsArtistLast = u.Artist;
                    _fsLyrics = null;
                    _fsLyricIndex = -1;
                    _fsLineBlocks.Clear();
                    FsLyricsPanel.Children.Clear();
                    if (fsEmpty)
                    {
                        FsLyricsPanel.Children.Add(FsHintText("♪ sem música tocando — dá play em algo pra ver a letra aqui"));
                    }
                    else
                    {
                        _ = LoadLyricsAsync(u.Title, u.Artist, fsKey);
                    }
                }
                _fsPosBase = u.Position;
                _fsPosBaseAt = DateTime.UtcNow;
                _fsPlaying = u.IsPlaying;
                if (fsEmpty && _fsMusicOpen) ExitFullscreenMusic(); // spec: SMTC esvaziou → volta ao painel
                double ratio = u.Duration.TotalSeconds > 0 ? u.Position.TotalSeconds / u.Duration.TotalSeconds : 0;
                if (InlineTrack.ActualWidth > 0)
                    InlineFill.Width = Math.Clamp(ratio, 0, 1) * InlineTrack.ActualWidth;
                InlinePos.Text = $"{(int)u.Position.TotalMinutes}:{u.Position.Seconds:D2}";
                InlineDur.Text = $"{(int)u.Duration.TotalMinutes}:{u.Duration.Seconds:D2}";

                UpdateCoverAccent(u);

                _mediaPlaying = u.IsPlaying;
                _spectrum.FeedPlayback(u.IsPlaying, u.Position);
                ApplySourceBadge(u.AppName); // v9.7 P2 (escopo): selo origem no inline + fullscreen
                MusicWheelView.SetHasSession(u.HasSession); // v9.7 P1: sessão em canal próprio (AUMID pode ser vazio)
                bool audioLive = _spectrum.HasAudio || _mediaPlaying;
                MusicWheelView.SetPlaying(audioLive && _settings.Music.Wheel);
            });
        }

        /// <summary>v9.7 P2 (escopo): aplica o mesmo selo de origem (ícone + tooltip + clique) nos títulos
        /// inline e fullscreen da janela principal. Cacheia o último AUMID para não re-resolver por update.</summary>
        private void ApplySourceBadge(string aumid)
        {
            aumid ??= "";
            if (aumid == _sourceAumid) return;
            _sourceAumid = aumid;

            SourceAppIcon.ApplyBadge(InlineSourceIcon, aumid);
            SourceAppIcon.ApplyBadge(FsSourceIcon, aumid);
        }

        /// <summary>Clique no selo inline/fullscreen → traz o app de origem pra frente.</summary>
        private void SourceBadge_Click(object sender, MouseButtonEventArgs e) => SourceAppIcon.Activate(_sourceAumid);

        private void ToggleMediaPanel() => SetMediaPanel(!_mediaPanelOpen, InstantMedia);

        /// <summary>Subida do cartão: opacity + translateY + escala, com visible/hit-test sincronizados.</summary>
        private void SetMediaPanel(bool open, bool instant)
        {
            _mediaPanelOpen = open;
            MediaPanel.IsHitTestVisible = open;

            if (instant)
            {
                MediaPanel.BeginAnimation(HeightProperty, null);
                MediaPanel.BeginAnimation(OpacityProperty, null);
                MediaTranslate.BeginAnimation(TranslateTransform.YProperty, null);
                MediaScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                MediaScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                MediaTranslate.Y = 0;
                MediaScale.ScaleX = MediaScale.ScaleY = 1;
                MediaPanel.Height = open ? MediaPanelHeight : 0;
                MediaPanel.Opacity = open ? 1 : 0;
                // T3.2: layout ainda não passou — monta post-layout (prioridade Loaded)
                if (open) Dispatcher.BeginInvoke(() => { if (_mediaPanelOpen) MountMediaGlass(); },
                    System.Windows.Threading.DispatcherPriority.Loaded);
                else UnmountMediaGlass();
                return;
            }

            var ease = new CubicEase { EasingMode = open ? EasingMode.EaseOut : EasingMode.EaseIn };
            var dur = TimeSpan.FromMilliseconds(open ? 280 : 190);

            var h = new DoubleAnimation { To = open ? MediaPanelHeight : 0, Duration = dur, EasingFunction = ease };
            MediaPanel.BeginAnimation(HeightProperty, h);

            var fade = new DoubleAnimation { To = open ? 1 : 0, Duration = dur, EasingFunction = ease };
            MediaPanel.BeginAnimation(OpacityProperty, fade);

            var slide = new DoubleAnimation { From = open ? 16 : MediaTranslate.Y, To = open ? 0 : 12, Duration = dur, EasingFunction = ease };
            MediaTranslate.BeginAnimation(TranslateTransform.YProperty, slide);

            var scale = new DoubleAnimation { From = open ? 0.97 : MediaScale.ScaleX, To = open ? 1 : 0.98, Duration = dur, EasingFunction = ease };
            MediaScale.BeginAnimation(ScaleTransform.ScaleXProperty, scale);
            MediaScale.BeginAnimation(ScaleTransform.ScaleYProperty, scale.Clone());

            // T3.2 (24/10): host do shader de vidro. Montar DEPOIS da animação (W6/R9);
            // teardown ANTES do colapso (nunca lensar backdrop em movimento).
            if (open)
            {
                h.Completed += (_, _) => { if (_mediaPanelOpen) MountMediaGlass(); };
            }
            else UnmountMediaGlass();
        }

        // ================= T3.2 (24/10): LIQUID GLASS — host do shader (MediaPanel piloto) =================
        // Árvore (padrão F2B): MediaGlassHost é irmão de BAIXO da Face vetorial — o conteúdo
        // vivo nunca entra no elemento com Effect (R9). Backdrop = VisualBrush do
        // BackdropStack (grid+scan+vignette vivos), Viewbox = rect do host (overscan Pad).
        private Effects.LiquidGlassEffect? _mediaGlassFx;
        private const double MediaGlassPad = 12.0;   // ≥ Refraction(8)+4: dobra sem clamp

        /// <summary>Monta o shader no painel (pós-layout; idempotente). Só em modo Shader.</summary>
        private void MountMediaGlass()
        {
            if (Glass.Mode != GlassMode.Shader) return;
            double w = MediaPanel.ActualWidth, h = MediaPanel.ActualHeight;
            if (w < 1 || h < 1) return;

            if (_mediaGlassFx is null)
            {
                _mediaGlassFx = new Effects.LiquidGlassEffect
                {
                    Radius = 14,        // CornerRadius do MediaPanel
                    Chamfer = 12,       // Chamfer.Size do painel
                    Bezel = 10,         // faixa refrativa
                    Refraction = 8,     // dobra máxima ≤ MediaGlassPad-4
                    Chromatic = 0.6     // fringing Liquify (0 = mono, 1 = pleno 1:0.925:0.875)
                };
                MediaGlassHost.Effect = _mediaGlassFx;
            }

            var fx = _mediaGlassFx;
            fx.InnerSize = new Size(w, h);
            fx.Pad = new Size(MediaGlassPad, MediaGlassPad);

            // Viewbox em px de dispositivo (TranslatePoint a partir do host inflado)
            var origin = MediaGlassHost.TranslatePoint(new Point(0, 0), BackdropStack);
            var box = new Rect(
                new Point(Math.Round(origin.X), Math.Round(origin.Y)),
                new Size(Math.Round(MediaGlassHost.ActualWidth), Math.Round(MediaGlassHost.ActualHeight)));
            fx.Input = new VisualBrush(BackdropStack)
            {
                Viewbox = box,
                ViewboxUnits = BrushMappingMode.Absolute,
                Viewport = new Rect(0, 0, 1, 1),
                TileMode = TileMode.None,
                Stretch = Stretch.None
            };
            MediaGlassHost.Opacity = 1;
        }

        /// <summary>Teardown síncrono: host para de lensar ANTES de soltar brush/effect.</summary>
        private void UnmountMediaGlass()
        {
            if (_mediaGlassFx is null) return;
            MediaGlassHost.Opacity = 0;
            MediaGlassHost.Effect = null;   // nunca soltar o Input com o Effect vivo (implicit-input trap)
            _mediaGlassFx = null;           // fx + VisualBrush morrem juntos (GC)
        }

        private void InlinePlay_Click(object sender, MouseButtonEventArgs e) => _ = _media.TogglePlayPauseAsync();
        private void InlineNext_Click(object sender, MouseButtonEventArgs e) => _ = _media.NextAsync();
        private void InlinePrev_Click(object sender, MouseButtonEventArgs e) => _ = _media.PreviousAsync();
        private void MediaCloseBtn_Click(object sender, MouseButtonEventArgs e) => SetMediaPanel(false, InstantMedia);

        // ================= FASE 1: modo música FULLSCREEN + letra LRC =================
        private void FsBtn_Click(object sender, RoutedEventArgs e) => ToggleFullscreenMusic();

        private void ToggleFullscreenMusic()
        {
            if (_fsMusicOpen) ExitFullscreenMusic();
            else OpenFullscreenMusic();
        }

        // ================= v7 FASE B: CHAT EXPANSÍVEL (cobre o reator, NÃO a telemetria) =================
        private bool _chatExpanded;
        private readonly BlurEffect _chatVeilBlur = new BlurEffect { Radius = 0 }; // v9.3.7: véu local da coluna central
        private const int ChatCompactCol = 2;   // coluna original do CONVERSA

        private void ChatExpandBtn_Click(object sender, MouseButtonEventArgs e)
        {
            SetChatExpanded(!_chatExpanded, instant: !MotionEnabled);
            e.Handled = true;
        }

        private void SetChatExpanded(bool open, bool instant)
        {
            if (_chatExpanded == open) return;
            _chatExpanded = open;
            bool fsOpen = _fsMusicOpen;

            ChatExpandIcon.Text = open ? "\uE738" : "\uE740";

            if (open)
            {
                if (fsOpen) ExitFullscreenMusic();          // um "modo foco" por vez
                Panel.SetZIndex(IslandChat, 60);
                // cobre SÓ o reator e o espaço do chat (colunas 1-2): telemetria fica intacta
                Grid.SetColumn(IslandChat, 1);
                Grid.SetColumnSpan(IslandChat, 2);
                // F2.3: vidro Mica-like do estado expandido agora é o FILL DA FACE (o Path é o
                // único pintor da ilha desde a F2.3) — nada de Border.Background: o valor local
                // no Border brigava com o Style e era a raiz do bug do null no recolher.
                // F2.4: face DENSA de leitura (alfa medido no composto) — a F2B trocava para
                // o vidro @0x38 e as letras ficavam sobre blur 9 + reator @0,15 + grade.
                // Zero véu iOS novo: só a face muda; o blur ambiente vaza pelas margens.
                Glass.SetFace(ChatFace, "ThemeChatExpandedDenseBrush");

                // v9.3.7: chat expansível ganha VÉU DE BLUR local. O backdrop-blur das Settings
                // (SetBackdropBlur) cobre o AppContent inteiro e borraria o PRÓPRIO chat — aqui o
                // véu borra SÓ a coluna central (reator + chips agenda/emails/música + painel inline)
                // que o chat expandido cobre; telemetria (col 0) e IslandChat ficam nítidos.
                // Chanfros da subárvore colapsam junto (adorner não borra nem herda Opacity —
                // mesma causa-raiz do v9.3.6).
                if (CenterColumn.Effect != _chatVeilBlur) CenterColumn.Effect = _chatVeilBlur;
                SetSubtreeAdornersVisible(CenterColumn, false);
                if (!instant)
                    _chatVeilBlur.BeginAnimation(BlurEffect.RadiusProperty, new DoubleAnimation(9, TimeSpan.FromMilliseconds(180)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
                else { _chatVeilBlur.BeginAnimation(BlurEffect.RadiusProperty, null); _chatVeilBlur.Radius = 9; }

                if (!instant)
                {
                    // animação fina: fade curto + scale sutil (nada de salto grosso)
                    var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
                    var dur = TimeSpan.FromMilliseconds(180);
                    IslandChat.BeginAnimation(OpacityProperty, new DoubleAnimation(1, dur) { EasingFunction = ease });
                    var scale = IslandChat.RenderTransform as ScaleTransform ?? new ScaleTransform(1, 1);
                    IslandChat.RenderTransformOrigin = new Point(0.5, 0.5);
                    IslandChat.RenderTransform = scale;
                    scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.985, 1, dur) { EasingFunction = ease });
                    scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.99, 1, dur) { EasingFunction = ease });
                }

                // reator esmaece "por trás do vidro" (nunca some) — fade fino de 180ms
                if (ReactorHost != null)
                {
                    if (!instant)
                        ReactorHost.BeginAnimation(OpacityProperty, new DoubleAnimation(0.15, TimeSpan.FromMilliseconds(180)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
                    else { ReactorHost.BeginAnimation(OpacityProperty, null); ReactorHost.Opacity = 0.15; }
                }
                CmdInput.Focus();
            }
            else
            {
                Panel.SetZIndex(IslandChat, fsOpen ? 60 : 0);
                Grid.SetColumn(IslandChat, ChatCompactCol);
                Grid.SetColumnSpan(IslandChat, 1);
                // F2.3: restaura o fill DEFAULT da ilha compacta na PRÓPRIA face. Não existe
                // "Background = null" (o Border não pinta mais) e Glass.SetFace(null/"") também
                // não serve: com chave vazia o ApplyFace retorna cedo e o Path fica com o último
                // Fill (a face presa que o gate proíbe). A chave declarada no XAML é a volta.
                Glass.SetFace(ChatFace, "IslandGlass");

                // v9.3.7: retira o véu com guard (padrão v9.3.2-fix/Codex P2: reabrir no meio do
                // fade-out não pode soltar o Effect); chanfros voltam SÓ depois que o blur saiu.
                if (CenterColumn.Effect == _chatVeilBlur)
                {
                    if (!instant)
                    {
                        var veilOff = new DoubleAnimation(0, TimeSpan.FromMilliseconds(200)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
                        veilOff.Completed += (_, _) =>
                        {
                            if (_chatVeilBlur.Radius > 0.01) return; // reabriu no meio do fade-out
                            CenterColumn.Effect = null;
                            SetSubtreeAdornersVisible(CenterColumn, true);
                        };
                        _chatVeilBlur.BeginAnimation(BlurEffect.RadiusProperty, veilOff);
                    }
                    else
                    {
                        _chatVeilBlur.BeginAnimation(BlurEffect.RadiusProperty, null);
                        _chatVeilBlur.Radius = 0;
                        CenterColumn.Effect = null;
                        SetSubtreeAdornersVisible(CenterColumn, true);
                    }
                }
                else SetSubtreeAdornersVisible(CenterColumn, true);

                if (ReactorHost != null)
                {
                    if (!instant)
                        ReactorHost.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(180)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
                    else { ReactorHost.BeginAnimation(OpacityProperty, null); ReactorHost.Opacity = 1; }
                }
            }
        }

        // v9.2: fundo "mica" no fullscreen de música — orbe + chips borrados; com CoverAll
        // as ilhas também esmaecem por trás do painel. Ilhas levam SÓ fade (nunca BlurEffect):
        // adorners das molduras (Chamfer/TechFrame) não seguem efeito/opacity do elemento e a
        // sombra da ilha seria descartada pelo blur (review Codex #4/#5). No CoverAll as
        // molduras das ilhas são desligadas para não flutuarem nítidas sobre o blur.
        private void SetFsBackdropBlur(bool on)
        {
            // v9.3: CoverAll agora alterna o FUNDO do painel fullscreen:
            //   true  = "mica" translúcido (#CC) — o blur do AppContent vira o efeito mica
            //   false = quase opaco (#F5) — blur desligado (nada visível atrás de qualquer forma)
            bool cover = _settings.Music.CoverAll;
            // v9.7: o fundo do fullscreen também segue o tema da capa (tokens Theme*).
            // v9.8: brush VIVO da paleta — a transição de 600 ms propaga sem reassinar nada.
            string themeKey = cover ? "ThemeFullscreenBrush" : "ThemeFullscreenOpaqueBrush";
            FullscreenMusic.Background = LivePalette.Instance.Get(themeKey)
                ?? new SolidColorBrush(Color.FromArgb(cover ? (byte)0xCC : (byte)0xF5, 0x0A, 0x13, 0x22));
            SetBackdropBlur(on && cover ? 12 : 0, on ? FullscreenMusic : null);
        }

        private void OpenFullscreenMusic()
        {
            if (_fsMusicOpen) return;
            _fsMusicOpen = true;
            FullscreenMusic.Visibility = Visibility.Visible;
            SetFsBackdropBlur(true);
            if (_chatExpanded) SetChatExpanded(false, instant: !MotionEnabled); // um modo foco por vez
            TechFrame.Refresh(FullscreenMusic);
            // F2.5: reator fora de vista (atrás do Z50 + blur) não gasta frames — congela no
            // mesmo ponto em que o IdleMotion-off já congela (anéis no ângulo atual, core em
            // repouso). Descongela no Exit.
            SetReactorPaused(true);
            // F2.5: adorners da subárvore do fullscreen (chanfros dos novos hosts) voltam nítidos.
            SetSubtreeAdornersVisible(FullscreenMusic, true);

            SyncLyricsForCurrentTrack();

            if (!MotionEnabled)
            {
                FullscreenMusic.BeginAnimation(OpacityProperty, null);
                FullscreenMusic.Opacity = 1;
            }
            else
            {
                var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
                var dur = TimeSpan.FromMilliseconds(220);
                FullscreenMusic.BeginAnimation(OpacityProperty, new DoubleAnimation(1, dur) { EasingFunction = ease });
            }

            _fsScrollCurrent = _fsScrollTarget = 0;
            _lyricsTimer.Start();
        }

        private void ExitFullscreenMusic()
        {
            if (!_fsMusicOpen) return;
            TechFrame.Clear(FullscreenMusic);
            // F2.5: colapsa os adorners da subárvore ANTES do fade — Adorner não herda Opacity
            // (lição v9.3.4/v9.3.6) e ficaria "órfão" nítido por cima do painel sumindo.
            SetSubtreeAdornersVisible(FullscreenMusic, false);
            // v9.3-fix (Codex P2): blur composto — Configurações pode seguir aberta
            SetBackdropBlur(_settingsOpen ? 10 : 0);
            _fsMusicOpen = false;
            _lyricsTimer.Stop();
            SetReactorPaused(false);

            if (!MotionEnabled)
            {
                FullscreenMusic.Visibility = Visibility.Collapsed;
                FullscreenMusic.BeginAnimation(OpacityProperty, null);
                FullscreenMusic.Opacity = 0;
                return;
            }

            var ease = new CubicEase { EasingMode = EasingMode.EaseIn };
            var dur = TimeSpan.FromMilliseconds(150);
            var fade = new DoubleAnimation(0, dur) { EasingFunction = ease };
            fade.Completed += (_, _) =>
            {
                FullscreenMusic.Visibility = Visibility.Collapsed;
                FullscreenMusic.BeginAnimation(OpacityProperty, null);
                FullscreenMusic.Opacity = 0;
            };
            FullscreenMusic.BeginAnimation(OpacityProperty, fade);
        }

        private void FsPrev_Click(object sender, MouseButtonEventArgs e) => _ = _media.PreviousAsync();
        private void FsPlay_Click(object sender, MouseButtonEventArgs e) => _ = _media.TogglePlayPauseAsync();
        private void FsNext_Click(object sender, MouseButtonEventArgs e) => _ = _media.NextAsync();

        /// <summary>Reabre a busca de letra da faixa corrente (ex.: abriu o fullscreen depois do fetch).</summary>
        private void SyncLyricsForCurrentTrack()
        {
            if (_fsLineBlocks.Count > 0 || _fsLyrics is { Length: > 0 }) return;
            if (string.IsNullOrWhiteSpace(_fsTitleLast) || string.IsNullOrWhiteSpace(_fsArtKey)) return;
            _ = LoadLyricsAsync(_fsTitleLast, _fsArtistLast, _fsArtKey);
        }

        private async System.Threading.Tasks.Task LoadLyricsAsync(string title, string artist, string key)
        {
            LyricLine[]? lines = null;
            try { lines = await LyricsService.GetAsync(title, artist); }
            catch { }

            if (key != _fsArtKey) return; // faixa mudou enquanto buscava
            Dispatcher.Invoke(() =>
            {
                if (key != _fsArtKey) return;
                _fsLyrics = lines;
                _fsLyricIndex = -1;
                _fsLineBlocks.Clear();
                FsLyricsPanel.Children.Clear();

                if (lines is { Length: > 0 })
                {
                    foreach (var l in lines)
                    {
                        var tb = new TextBlock
                        {
                            Text = l.Text,
                            FontFamily = new FontFamily("Segoe UI Variable Text"),
                            FontSize = 16,
                            FontWeight = FontWeights.Normal, // F2.5: peso uniforme — ênfase é scale/opacity (sem reflow)
                            Foreground = ThemedBrush("ThemeLabelBrush", FsLineFar),
                            HorizontalAlignment = HorizontalAlignment.Center,
                            TextAlignment = TextAlignment.Center,
                            TextWrapping = TextWrapping.Wrap,
                            MaxWidth = 560,
                            Margin = new Thickness(0, 7, 0, 7),
                            RenderTransformOrigin = new Point(0.5, 0.5),
                            RenderTransform = new ScaleTransform(1, 1)
                        };
                        FsLyricsPanel.Children.Add(tb);
                        _fsLineBlocks.Add(tb);
                    }
                }
                else
                {
                    FsLyricsPanel.Children.Add(FsHintText("♪ sem letra sincronizada para esta faixa"));
                }
                _fsScrollCurrent = _fsScrollTarget = 0;
            });
        }

        private static TextBlock FsHintText(string text) => new()
        {
            Text = text,
            FontFamily = new FontFamily("Segoe UI Variable Text"),
            FontSize = 13,
            // F2.5: 13px sobre a face de leitura também é texto pequeno → 4,5:1 (ThemeLabelBrush).
            Foreground = ThemedBrush("ThemeLabelBrush", FsLineFar),
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 10, 0, 10)
        };

        /// <summary>250ms: linha atual (busca móvel) + destaque + alvo de scroll. Dado, não decoração
        /// (roda também no perfil "Sem animações"; lá o scroll aplica direto, sem lerp).</summary>
        private void SyncLyricsTick()
        {
            if (!_fsMusicOpen) return;
            if (_fsLyrics is not { Length: > 0 } lines || _fsLineBlocks.Count == 0)
            {
                return;
            }

            var pos = _fsPlaying ? _fsPosBase + (DateTime.UtcNow - _fsPosBaseAt) : _fsPosBase;
            int idx = _fsLyricIndex;
            while (idx + 1 < lines.Length && lines[idx + 1].Time <= pos) idx++;
            while (idx >= 0 && lines[idx].Time > pos) idx--;

            if (idx != _fsLyricIndex)
            {
                _fsLyricIndex = idx;
                for (int i = 0; i < _fsLineBlocks.Count; i++)
                {
                    int state = i == idx ? 0 : Math.Abs(i - idx) == 1 ? 1 : 2;
                    SetLineEmphasis(_fsLineBlocks[i], state);
                }
                try
                {
                    if (idx >= 0 && idx < _fsLineBlocks.Count)
                    {
                        var y = _fsLineBlocks[idx].TransformToAncestor(FsLyricsPanel).Transform(new Point(0, 0)).Y;
                        _fsScrollTarget = Math.Max(0, y + _fsLineBlocks[idx].ActualHeight / 2 - FsLyricsScroll.ViewportHeight / 2);
                    }
                }
                catch { }
            }

            if (!MotionEnabled) UpdateFsScroll(0); // salto direto (sem animação)
        }

        /// <summary>F2.5 — ênfase da linha por RGB + opacidade + escala 1,06× (ativa), NUNCA por
        /// FontSize/peso: mudar métrica reflui o painel inteiro a cada 250 ms. A escala é
        /// transform-only (bitmap do próprio texto), sem Effect e sem re-layout.
        /// state: 0 = ativa · 1 = vizinha · 2 = distante. Estado fica em Tag para não
        /// reiniciar animação de linha que não mudou.</summary>
        private void SetLineEmphasis(TextBlock tb, int state)
        {
            if (tb.Tag is int prev && prev == state) return;
            tb.Tag = state;
            tb.Foreground = state switch
            {
                0 => ThemedBrush("ThemeTextStrongBrush", FsLineActive),
                1 => ThemedBrush("ThemeTextBrush", FsLineNear),
                _ => ThemedBrush("ThemeLabelBrush", FsLineFar)
            };
            tb.Opacity = state == 0 ? 1.0 : 0.92;
            if (tb.RenderTransform is not ScaleTransform st) return;
            double target = state == 0 ? 1.06 : 1.0;
            if (MotionEnabled)
            {
                var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
                var dur = TimeSpan.FromMilliseconds(180);
                st.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(target, dur) { EasingFunction = ease });
                st.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(target, dur) { EasingFunction = ease });
            }
            else
            {
                st.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                st.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                st.ScaleX = st.ScaleY = target;
            }
        }

        /// <summary>Lerp do scroll (chamado a 60fps pelo RenderTick quando MotionEnabled; dt=0 → salto).</summary>
        private void UpdateFsScroll(double dt)
        {
            if (!_fsMusicOpen) return;
            double target = _fsScrollTarget;
            if (dt <= 0)
            {
                _fsScrollCurrent = target;
                FsLyricsScroll.ScrollToVerticalOffset(target);
                return;
            }
            _fsScrollCurrent += (target - _fsScrollCurrent) * Math.Min(1, dt * 6);
            if (Math.Abs(target - _fsScrollCurrent) < 0.5) _fsScrollCurrent = target;
            FsLyricsScroll.ScrollToVerticalOffset(_fsScrollCurrent);
        }

        private void UndockBtn_Click(object sender, MouseButtonEventArgs e)
        {
            if (_player is { IsLoaded: true }) { _player.Close(); _player = null; }
            else
            {
                _player = new PlayerWindow(_media);   // sem Owner: nao puxa a janela principal
                _player.Show();                        // ShowActivated=false no XAML
            }
        }

        // ================= DETECÇÃO DE JOGOS =================
        private void StartGameDetection()
        {
            if (!_settings.GameDetectionEnabled)
            {
                StopGameDetection();
                return;
            }
            if (_games == null)
            {
                _games = new GameDetectionService(() => _settings.Games);
                _games.Updated += OnGameActivity;
            }
            if (!_games.IsRunning) _games.Start(TimeSpan.FromSeconds(4));
        }

        private void StopGameDetection()
        {
            _games?.Stop();
            GameLabel.Text = "detecção de jogos desativada";
            GameLabel.SetResourceReference(TextBlock.ForegroundProperty, "ThemeTextDimBrush");
            GameSinceLabel.Visibility = Visibility.Collapsed;
        }

        private void OnGameActivity(GameActivity act)
        {
            if (!_settings.GameDetectionEnabled) return;
            Dispatcher.Invoke(() => UpdateGameUi(act));
        }

        private void UpdateGameUi(GameActivity act)
        {
            switch (act.State)
            {
                case GameActivityState.Foreground:
                    GameLabel.Text = $"jogando agora: {act.GameName}";
                    GameLabel.SetResourceReference(TextBlock.ForegroundProperty, "ThemeSecondaryBrush");
                    ShowSince(act.SinceUtc);
                    break;
                case GameActivityState.Running:
                    GameLabel.Text = $"em execução: {act.GameName}";
                    GameLabel.SetResourceReference(TextBlock.ForegroundProperty, "ThemeTextBrush");
                    ShowSince(act.SinceUtc);
                    break;
                case GameActivityState.Unavailable:
                    GameLabel.Text = "não foi possível identificar";
                    GameLabel.SetResourceReference(TextBlock.ForegroundProperty, "ThemeTextDimBrush");
                    GameSinceLabel.Visibility = Visibility.Collapsed;
                    break;
                default:
                    GameLabel.Text = "nenhum jogo detectado";
                    GameLabel.SetResourceReference(TextBlock.ForegroundProperty, "ThemeTextDimBrush");
                    GameSinceLabel.Visibility = Visibility.Collapsed;
                    break;
            }
        }

        private void ShowSince(DateTime? sinceUtc)
        {
            if (sinceUtc == null) { GameSinceLabel.Visibility = Visibility.Collapsed; return; }
            var d = DateTime.UtcNow - sinceUtc.Value;
            GameSinceLabel.Text = d.TotalMinutes < 1
                ? "há instantes"
                : d.TotalHours >= 1
                    ? $"há {(int)d.TotalHours} h {d.Minutes} min"
                    : $"há {(int)d.TotalMinutes} min";
            GameSinceLabel.Visibility = Visibility.Visible;
        }

        // ================= STATE.JSON =================
        private void LoadState()
        {
            try
            {
                if (!File.Exists(_statePath)) return;
                var wt = File.GetLastWriteTimeUtc(_statePath);
                if (wt <= _lastStateWrite) return;
                _lastStateWrite = wt;

                using var doc = JsonDocument.Parse(File.ReadAllText(_statePath));
                var root = doc.RootElement;

                if (root.TryGetProperty("mode", out var m) && m.ValueKind == JsonValueKind.String)
                {
                    var v = m.GetString();
                    if (v != null && v != _mode) { _mode = v; ApplyMode(); }
                }
                if (root.TryGetProperty("status", out var st) && st.ValueKind == JsonValueKind.String)
                    StatusLine.Text = st.GetString() ?? "";
                if (root.TryGetProperty("message", out var msg) && msg.ValueKind == JsonValueKind.String)
                    StatusText.Text = msg.GetString() ?? "";
                if (root.TryGetProperty("reply", out var rep) && rep.ValueKind == JsonValueKind.String)
                {
                    string? text = rep.GetString();
                    int id = root.TryGetProperty("reply_id", out var rid) && rid.TryGetInt32(out var i) ? i : _lastReplyId + 1;
                    if (text != null && id != _lastReplyId)
                    {
                        _lastReplyId = id;
                        AppendChatTypewriter("KATIE", text, "#FF00D9FF"); // Fase E: máquina de escrever
                        PulseSpeech();
                    }
                }
                if (root.TryGetProperty("pulse", out var p) && p.ValueKind == JsonValueKind.True)
                    PulseSpeech();

                // v7 Fase C: métricas da sessão -> seção Sistema das Settings (campos opcionais)
                int? sysReq = null, sysTok = null, sysLat = null; double? sysPow = null;
                if (root.TryGetProperty("requests", out var rq) && rq.ValueKind == JsonValueKind.Number) sysReq = (int)rq.GetDouble();
                if (root.TryGetProperty("tokens", out var tk) && tk.ValueKind == JsonValueKind.Number) sysTok = (int)tk.GetDouble();
                if (root.TryGetProperty("latency_ms", out var ltm) && ltm.ValueKind == JsonValueKind.Number) sysLat = (int)ltm.GetDouble();
                if (root.TryGetProperty("power_w", out var pw) && pw.ValueKind == JsonValueKind.Number) sysPow = pw.GetDouble();
                if (sysReq.HasValue || sysTok.HasValue || sysLat.HasValue || sysPow.HasValue)
                    SettingsUI.UpdateSystemMetrics(sysReq, sysTok, sysLat, sysPow);
                if (root.TryGetProperty("player", out var pl))
                {
                    bool wantPlayer = pl.ValueKind == JsonValueKind.True;
                    if (wantPlayer && _player is not { IsLoaded: true })
                    { _player = new PlayerWindow(_media); _player.Show(); }
                    else if (!wantPlayer && _player is { IsLoaded: true })
                    { _player.Close(); _player = null; }
                }
                if (root.TryGetProperty("backdrop", out var bd) && bd.ValueKind == JsonValueKind.String)
                {
                    var v = bd.GetString();
                    if (v != null && v != _backdrop)
                    {
                        _backdrop = v;
                        DwmCompat.ApplyBackdrop(this, _backdrop); // hot-swap sem restart
                    }
                }
                if (root.TryGetProperty("panel", out var pnl) && pnl.ValueKind == JsonValueKind.String)
                {
                    if (pnl.GetString() == "music" && !_mediaPanelOpen) SetMediaPanel(true, InstantMedia);
                    else if (pnl.GetString() == "none" && _mediaPanelOpen) SetMediaPanel(false, InstantMedia);
                }
            }
            catch { /* state parcial: ignora */ }
        }

        private void ApplyMode()
        {
            StatusDot.Fill = _mode switch
            {
                "listening" => new SolidColorBrush(Color.FromRgb(0xFF, 0x3B, 0x3B)),
                "thinking" => new SolidColorBrush(Color.FromRgb(0x00, 0xD9, 0xFF)),
                "speaking" => new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x88)),
                _ => new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x88))
            };
            if (StatusDot.Effect is DropShadowEffect d)
                d.Color = _mode == "listening" ? Color.FromRgb(0xFF, 0x3B, 0x3B) : Color.FromRgb(0x00, 0xFF, 0x88);
            StatusLine.Text = _mode switch
            {
                "idle" => "K.A.T.I.E.",
                "listening" => "OUVINDO \u00B7 FALA COMIGO",
                "thinking" => "PROCESSANDO \u00B7 CONSULTANDO MODELOS",
                "speaking" => "FALANDO",
                _ => StatusLine.Text
            };
            ModeLabel.Text = $"MODE  {_mode}";
        }

        // ================= TELEMETRIA =================
        private async System.Threading.Tasks.Task UpdateTelemetryAsync()
        {
            double cpu = _lastCpu;
            double ramPct = 0, gpuPct = -1;
            (double rx, double tx) net = (-1, -1);

            try { cpu = await System.Threading.Tasks.Task.Run(GetCpuUsage); }
            catch { }

            try
            {
                var mem = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
                GlobalMemoryStatusEx(ref mem);
                ramPct = 100.0 * (mem.ullTotalPhys - mem.ullAvailPhys) / mem.ullTotalPhys;
            }
            catch { }

            try
            {
                var psi = new ProcessStartInfo("nvidia-smi", "--query-gpu=utilization.gpu --format=csv,noheader,nounits")
                { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
                using var p = Process.Start(psi);
                if (p != null)
                {
                    string o = await p.StandardOutput.ReadToEndAsync();
                    if (double.TryParse(o.Trim(), out var g)) gpuPct = g;
                }
            }
            catch { gpuPct = -1; }

            try { net = GetNetThroughputKBs(); } catch { }

            Dispatcher.Invoke(() =>
            {
                if (cpu >= 0) { CpuLabel.Text = $"CPU  {cpu:F0}%"; CpuBar.Value = cpu; }
                if (gpuPct >= 0) { GpuLabel.Text = $"GPU  {gpuPct:F0}%"; GpuBar.Value = gpuPct; }
                else GpuLabel.Text = "GPU  n/a";
                RamLabel.Text = $"RAM  {ramPct:F0}%"; RamBar.Value = ramPct;
                if (_settings.Telemetry.ShowNet)
                {
                    NetLabel.Text = net.rx < 0
                        ? "NET  \u2193 -- \u2191 --"
                        : $"NET  \u2193 {FmtNet(net.rx)} \u2191 {FmtNet(net.tx)}";
                }
            });
        }

        /// <summary>Throughput real somado das interfaces ativas (KB/s) — nada de ping/externo.</summary>
        private (double rx, double tx) GetNetThroughputKBs()
        {
            long rx = 0, tx = 0;
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                try
                {
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    var st = ni.GetIPv4Statistics();
                    rx += st.BytesReceived;
                    tx += st.BytesSent;
                }
                catch { }
            }

            var now = DateTime.UtcNow;
            if (_prevNetUtc == DateTime.MinValue)
            {
                _prevNetRx = rx; _prevNetTx = tx; _prevNetUtc = now;
                return (-1, -1);
            }
            double secs = Math.Max(0.5, (now - _prevNetUtc).TotalSeconds);
            double r = Math.Max(0, rx - _prevNetRx) / secs / 1024.0;
            double t = Math.Max(0, tx - _prevNetTx) / secs / 1024.0;
            _prevNetRx = rx; _prevNetTx = tx; _prevNetUtc = now;
            return (r, t);
        }

        private static string FmtNet(double kbs) =>
            kbs >= 1024 ? $"{kbs / 1024:F1}M/s" : $"{kbs:F0}K/s";

        // ===== Frame maximizado: SM_CXPADDEDBORDER escalado por DPI =====
        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        private const int SM_CXPADDEDBORDER = 92;

        private double GetPaddedBorder()
        {
            double px = GetSystemMetrics(SM_CXPADDEDBORDER);
            if (PresentationSource.FromVisual(this) is { } src && src.CompositionTarget != null)
                return px / src.CompositionTarget.TransformToDevice.M11;
            return px;
        }

        // ===== CPU P/Invoke =====
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetSystemTimes(out FILETIME idle, out FILETIME kernel, out FILETIME user);

        // ================= v7 iter3: CLIPBOARD WATCHER (KATIE reage ao que o sir copia) =================
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool AddClipboardFormatListener(IntPtr hwnd);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);
        private const int WM_CLIPBOARDUPDATE = 0x031D;
        private string _lastClip = "";
        private readonly System.Windows.Threading.DispatcherTimer _clipDebounce =
            new() { Interval = TimeSpan.FromMilliseconds(400) }; // FSW-duplo não: evento único, mas cola dupla sim

        private void InitClipboardWatcher()
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            AddClipboardFormatListener(hwnd);
            var source = System.Windows.Interop.HwndSource.FromHwnd(hwnd);
            source?.AddHook(ClipboardHook);
            _clipDebounce.Tick += (_, _) => { _clipDebounce.Stop(); OnClipboardChanged(); };
        }

        private IntPtr ClipboardHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_CLIPBOARDUPDATE)
            {
                handled = true;
                _clipDebounce.Stop(); _clipDebounce.Start(); // debounce 400ms (eventos em rajada)
            }
            return IntPtr.Zero;
        }

        private void OnClipboardChanged()
        {
            try
            {
                if (!Clipboard.ContainsText()) return;
                var text = Clipboard.GetText().Trim();
                if (text.Length == 0 || text == _lastClip) return;
                _lastClip = text;
                // só um aviso sutil no StatusText — NÃO é chat spam (o dado vai pro brain via state? fase futura)
                string preview = text.Length > 60 ? text[..60] + "…" : text;
                StatusText.Text = $"clipboard capturado ({text.Length} chars): {preview}";
            }
            catch { /* clipboard pode estar ocupado com outro processo — best-effort */ }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct FILETIME
        {
            public uint dwLowDateTime;
            public uint dwHighDateTime;
            public ulong Value => ((ulong)dwHighDateTime << 32) | dwLowDateTime;
        }

        private FILETIME _prevIdle, _prevKernel, _prevUser;
        private bool _cpuFirstSample = true;

        private double GetCpuUsage()
        {
            GetSystemTimes(out var idle, out var kernel, out var user);
            if (_cpuFirstSample)
            {
                _prevIdle = idle; _prevKernel = kernel; _prevUser = user;
                _cpuFirstSample = false;
                return _lastCpu < 0 ? 0 : _lastCpu;
            }
            ulong dIdle = idle.Value - _prevIdle.Value;
            ulong dKernel = kernel.Value - _prevKernel.Value;
            ulong dUser = user.Value - _prevUser.Value;
            _prevIdle = idle; _prevKernel = kernel; _prevUser = user;
            ulong dTotal = dKernel + dUser;
            if (dTotal == 0) return _lastCpu < 0 ? 0 : _lastCpu;
            double pct = 100.0 * (1.0 - (double)dIdle / (double)dTotal);
            if (pct < 0) pct = 0; if (pct > 100) pct = 100;
            _lastCpu = pct;
            return pct;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

        // ================= CHROME DA JANELA =================
        // Fase 1: DragMove removido — WindowChrome.CaptionHeight assume o drag e devolve Aero Snap.
        private void MinBtn_Click(object sender, MouseButtonEventArgs e) => WindowState = WindowState.Minimized;

        private void CloseBtn_Click(object sender, MouseButtonEventArgs e) => Close();

        private void MaxBtn_Click(object sender, MouseButtonEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private void TitleBtn_Stop(object sender, MouseButtonEventArgs e) { e.Handled = true; }

        // ================= CHIPS =================
        private void Chip_Click(object sender, MouseButtonEventArgs e)
        {
            string cmd = sender switch
            {
                Border b when b == ChipAgenda => "agenda",
                Border b when b == ChipEmails => "emails",
                Border b when b == ChipMusic => "music",
                _ => "unknown"
            };
            if (cmd == "music")
            {
                ToggleMediaPanel();
                return;
            }
            try
            {
                var cmdPath = IOPath.Combine(AppDomain.CurrentDomain.BaseDirectory, "command.json");
                File.WriteAllText(cmdPath, JsonSerializer.Serialize(new { chip = cmd, ts = DateTime.UtcNow }));
            }
            catch { }
            AppendChat("KATIE", $"chip '{cmd}' \u2192 comando enviado. Aguardo resposta via state.json.", "#FF00FF88");
        }

        private void SendBtn_Click(object sender, MouseButtonEventArgs e) => SendChat();

        protected override void OnClosed(EventArgs e)
        {
            _renderTimer.Stop(); _stateTimer.Stop(); _telemetryTimer.Stop(); _clockTimer.Stop(); _lyricsTimer.Stop(); _typeTimer.Stop(); _topmostTimer.Stop(); // Codex P2: timer v9.4.0 vazava no OnClosed
            MusicWheelView.StopTimer(); // Codex P2 v9.5.0: roda parava com a janela, não com o app
            _spectrum.BandsChanged -= OnBandsChanged;
            _spectrum.Dispose();
            _stateWatcher?.Dispose();
            var h = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            if (h != IntPtr.Zero) RemoveClipboardFormatListener(h);
            _games?.Dispose();
            base.OnClosed(e);
        }
    }
}
