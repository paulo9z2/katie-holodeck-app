using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Xml.Linq;

namespace KatieHoloDeck
{
    /// <summary>
    /// Mini player flutuante (estilo Translucid do sir). Deslocavel, topmost,
    /// NAO rouba foco (ShowActivated=false) e NAO tem Owner — interagir com ele
    /// nunca traz a janela principal pra frente. O MediaTracker pertence ao MainWindow.
    /// </summary>
    public partial class PlayerWindow : Window
    {
        private readonly MediaTracker _tracker;
        private bool _volDragging;
        private bool _volReady;
        private string? _sourceAumid; // v9.7 (selo origem): AUMID do app que está tocando (null = nada aplicado ainda)
        private static readonly Brush _coverDefault =
            new LinearGradientBrush(Color.FromRgb(0x1E, 0x4E, 0x8C), Color.FromRgb(0x0A, 0x12, 0x20), 90.0);

        public PlayerWindow(MediaTracker tracker)
        {
            InitializeComponent();

            // v9.8: mesma paleta VIVA da MainWindow (Window.Resources não congela) — o player
            // consome AccentBrush/Theme* e acompanha a transição sem reassinar recursos.
            Resources.MergedDictionaries.Add(LivePalette.Instance.Dictionary);
            _tracker = tracker ?? throw new ArgumentNullException(nameof(tracker));

            var wa = System.Windows.SystemParameters.WorkArea;
            Left = wa.Right - Width - 24;
            Top = wa.Bottom - Height - 12;

            Loaded += PlayerWindow_Loaded;
            Closed += PlayerWindow_Closed;
        }

        private void PlayerWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _tracker.Updated += OnMediaUpdate;
            _ = _tracker.PushAsync();
        }

        private void PlayerWindow_Closed(object? sender, EventArgs e) => _tracker.Updated -= OnMediaUpdate;

        private void OnMediaUpdate(MediaUpdate u)
        {
            Dispatcher.Invoke(() =>
            {
                TrackTitle.Text = string.IsNullOrWhiteSpace(u.Title) ? "Nada tocando" : u.Title;
                ApplySourceIcon(u.AppName); // selo origem: ícone do app no fim do nome
                TrackArtist.Text = string.IsNullOrWhiteSpace(u.Artist) ? "KATIE · SMTC" : u.Artist;
                PlayIcon.Text = u.IsPlaying ? "⏸" : "▶";

                if (u.Thumbnail is { Length: > 0 } bytes)
                {
                    try
                    {
                        var img = new BitmapImage();
                        using var ms = new MemoryStream(bytes);
                        img.BeginInit();
                        img.CacheOption = BitmapCacheOption.OnLoad;
                        img.StreamSource = ms;
                        img.EndInit();
                        img.Freeze();
                        CoverArt.Fill = new ImageBrush(img) { Stretch = Stretch.UniformToFill };
                    }
                    catch { CoverArt.Fill = _coverDefault; }
                }
                else CoverArt.Fill = _coverDefault;

                double ratio = u.Duration.TotalSeconds > 0 ? u.Position.TotalSeconds / u.Duration.TotalSeconds : 0;
                if (ProgressTrack.ActualWidth > 0)
                    ProgressFill.Width = Math.Clamp(ratio, 0, 1) * ProgressTrack.ActualWidth;
                PosText.Text = Fmt(u.Position);
                DurText.Text = Fmt(u.Duration);

                if (!_volDragging) VolSlider.Value = u.Volume * 100;
                _volReady = true;
            });
        }

        private static string Fmt(TimeSpan t) => $"{(int)t.TotalMinutes}:{t.Seconds:D2}";

        /// <summary>v9.7 (Selo de Origem): resolve o ícone do app que está tocando pelo AUMID do SMTC
        /// (cascata com cache: ícone do processo → ícone do pacote → glifo genérico) e mostra inline
        /// depois do nome, com fade-in leve na troca.</summary>
        private void ApplySourceIcon(string aumid)
        {
            aumid ??= "";
            if (aumid == _sourceAumid) return;
            _sourceAumid = aumid;

            SourceAppIcon.ApplyBadge(SourceIcon, aumid);
        }

