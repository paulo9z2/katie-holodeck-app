using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using KatieHoloDeck.Core;

namespace KatieHoloDeck
{
    /// <summary>
    /// Central de Configurações (painel flutuante do MainWindow). Todos os controles
    /// exibidos são funcionais: mutam a instância de AppSettings e disparam Changed →
    /// o MainWindow aplica na hora e persiste em settings.json (gravação segura).
    /// </summary>
    public partial class SettingsPanel : System.Windows.Controls.UserControl
    {
        /// <summary>Configuração mudou (a instância do evento é a vigente — troque a sua referência).</summary>
        public event Action<AppSettings>? Changed;

        /// <summary>Expõe o cartão do painel pro TechFrame da MainWindow.</summary>
        public System.Windows.Controls.Border Card => PanelCard;

        private AppSettings? _s;
        private string _settingsPath = "";
        private bool _loadingUi;
        private bool _navInited;

        public SettingsPanel()
        {
            InitializeComponent();
        }

        // ================= INICIALIZAÇÃO / SINCRONIZAÇÃO =================

        public void Initialize(AppSettings settings, string settingsPath)
        {
            _s = settings;
            _settingsPath = settingsPath;

            _loadingUi = true;
            try
            {
                if (!_navInited) { NavAparencia.IsChecked = true; _navInited = true; }

                SyncSwatches();
                HexBox.Text = settings.AccentHex;
                HexErr.Visibility = Visibility.Collapsed;

                (settings.EffectsProfile switch
                {
                    "reduced" => OptReduced,
                    "none" => OptNone,
                    _ => OptFull
                }).IsChecked = true;

                GameDetectChk.IsChecked = settings.GameDetectionEnabled;
                ReactorMotionChk.IsChecked = settings.Reactor.IdleMotion;
                GlassChk.IsChecked = settings.EnableGlass;
                UpdateGlassModeInfo();
                RebuildGames();
                GameAddErr.Visibility = Visibility.Collapsed;
                GameTestResult.Text = "";

                MusicAnimChk.IsChecked = settings.Music.AnimateOpen;
                MusicUndockChk.IsChecked = settings.Music.ShowUndockButton;
                MusicCoverAllChk.IsChecked = settings.Music.CoverAll;
                MusicThemeChk.IsChecked = settings.Music.CoverTheme;
                MusicAccentChk.IsChecked = settings.Music.CoverAccent;
                MusicWheelChk.IsChecked = settings.Music.Wheel;
                MusicRingsChk.IsChecked = settings.Music.Rings;
                MusicSensitivitySlider.Value = Math.Clamp(settings.Music.Sensitivity, 1.0, 20.0);
                MusicSensitivityValue.Text = Math.Clamp(settings.Music.Sensitivity, 1.0, 20.0).ToString("0.#");
                MusicCalcCombo.SelectedIndex = settings.Music.CalculationType switch
                {
                    "median" => 1,
                    "mean" => 2,
                    _ => 0
                };
                ChatFocusChk.IsChecked = settings.Chat.KeepFocus;
                ChatScrollChk.IsChecked = settings.Chat.AutoScroll;
                ChatOnTopChk.IsChecked = settings.Chat.ChatOnTop;

                int iv = settings.Telemetry.IntervalSeconds;
                (iv <= 3 ? OptInt2 : iv <= 7 ? OptInt5 : OptInt10).IsChecked = true;
                TelGpuChk.IsChecked = settings.Telemetry.ShowGpu;
                TelNetChk.IsChecked = settings.Telemetry.ShowNet;

                StartMinChk.IsChecked = settings.General.StartMinimized;

                // F2.0: versão vem de Core/AppInfo (fonte única = csproj), nunca de literal aqui.
                InfoVersion.Text = $"K.A.T.I.E. Holodeck · {AppInfo.VersionString} · WPF .NET 8 · este painel vem do ponto ciano do logo";
                InfoPath.Text = $"settings.json → {settingsPath}";
            }
            finally { _loadingUi = false; }
        }

        private void NotifyChanged()
        {
            if (_loadingUi || _s == null) return;
            Changed?.Invoke(_s);
        }

        /// <summary>Foco inicial ao abrir o painel (primeira categoria) — acessibilidade de teclado.</summary>
        public void FocusFirst()
        {
            NavAparencia.Focus();
        }

        /// <summary>v7 Fase C: atualiza as métricas da seção Sistema com os campos do state.json.
        /// Chamado pelo MainWindow.LoadState. NULL-safe: campo ausente mantém o valor atual.</summary>
        public void UpdateSystemMetrics(int? requests, int? tokens, double? latencyMs, double? powerW)
        {
            if (requests.HasValue) SysRequests.Text = requests.Value.ToString();
            if (tokens.HasValue) SysTokens.Text = tokens.Value.ToString();
            if (latencyMs.HasValue) SysLatency.Text = $"{latencyMs.Value / 1000.0:0.0}s";
            if (powerW.HasValue) SysPower.Text = $"{powerW.Value:0.0} W";
        }

