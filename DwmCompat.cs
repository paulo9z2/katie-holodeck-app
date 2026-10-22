using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace KatieHoloDeck
{
    /// <summary>
    /// P/Invoke DWM (Win11) para a janela principal: dark mode, caption, border,
    /// cantos arredondados e backdrop (Mica/Acrylic). Cada atributo loga uma linha
    /// JSON em dwm_debug.jsonl no BaseDirectory (attr, valor, hresult).
    /// Referência: Wpf.Ui (commit ffebacd) — WindowBackdrop.cs / UnsafeNativeMethods.cs.
    /// </summary>
    internal static class DwmCompat
    {
        // ===== Atributos DWMWA_* =====
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_BORDER_COLOR = 34;
        private const int DWMWA_CAPTION_COLOR = 35;
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;

        // ===== Valores =====
        private const int DWMWCP_ROUND = 2;
        private const uint DWMWA_COLOR_NONE = 0xFFFFFFFE;

        // DWM_SYSTEMBACKDROP_TYPE
        private const int DWMSBT_NONE = 1;
        private const int DWMSBT_MAINWINDOW = 2;      // Mica
        private const int DWMSBT_TRANSIENTWINDOW = 3; // Acrylic

        // DWMWA_SYSTEMBACKDROP_TYPE só existe a partir do Win11 22H2 (build 22621)
        private const int MinBuildForBackdrop = 22621;

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        // Padrão wpfui RemoveWindowTitlebarContents: remove WS_SYSMENU p/ matar o menu
        // de sistema nativo (Alt+Space) que brigaria com o chrome customizado.
        // v8 (hang): o estilo mudado em runtime EXIGE SetWindowPos(SWP_FRAMECHANGED) para
        // o win32k/DWM recomputarem o frame/non-client — sem isso a janela fica com
        // frame/hit-test stale (a técnica original do wpfui já rendeu black screen —
        // lepoco/wpfui#803; requisito documentado na página do SetWindowLongPtr).
        private const int GWL_STYLE = -16;
        private const long WS_SYSMENU = 0x00080000L;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_FRAMECHANGED = 0x0020;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

        private static readonly string LogPath =
            System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dwm_debug.jsonl");

        private static readonly System.Text.UTF8Encoding NoBomUtf8 = new(encoderShouldEmitUTF8Identifier: false);

        private static readonly object LogLock = new();

        private static void Log(string attr, string value, int hresult)
        {
            try
            {
                lock (LogLock)
                {
                    File.AppendAllText(
                        LogPath,
                        $"{{\"ts\":\"{DateTime.Now:HH:mm:ss.fff}\",\"attr\":\"{attr}\",\"value\":\"{value}\",\"hresult\":{hresult}}}\n",
                        NoBomUtf8);
                }
            }
            catch { /* log nunca derruba a janela */ }
        }

        private static void Set(IntPtr hwnd, string attrName, int attr, int value)
        {
            int v = value;
            int hr = DwmSetWindowAttribute(hwnd, attr, ref v, sizeof(int));
            Log(attrName, value.ToString(), hr);
        }

        private static IntPtr Handle(Window window) => new WindowInteropHelper(window).Handle;

        // ================= FASE 1: attrs visuais (sem backdrop) =================
        private static void ApplyVisuals(Window window)
        {
            IntPtr hwnd = Handle(window);
            if (hwnd == IntPtr.Zero) return;

            // Ordem wpfui WindowBackdrop: dark → caption → border → corner.
            // dark + caption bem cedo minimizam o flash branco na abertura (dotnet/wpf#10513).
            Set(hwnd, "DWMWA_USE_IMMERSIVE_DARK_MODE", DWMWA_USE_IMMERSIVE_DARK_MODE, 1);
            Set(hwnd, "DWMWA_CAPTION_COLOR", DWMWA_CAPTION_COLOR, unchecked((int)DWMWA_COLOR_NONE));

            // STRETCH: borda azul-ciano bem escura (COLORREF é BGR: 0x00221100 = RGB(0,17,34))
            Set(hwnd, "DWMWA_BORDER_COLOR", DWMWA_BORDER_COLOR, 0x00221100);

            Set(hwnd, "DWMWA_WINDOW_CORNER_PREFERENCE", DWMWA_WINDOW_CORNER_PREFERENCE, DWMWCP_ROUND);
        }

        /// <summary>Aplica o visual Win11 (dark/caption/border/corner) e zera os fundos.
        /// Chamado no SourceInitialized — o backdrop é aplicado à parte (ApplyBackdrop).</summary>
        public static void Apply(Window window)
        {
            ApplyVisuals(window);

            // Remove WS_SYSMENU (padrão wpfui RemoveWindowTitlebarContents).
            IntPtr hwnd = Handle(window);
            if (hwnd != IntPtr.Zero)
            {
                long style = GetWindowLong(hwnd, GWL_STYLE);
                long cleared = style & ~WS_SYSMENU;
                _ = SetWindowLongPtr(hwnd, GWL_STYLE, (IntPtr)cleared);
                Log("WS_SYSMENU", "removed", 0);

                // v8 (hang): recomputa o frame depois da troca de estilo (requisito Win32 —
                // sem este SWP_FRAMECHANGED o non-client/hit-test fica desatualizado no
                // win32k/DWM; era o furo do port do wpfui).
                // DIAGNÓSTICO: SetWindowPos DENTRO de SourceInitialized = re-entrância
                // no layout inicial do WPF (suspeito do nascimento-morto). Desligado p/ teste.
                // bool ok = SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0,
                //     SWP_FRAMECHANGED | SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
                // Log("SWP_FRAMECHANGED", ok ? "after_style_change" : "failed", ok ? 0 : Marshal.GetLastWin32Error());
            }

            // Zera fundos para o backdrop respirar (padrão WindowBackdrop.RemoveBackground do wpfui):
            // background do visual root + CompositionTarget.BackgroundColor transparentes.
            window.SetCurrentValue(Window.BackgroundProperty, System.Windows.Media.Brushes.Transparent);
            if (PresentationSource.FromVisual(window) is HwndSource source && source.CompositionTarget != null)
                source.CompositionTarget.BackgroundColor = System.Windows.Media.Colors.Transparent;
        }

        // ================= SEMPRE NO TOPO (chatOnTop) =================
        private static readonly IntPtr HwndTopmost = new(-1);    // HWND_TOPMOST
        private static readonly IntPtr HwndNotTopmost = new(-2); // HWND_NOTOPMOST

        /// <summary>
        /// Reafirma HWND_TOPMOST (ou devolve ao normal em OFF) sem mover, redimensionar
        /// nem roubar foco — SWP_NOACTIVATE|SWP_NOMOVE|SWP_NOSIZE.
        /// </summary>
        public static void SetTopmost(Window window, bool topmost)
        {
            IntPtr hwnd = Handle(window);
            if (hwnd == IntPtr.Zero) return;
            _ = SetWindowPos(
                hwnd,
                topmost ? HwndTopmost : HwndNotTopmost,
                0, 0, 0, 0,
                SWP_NOACTIVATE | SWP_NOMOVE | SWP_NOSIZE);
        }

        // ================= FASE 2: backdrop =================
        /// <summary>
        /// Aplica backdrop por nome: "mica"=DWMSBT_MAINWINDOW(2), "acrylic"=DWMSBT_TRANSIENTWINDOW(3),
        /// "solid"=DWMSBT_NONE(1, nenhum efeito). Ausente/inválido = mica (retrocompatível).
        /// Guard: só a partir do build 22621 (Win11 22H2); antes disso loga e não faz nada.
        /// </summary>
        public static void ApplyBackdrop(Window window, string? name)
        {
            IntPtr hwnd = Handle(window);
            if (hwnd == IntPtr.Zero) return;

            if (Environment.OSVersion.Version.Build < MinBuildForBackdrop)
            {
                Log("DWMWA_SYSTEMBACKDROP_TYPE", $"skipped_build_{Environment.OSVersion.Version.Build}", 0);
                return;
            }

            int value = name?.Trim().ToLowerInvariant() switch
            {
                "acrylic" => DWMSBT_TRANSIENTWINDOW,
                "solid" => DWMSBT_NONE,
                _ => DWMSBT_MAINWINDOW // "mica" e qualquer outra coisa: default retrocompatível
            };
            Set(hwnd, "DWMWA_SYSTEMBACKDROP_TYPE", DWMWA_SYSTEMBACKDROP_TYPE, value);
        }
    }
}