        /// <summary>Clique no selo → traz o app de origem pra frente (SetForegroundWindow).</summary>
        private void SourceIcon_Click(object sender, MouseButtonEventArgs e) => SourceAppIcon.Activate(_sourceAumid ?? "");

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed) DragMove();
        }

        private void CloseBtn_Click(object sender, MouseButtonEventArgs e) => Close();

        private void Play_Click(object sender, MouseButtonEventArgs e) => _ = _tracker.TogglePlayPauseAsync();
        private void Next_Click(object sender, MouseButtonEventArgs e) => _ = _tracker.NextAsync();
        private void Prev_Click(object sender, MouseButtonEventArgs e) => _ = _tracker.PreviousAsync();

        private void Vol_DragStarted(object sender, RoutedEventArgs e) => _volDragging = true;
        private void Vol_DragCompleted(object sender, RoutedEventArgs e)
        {
            _volDragging = false;
            ApplyVolume();
        }
        private void Vol_Changed(object sender, RoutedEventArgs e)
        {
            if (!_volDragging) ApplyVolume();
        }
        private void ApplyVolume()
        {
            if (_volReady) MasterVolume.Set((float)(VolSlider.Value / 100.0));
        }
    }

    /// <summary>
    /// v9.7 (Selo de Origem) — AUMID do SMTC → ícone real do app que está tocando.
    /// Cascata com cache (mesma técnica do yasb, REF_yasb.md ideia 5):
    ///   1) ícone do PROCESSO (executável real do app);
    ///   2) ícone do PACOTE (MSIX: manifesto do pacote + logo);
    ///   3) fallback GENÉRICO (glifo).
    /// Sem NuGet: só P/Invoke (shell32/user32) + WPF. Tudo em cache por AUMID (resolve 1x por app).
    /// </summary>
    internal static class SourceAppIcon
    {
        private const int IconSize = 32;    // resolução do bitmap (a UI mostra 16 px)
        private const int SwRestore = 9;
        private const uint ProcessQueryLimitedInformation = 0x1000; // OpenProcess: ler AUMID sem privilégio
        private const int ErrorInsufficientBuffer = 122;

        private static readonly Dictionary<string, ImageSource?> IconCache = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> NameCache = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string?> ExeCache = new(StringComparer.OrdinalIgnoreCase);
        private static ImageSource? _generic;

        /// <summary>Ícone do app de origem (null = sem app resolvível).</summary>
        public static ImageSource? Resolve(string aumid)
        {
            if (string.IsNullOrWhiteSpace(aumid)) return null;
            lock (IconCache)
            {
                if (IconCache.TryGetValue(aumid, out var cached)) return cached;
            }

            var icon = ExtractProcessIcon(aumid) ?? ExtractPackageIcon(aumid) ?? GenericIcon();
            lock (IconCache) { IconCache[aumid] = icon; }
            return icon;
        }

        /// <summary>v9.7 P2 (escopo): aplica o selo em QUALQUER Image (PlayerWindow, InlineTitle, FsTitle).
        /// Sem ícone resolvível → Collapsed (não reserva espaço); com ícone → fade-in de 250 ms na
        /// OPACIDADE DO ELEMENTO (nunca storyboard em .Color de pincel da paleta).</summary>
        public static void ApplyBadge(Image target, string aumid)
        {
            var icon = Resolve(aumid);
            if (icon is null)
            {
                target.BeginAnimation(UIElement.OpacityProperty, null);
                target.Source = null;
                target.Opacity = 0;
                target.Visibility = Visibility.Collapsed;
                target.ToolTip = null;
                return;
            }

            target.Source = icon;
            target.ToolTip = ResolveName(aumid);
            target.Visibility = Visibility.Visible;
            target.Opacity = 0;
            target.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(250))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });
        }

        /// <summary>Nome legível do app de origem (tooltip do selo).</summary>
        public static string ResolveName(string aumid)
        {
            if (string.IsNullOrWhiteSpace(aumid)) return "";
            lock (NameCache)
            {
                if (NameCache.TryGetValue(aumid, out var cached)) return cached;
            }

            string name = ResolveNameUncached(aumid);
            lock (NameCache) { NameCache[aumid] = name; }
            return name;
        }

        /// <summary>Traz o app de origem pra frente (SetForegroundWindow na janela do AUMID).</summary>
        public static bool Activate(string aumid)
        {
            if (string.IsNullOrWhiteSpace(aumid)) return false;

            IntPtr hwnd = FindWindowByAumid(aumid);
            if (hwnd == IntPtr.Zero)
            {
                // sem janela com esse AUMID: tenta pelo nome do processo
                foreach (string name in CandidateProcessNames(aumid))
                {
                    try
                    {
                        foreach (var p in Process.GetProcessesByName(name))
                        {
                            try
                            {
                                if (!p.HasExited && p.MainWindowHandle != IntPtr.Zero)
                                {
                                    hwnd = p.MainWindowHandle;
                                    break;
                                }
                            }
                            finally { p.Dispose(); }
                        }
                    }
                    catch { }
                    if (hwnd != IntPtr.Zero) break;
                }
            }

            if (hwnd == IntPtr.Zero) return false;
            ShowWindow(hwnd, SwRestore);
            return BringToFront(hwnd);
        }

        // ================= NOME =================

        private static string ResolveNameUncached(string aumid)
        {
            string? exe = FindExecutable(aumid);
            if (exe is not null)
            {
                try
                {
                    var info = FileVersionInfo.GetVersionInfo(exe);
                    if (!string.IsNullOrWhiteSpace(info.FileDescription)) return info.FileDescription.Trim();
                }
                catch { }
                return Path.GetFileNameWithoutExtension(exe);
            }

            // sem processo: "SpotifyAB.SpotifyMusic_zmbnek6z7gvc!Spotify" → "Spotify"
            string id = aumid;
            int bang = id.LastIndexOf('!');
            if (bang >= 0 && bang < id.Length - 1) id = id[(bang + 1)..];
            else if (id.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) id = id[..^4];
            int dot = id.LastIndexOf('.');
            if (dot >= 0 && dot < id.Length - 1) id = id[(dot + 1)..];
            return Humanize(id);
        }

        private static string Humanize(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return "";
            string s = id.Replace('_', ' ').Replace('-', ' ').Trim();
            return char.ToUpperInvariant(s[0]) + s[1..];
        }

        // ================= CASCATA DE ÍCONES =================

        /// <summary>1) ícone do PROCESSO: acha a janela do app (AUMID) → PID → executável → ícone.</summary>
        private static ImageSource? ExtractProcessIcon(string aumid)
        {
            string? exe = FindExecutable(aumid);
            if (exe is null) return null;
            try
            {
                // ícone GRANDE (32 px) primeiro; small (16 px) como plano B
                IntPtr[] large = new IntPtr[1];
                IntPtr hIcon = ExtractIconEx(exe, 0, large, null, 1) != 0 ? large[0] : IntPtr.Zero;
                if (hIcon == IntPtr.Zero)
                {
                    IntPtr[] small = new IntPtr[1];
                    if (ExtractIconEx(exe, 0, null, small, 1) == 0) return null;
                    hIcon = small[0];
                    if (hIcon == IntPtr.Zero) return null;
                }

                try
                {
                    var bmp = Imaging.CreateBitmapSourceFromHIcon(
                        hIcon, Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(IconSize, IconSize));
                    bmp.Freeze();
                    return bmp;
                }
                finally { DestroyIcon(hIcon); }
            }
            catch { return null; }
        }

        /// <summary>2) ícone do PACOTE (MSIX): logo declarado no AppxManifest do pacote instalado.</summary>
        private static ImageSource? ExtractPackageIcon(string aumid)
        {
            try
            {
                int bang = aumid.IndexOf('!');
                if (bang <= 0) return null;

                string pfn = aumid[..bang]; // "SpotifyAB.SpotifyMusic_zmbnek6z7gvc"
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Packages", pfn);
                if (!Directory.Exists(dir)) return null;

                string manifest = Path.Combine(dir, "AppxManifest.xml");
                if (!File.Exists(manifest)) return null;

                string? logo = ReadManifestLogo(manifest);
                if (logo is null) return null;

                string assets = Path.Combine(dir, "Assets");
                if (!Directory.Exists(assets)) return null;

                string baseName = Path.GetFileNameWithoutExtension(logo);
                string? file = null;
                foreach (var f in Directory.EnumerateFiles(assets, baseName + "*.png"))
                {
                    file = f;
                    break;
                }
                if (file is null) return null;

                var img = new BitmapImage();
                img.BeginInit();
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.UriSource = new Uri(file);
                img.EndInit();
                img.Freeze();
                return img;
            }
            catch { return null; }
        }

        private static string? ReadManifestLogo(string manifestPath)
        {
            try
            {
                var doc = XDocument.Load(manifestPath);
                foreach (var el in doc.Descendants())
                {
                    if (!el.Name.LocalName.Equals("VisualElements", StringComparison.OrdinalIgnoreCase)) continue;

                    // primeiro o atributo "Logo" exato; depois qualquer "…Logo" (Square44x44Logo etc.)
                    string? fallback = null;
                    foreach (var attr in el.Attributes())
                    {
                        if (string.IsNullOrWhiteSpace(attr.Value)) continue;
                        if (attr.Name.LocalName.Equals("Logo", StringComparison.OrdinalIgnoreCase))
                            return attr.Value;
                        if (fallback is null && attr.Name.LocalName.EndsWith("Logo", StringComparison.OrdinalIgnoreCase))
                            fallback = attr.Value;
                    }
                    if (fallback is not null) return fallback;
                }
            }
            catch { }
            return null;
        }

        /// <summary>3) fallback GENÉRICO: glifo (disco + miolo) desenhado em código — zero dependência.</summary>
        private static ImageSource? GenericIcon()
        {
            if (_generic is not null) return _generic;
            try
            {
                var group = new DrawingGroup();
                group.Children.Add(new GeometryDrawing(
                    null, new Pen(new SolidColorBrush(Color.FromRgb(0x6C, 0x85, 0xA8)), 2.4),
                    new EllipseGeometry(new Point(16, 16), 12, 12)));
                group.Children.Add(new GeometryDrawing(
                    new SolidColorBrush(Color.FromRgb(0x9F, 0xB0, 0xC8)), null,
                    new EllipseGeometry(new Point(16, 16), 4.2, 4.2)));

                var image = new DrawingImage(group);
                image.Freeze();
                _generic = image;
            }
            catch { }
            return _generic;
        }

        // ================= AUMID → PROCESSO =================

        private static string? FindExecutable(string aumid)
        {
            lock (ExeCache)
            {
                if (ExeCache.TryGetValue(aumid, out var cached)) return cached;
            }

            string? exe = FindExecutableUncached(aumid);
            lock (ExeCache) { ExeCache[aumid] = exe; }
            return exe;
        }

        private static string? FindExecutableUncached(string aumid)
        {
            // 0) app de DESKTOP: o SMTC costuma expor o próprio caminho do .exe como AUMID
            //    ("C:\Program Files\Spotify\Spotify.exe") — o atalho mais curto e o mais certo.
            if (aumid.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && File.Exists(aumid))
                return aumid;

            // 1) a janela que EXPÕE esse AUMID → PID → executável
            IntPtr hwnd = FindWindowByAumid(aumid);
            if (hwnd != IntPtr.Zero)
            {
                GetWindowThreadProcessId(hwnd, out uint pid);
                string? exe = ExecutableOf((int)pid);
                if (exe is not null) return exe;
            }

            // 2) pelo NOME do processo ("chrome.exe" → "chrome", "!Spotify" → "Spotify")
            foreach (string name in CandidateProcessNames(aumid))
            {
                try
                {
                    foreach (var p in Process.GetProcessesByName(name))
                    {
                        try
                        {
                            if (p.HasExited) continue;
                            string? exe = ExecutableOf(p.Id);
                            if (exe is not null) return exe;
                        }
                        finally { p.Dispose(); }
                    }
                }
                catch { }
            }
            return null;
        }

        private static IEnumerable<string> CandidateProcessNames(string aumid)
        {
            var names = new List<string>();
            string id = aumid;
            int bang = id.LastIndexOf('!');
            if (bang >= 0 && bang < id.Length - 1) id = id[(bang + 1)..];
            if (id.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) id = id[..^4];
            if (id.Length > 0) names.Add(id);

            int dot = id.LastIndexOf('.');
            if (dot > 0 && dot < id.Length - 1)
            {
                string tail = id[(dot + 1)..];
                if (!names.Contains(tail)) names.Add(tail);
            }
            return names;
        }

        private static string? ExecutableOf(int pid)
        {
            try
            {
                using var p = Process.GetProcessById(pid);
                return p.MainModule?.FileName;
            }
            catch { return null; }
        }

        // ================= WIN32 (janela/AUMID/foreground) =================

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        private static IntPtr FindWindowByAumid(string aumid)
        {
            IntPtr found = IntPtr.Zero;
            EnumWindows((h, _) =>
            {
                string? id = GetAppUserModelId(h);
                if (id is not null && id.Equals(aumid, StringComparison.OrdinalIgnoreCase))
                {
                    found = h;
                    return false;
                }
                return true;
            }, IntPtr.Zero);
            return found;
        }

        private static string? GetAppUserModelId(IntPtr hwnd)
        {
            // v9.7 P2 fix: GetApplicationUserModelId é do kernel32 e recebe HANDLE DE PROCESSO (não HWND).
            // HWND → PID → OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION) → AUMID.
            if (hwnd == IntPtr.Zero) return null;
            GetWindowThreadProcessId(hwnd, out uint pid);
            if (pid == 0) return null;

            IntPtr process = OpenProcess(ProcessQueryLimitedInformation, false, pid);
            if (process == IntPtr.Zero) return null;
            try { return GetProcessAppUserModelId(process); }
            finally { CloseHandle(process); }
        }

        private static string? GetProcessAppUserModelId(IntPtr process)
        {
            try
            {
                uint len = 256;
                var sb = new StringBuilder((int)len);

                // buffer pequeno: a API devolve ERROR_INSUFFICIENT_BUFFER + o tamanho necessário em len
                int hr = GetApplicationUserModelId(process, ref len, sb);
                if (hr == ErrorInsufficientBuffer)
                {
                    if (len == 0 || len > 32768) return null;
                    sb = new StringBuilder((int)len + 1);
                    len = (uint)sb.Capacity;
                    hr = GetApplicationUserModelId(process, ref len, sb);
                }
                if (hr != 0) return null;

                string id = sb.ToString();
                return string.IsNullOrWhiteSpace(id) ? null : id;
            }
            catch { return null; }
        }

        private static bool BringToFront(IntPtr hwnd)
        {
            IntPtr fg = GetForegroundWindow();
            if (fg == IntPtr.Zero) return SetForegroundWindow(hwnd);

            uint fgThread = GetWindowThreadProcessId(fg, out _);
            uint target = GetWindowThreadProcessId(hwnd, out _);
            bool attached = fgThread != target && AttachThreadInput(fgThread, target, true);
            try { return SetForegroundWindow(hwnd); }
            finally { if (attached) AttachThreadInput(fgThread, target, false); }
        }

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetApplicationUserModelId(IntPtr hProcess, ref uint appUserModelIdLength, [Out] StringBuilder? appUserModelId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern uint ExtractIconEx(string lpszFile, int nIconIndex, IntPtr[]? phiconLarge, IntPtr[]? phiconSmall, uint nIcons);
    }
}