        // ================= NAVEGAÇÃO =================

        private void Nav_Checked(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            var tag = (string)((RadioButton)sender).Tag;
            SecAparencia.Visibility = tag == "aparencia" ? Visibility.Visible : Visibility.Collapsed;
            SecEfeitos.Visibility = tag == "efeitos" ? Visibility.Visible : Visibility.Collapsed;
            SecJogos.Visibility = tag == "jogos" ? Visibility.Visible : Visibility.Collapsed;
            SecMusica.Visibility = tag == "musica" ? Visibility.Visible : Visibility.Collapsed;
            SecConversa.Visibility = tag == "conversa" ? Visibility.Visible : Visibility.Collapsed;
            SecTelemetria.Visibility = tag == "telemetria" ? Visibility.Visible : Visibility.Collapsed;
            SecSistema.Visibility = tag == "sistema" ? Visibility.Visible : Visibility.Collapsed;
            SecGeral.Visibility = tag == "geral" ? Visibility.Visible : Visibility.Collapsed;
        }

        // ================= APARÊNCIA =================

        private void SyncSwatches()
        {
            if (_s == null) return;
            foreach (var rb in new[] { Sw1, Sw2, Sw3, Sw4, Sw5 })
                rb.IsChecked = string.Equals((string)rb.Tag, _s.AccentHex, StringComparison.OrdinalIgnoreCase);
        }

        private void Swatch_Checked(object sender, RoutedEventArgs e)
        {
            if (_loadingUi || _s == null) return;
            var hex = (string)((RadioButton)sender).Tag;
            _s.AccentHex = hex;
            HexBox.Text = hex;
            HexErr.Visibility = Visibility.Collapsed;
            NotifyChanged();
        }

        private void HexBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) ApplyHexInput();
        }

        private void ApplyHex_Click(object sender, RoutedEventArgs e) => ApplyHexInput();

        private void ApplyHexInput()
        {
            if (_s == null) return;
            if (!SettingsService.TryParseAccent(HexBox.Text, out var c))
            {
                HexErr.Text = "cor inválida — use #RRGGBB ou #AARRGGBB (ex.: #FF00D9FF)";
                HexErr.Visibility = Visibility.Visible;
                return;
            }
            HexErr.Visibility = Visibility.Collapsed;
            _s.AccentHex = SettingsService.ToHex(c);
            HexBox.Text = _s.AccentHex;
            _loadingUi = true;
            try { SyncSwatches(); } finally { _loadingUi = false; }
            NotifyChanged();
        }

        private void ResetAccent_Click(object sender, RoutedEventArgs e)
        {
            if (_s == null) return;
            _s.AccentHex = "#FF00D9FF";
            HexBox.Text = _s.AccentHex;
            HexErr.Visibility = Visibility.Collapsed;
            _loadingUi = true;
            try { SyncSwatches(); } finally { _loadingUi = false; }
            NotifyChanged();
        }

        // ================= APARÊNCIA · MATERIAL (F1) =================

        private void Glass_Toggled(object sender, RoutedEventArgs e)
        {
            if (_loadingUi || _s == null) return;
            _s.EnableGlass = GlassChk.IsChecked == true;
            UpdateGlassModeInfo();
            NotifyChanged();
        }

        /// <summary>Mostra o modo EFETIVO (Off/Vector/Shader) — OptNone trava em Vector.</summary>
        private void UpdateGlassModeInfo()
        {
            if (GlassModeInfo == null || _s == null) return;
            var mode = Glass.Resolve(_s.EnableGlass, _s.EffectsProfile);
            GlassModeInfo.Text = mode == GlassMode.Off
                ? "modo efetivo: off — o app fica com o contorno de acento atual."
                : $"modo efetivo: {Glass.Describe(mode)} — \"sem animações\" força vetorial; o shader chega na fase 2.";
        }

        // ================= EFEITOS =================

        private void Profile_Checked(object sender, RoutedEventArgs e)
        {
            if (_loadingUi || _s == null) return;
            _s.EffectsProfile = (string)((RadioButton)sender).Tag;
            UpdateGlassModeInfo();   // F1: OptNone força o modo vetorial — o texto tem de refletir
            NotifyChanged();
        }

        // ================= JOGOS =================

        private void GameDetect_Toggled(object sender, RoutedEventArgs e)
        {
            if (_loadingUi || _s == null) return;
            _s.GameDetectionEnabled = GameDetectChk.IsChecked == true;
            NotifyChanged();
        }

        private void RebuildGames()
        {
            GamesRows.Children.Clear();
            if (_s == null) return;
            foreach (var g in _s.Games)
                GamesRows.Children.Add(BuildGameRow(g));
            if (_s.Games.Count == 0)
            {
                GamesRows.Children.Add(new TextBlock
                {
                    Text = "nenhum jogo na lista — adicione abaixo ou restaure os padrões",
                    FontFamily = new FontFamily("Segoe UI Variable Text"),
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x40, 0x54, 0x6E)),
                    Margin = new Thickness(2, 4, 0, 4)
                });
            }
        }

        private UIElement BuildGameRow(GameProfile g)
        {
            var row = new Grid { Margin = new Thickness(0, 3, 0, 3) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var chk = new CheckBox
            {
                Style = (Style)FindResource("FluentCheckBoxStyle"),
                IsChecked = g.Enabled,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };
            chk.Checked += (_, _) => { g.Enabled = true; NotifyChanged(); };
            chk.Unchecked += (_, _) => { g.Enabled = false; NotifyChanged(); };
            Grid.SetColumn(chk, 0);

            var nameBox = new TextBox
            {
                Style = (Style)FindResource("FluentTextBoxStyle"),
                Text = g.Name,
                Padding = new Thickness(8, 6, 8, 6),
                FontFamily = new FontFamily("Segoe UI Variable Text"),
                FontSize = 12,
                MaxLength = 40,
                ToolTip = "nome exibido"
            };
            nameBox.LostFocus += (_, _) =>
            {
                var t = nameBox.Text.Trim();
                if (t.Length == 0) { nameBox.Text = g.Name; return; } // não aceita vazio
                if (t != g.Name) { g.Name = t; NotifyChanged(); }
            };
            var nameShell = new Border { Style = (Style)FindResource("FieldShellStyle"), Margin = new Thickness(0, 0, 8, 0), Child = nameBox };
            Grid.SetColumn(nameShell, 1);

            var procBox = new TextBox
            {
                Style = (Style)FindResource("FluentTextBoxStyle"),
                Text = string.Join(", ", g.Processes),
                Padding = new Thickness(8, 6, 8, 6),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                MaxLength = 160,
                ToolTip = "processos separados por vírgula (ex.: hollow_knight.exe, hollow_knight_64)"
            };
            procBox.LostFocus += (_, _) =>
            {
                var list = SettingsService.NormalizeMany(procBox.Text);
                if (list.Count == 0) { procBox.Text = string.Join(", ", g.Processes); return; }
                g.Processes = list;
                procBox.Text = string.Join(", ", list);
                NotifyChanged();
            };
            var procShell = new Border { Style = (Style)FindResource("FieldShellStyle"), Margin = new Thickness(0, 0, 8, 0), Child = procBox };
            Grid.SetColumn(procShell, 2);

            var del = new Button
            {
                Style = (Style)FindResource("GhostButtonStyle"),
                Content = "remover",
                Padding = new Thickness(10, 6, 10, 6),
                VerticalAlignment = VerticalAlignment.Center
            };
            del.Click += (_, _) =>
            {
                _s?.Games.Remove(g);
                RebuildGames();
                NotifyChanged();
            };
            Grid.SetColumn(del, 3);

            row.Children.Add(chk);
            row.Children.Add(nameShell);
            row.Children.Add(procShell);
            row.Children.Add(del);
            return row;
        }

        private void AddGame_Click(object sender, RoutedEventArgs e)
        {
            if (_s == null) return;
            var name = GameNameBox.Text.Trim();
            var procs = SettingsService.NormalizeMany(GameProcBox.Text);
            if (name.Length == 0 || procs.Count == 0)
            {
                ShowGameErr("preencha o nome exibido e o processo (ex.: hollow_knight.exe)");
                return;
            }
            var clash = _s.Games.FirstOrDefault(g => g.Processes.Intersect(procs, StringComparer.OrdinalIgnoreCase).Any());
            if (clash != null)
            {
                ShowGameErr($"esse processo já pertence a “{clash.Name}” — remova de lá ou use outro processo");
                return;
            }
            _s.Games.Add(new GameProfile { Name = name, Processes = procs });
            GameNameBox.Text = "";
            GameProcBox.Text = "";
            GameAddErr.Visibility = Visibility.Collapsed;
            RebuildGames();
            NotifyChanged();
        }

        private void ShowGameErr(string msg)
        {
            GameAddErr.Text = msg;
            GameAddErr.Visibility = Visibility.Visible;
        }

        private async void TestGames_Click(object sender, RoutedEventArgs e)
        {
            if (_s == null) return;
            TestGamesBtn.IsEnabled = false;
            GameTestResult.Text = "verificando…";
            try
            {
                var snapshot = new List<GameProfile>(_s.Games);
                var act = await Task.Run(() =>
                {
                    using var svc = new GameDetectionService(() => snapshot);
                    return svc.ScanOnce(snapshot);
                });
                GameTestResult.Text = act.State switch
                {
                    GameActivityState.Foreground => $"jogando agora: {act.GameName}",
                    GameActivityState.Running => $"em execução: {act.GameName} (janela em segundo plano)",
                    GameActivityState.Unavailable => "não foi possível ler os processos",
                    _ => "nenhum jogo detectado"
                };
            }
            catch
            {
                GameTestResult.Text = "falha inesperada no teste";
            }
            finally { TestGamesBtn.IsEnabled = true; }
        }

        // ================= MÚSICA / CONVERSA / TELEMETRIA / GERAL =================

        private void Music_Toggled(object sender, RoutedEventArgs e)
        {
            if (_loadingUi || _s == null) return;
            _s.Music.AnimateOpen = MusicAnimChk.IsChecked == true;
            _s.Music.ShowUndockButton = MusicUndockChk.IsChecked == true;
            _s.Music.CoverAll = MusicCoverAllChk.IsChecked == true;
            _s.Music.CoverTheme = MusicThemeChk.IsChecked == true;
            _s.Music.CoverAccent = MusicAccentChk.IsChecked == true;
            _s.Music.Wheel = MusicWheelChk.IsChecked == true;
            _s.Music.Rings = MusicRingsChk.IsChecked == true;
            NotifyChanged();
        }

        private void ReactorMotion_Toggled(object sender, RoutedEventArgs e)
        {
            if (_loadingUi || _s == null) return;
            _s.Reactor.IdleMotion = ReactorMotionChk.IsChecked == true;
            NotifyChanged();
        }

        private void MusicSensitivity_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_loadingUi || _s == null) return;
            double value = Math.Clamp(Math.Round(e.NewValue), 1.0, 20.0);
            _s.Music.Sensitivity = value;
            if (MusicSensitivityValue != null) MusicSensitivityValue.Text = value.ToString("0");
            NotifyChanged();
        }

        private void MusicCalc_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_loadingUi || _s == null) return;
            if (MusicCalcCombo.SelectedItem is ComboBoxItem { Tag: string tag })
            {
                _s.Music.CalculationType = tag;
                NotifyChanged();
            }
        }

        private void Chat_Toggled(object sender, RoutedEventArgs e)
        {
            if (_loadingUi || _s == null) return;
            _s.Chat.KeepFocus = ChatFocusChk.IsChecked == true;
            _s.Chat.AutoScroll = ChatScrollChk.IsChecked == true;
            _s.Chat.ChatOnTop = ChatOnTopChk.IsChecked == true;
            NotifyChanged();
        }

        private void Interval_Checked(object sender, RoutedEventArgs e)
        {
            if (_loadingUi || _s == null) return;
            if (int.TryParse((string)((RadioButton)sender).Tag, out var secs))
            {
                _s.Telemetry.IntervalSeconds = secs;
                NotifyChanged();
            }
        }

        private void Telemetry_Toggled(object sender, RoutedEventArgs e)
        {
            if (_loadingUi || _s == null) return;
            _s.Telemetry.ShowGpu = TelGpuChk.IsChecked == true;
            _s.Telemetry.ShowNet = TelNetChk.IsChecked == true;
            NotifyChanged();
        }

        private void General_Toggled(object sender, RoutedEventArgs e)
        {
            if (_loadingUi || _s == null) return;
            _s.General.StartMinimized = StartMinChk.IsChecked == true;
            NotifyChanged();
        }

        // ================= RESTAURAR PADRÕES =================

        private void RestoreDefaults_Click(object sender, RoutedEventArgs e)
        {
            var owner = Window.GetWindow(this);
            var answer = MessageBox.Show(
                owner,
                "Restaurar TODAS as configurações padrão?\n\n" +
                "· cor volta ao ciano elétrico\n" +
                "· efeitos voltam ao perfil Completo\n" +
                "· a lista de jogos é recriada a partir do catálogo (seus jogos personalizados saem)\n" +
                "· nenhum outro arquivo do app (state.json, command.json, música) é tocado.",
                "K.A.T.I.E. · restaurar padrões",
                MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
            if (answer != MessageBoxResult.Yes) return;

            var fresh = SettingsService.Defaults();
            Initialize(fresh, _settingsPath);
            Changed?.Invoke(fresh);
        }
    }
}
