using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Media;

namespace KatieHoloDeck
{
    // ================= MODELOS =================

    /// <summary>Perfil de jogo para a detecção local (nome exibido + nomes de processo).</summary>
    public sealed class GameProfile
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
        public string Name { get; set; } = "";
        /// <summary>Nomes de processo normalizados (minúsculos, sem ".exe"). Casamento EXATO.</summary>
        public List<string> Processes { get; set; } = new();
        public bool Enabled { get; set; } = true;
        public int Priority { get; set; } = 50;
    }

    public sealed class MusicPrefs
    {
        public bool AnimateOpen { get; set; } = true;
        public bool ShowUndockButton { get; set; } = true;
        /// <summary>true: no fullscreen de música tudo (telemetria/chat) fica sob o painel e borrado.</summary>
        // v9.3: default true — spec do sir 18/10: fullscreen de música SEMPRE cobre tudo;
        // CoverAll agora alterna fundo "mica translúcido" (true) vs "quase opaco" (false)
        public bool CoverAll { get; set; } = true;
        /// <summary>true: o acento acompanha a cor dominante da capa da música (transição ~600 ms).</summary>
        public bool CoverAccent { get; set; } = true;
        /// <summary>true: o APP INTEIRO (ilhas, chat, gauges, HUD, player) segue a paleta da capa.
        /// Default ON (v9.7). OFF devolve o tema ciano padrão do app.</summary>
        public bool CoverTheme { get; set; } = true;
        /// <summary>true: roda audio-reativa visível ao redor do reator enquanto toca música.</summary>
        public bool Wheel { get; set; } = true;
        /// <summary>true: os 2 anéis decorativos girantes (CW/CCW) da roda.</summary>
        public bool Rings { get; set; } = true;
        /// <summary>1..20 — escala do Wallpaper Engine para o AudioSpectrum.</summary>
        public double Sensitivity { get; set; } = 1.0;
        /// <summary>max | median | mean — cálculo dos peaks (Wallpaper Engine usa os mesmos).</summary>
        public string CalculationType { get; set; } = "max";
    }

    public sealed class ChatPrefs
    {
        public bool KeepFocus { get; set; } = true;
        public bool AutoScroll { get; set; } = true;
        /// <summary>true: reafirma HWND_TOPMOST a cada ~2 s (janela acima dos outros apps).</summary>
        public bool ChatOnTop { get; set; } = false;
    }

    public sealed class TelemetryPrefs
    {
        public int IntervalSeconds { get; set; } = 2;
        public bool ShowGpu { get; set; } = true;
        public bool ShowNet { get; set; } = true;
    }

    public sealed class GeneralPrefs
    {
        public bool StartMinimized { get; set; } = false;
    }

    public sealed class ReactorSettings
    {
        /// <summary>true: animação autônoma do reator (rotação dos anéis + pulso do núcleo).</summary>
        public bool IdleMotion { get; set; } = false;
    }

    public sealed class AppSettings
    {
        public int Version { get; set; } = 1;
        public string AccentHex { get; set; } = "#FF00D9FF";
        /// <summary>full | reduced | none</summary>
        public string EffectsProfile { get; set; } = "full";
        /// <summary>F1: aro de cristal T2 (vidro VETORIAL — o shader é F2). Default OFF:
        /// desligado o app fica com o visual aprovado do v9.8.2.</summary>
        public bool EnableGlass { get; set; } = false;
        public bool GameDetectionEnabled { get; set; } = true;
        public List<GameProfile> Games { get; set; } = new();
        public MusicPrefs Music { get; set; } = new();
        public ChatPrefs Chat { get; set; } = new();
        public TelemetryPrefs Telemetry { get; set; } = new();
        public GeneralPrefs General { get; set; } = new();
        public ReactorSettings Reactor { get; set; } = new();
    }

    public enum SettingsLoadStatus { Ok, MissingFile, Corrupted }

    // ================= SERVIÇO =================

    /// <summary>
    /// Persistência local das preferências (settings.json na BaseDirectory, ao lado de
    /// state.json/command.json — mesmo mecanismo do app). Arquivo ausente/corrompido
    /// nunca derruba: cai nos padrões e reporta o estado para a UI avisar.
    /// </summary>
    public static class SettingsService
    {
        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        public static AppSettings Defaults()
        {
            var s = new AppSettings();
            s.Games = DefaultCatalog();
            return s;
        }

        /// <summary>Catálogo inicial — nomes de processo reais, casamento exato (sem parcial).</summary>
        public static List<GameProfile> DefaultCatalog() => new()
        {
            new GameProfile { Name = "Counter-Strike 2",  Processes = { "cs2" } },
            new GameProfile { Name = "VALORANT",          Processes = { "valorant" } },
            new GameProfile { Name = "Rocket League",     Processes = { "rocketleague" } },
            new GameProfile { Name = "GTA V",             Processes = { "gta5", "gta5_enhanced" } },
            new GameProfile { Name = "Roblox",            Processes = { "robloxplayerbeta" } },
            new GameProfile { Name = "Dota 2",            Processes = { "dota2" } },
            new GameProfile { Name = "Fortnite",          Processes = { "fortniteclient-win64-shipping" } },
            new GameProfile { Name = "League of Legends", Processes = { "league of legends" } },
            new GameProfile { Name = "Overwatch",         Processes = { "overwatch" } },
            new GameProfile { Name = "Apex Legends",      Processes = { "r5apex", "r5apex_dx12" } },
            new GameProfile { Name = "Elden Ring",        Processes = { "eldenring" } },
            new GameProfile { Name = "No Man's Sky",      Processes = { "nms" } },
            new GameProfile { Name = "Cyberpunk 2077",    Processes = { "cyberpunk2077" } },
            new GameProfile { Name = "The Witcher 3",     Processes = { "witcher3" } },
            new GameProfile { Name = "Stardew Valley",    Processes = { "stardew valley" } },
            new GameProfile { Name = "Terraria",          Processes = { "terraria" } },
        };

        /// <summary>"RocketLeague.exe " → "rocketleague" (minúsculo, sem .exe, sem espaços nas pontas).</summary>
        public static string NormalizeProcess(string? raw)
        {
            var v = (raw ?? "").Trim().ToLowerInvariant();
            if (v.EndsWith(".exe")) v = v[..^4];
            return v.Trim();
        }

        /// <summary>"a.exe, b; c" → ["a","b","c"] (normalizados, sem vazios, sem duplicatas).</summary>
        public static List<string> NormalizeMany(string? raw) =>
            (raw ?? "")
                .Split(new[] { ',', ';', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(NormalizeProcess)
                .Where(p => p.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

        public static bool TryParseAccent(string? hex, out Color color)
        {
            color = default;
            try
            {
                if (string.IsNullOrWhiteSpace(hex)) return false;
                var v = hex.Trim();
                if (!v.StartsWith('#')) v = "#" + v;
                if (ColorConverter.ConvertFromString(v) is Color c) { color = c; return true; }
            }
            catch { }
            return false;
        }

        public static string ToHex(Color c) =>
            $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";

        public static AppSettings Load(string path, out SettingsLoadStatus status)
        {
            try
            {
                if (!File.Exists(path)) { status = SettingsLoadStatus.MissingFile; return Defaults(); }
                var s = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), JsonOpts);
                if (s == null) { status = SettingsLoadStatus.Corrupted; return Defaults(); }
                Validate(s);
                status = SettingsLoadStatus.Ok;
                return s;
            }
            catch
            {
                status = SettingsLoadStatus.Corrupted;
                return Defaults();
            }
        }

        /// <summary>Gravação segura: escreve .tmp e substitui (nunca deixa arquivo pela metade).</summary>
        public static bool Save(string path, AppSettings s)
        {
            try
            {
                Validate(s);
                var tmp = path + ".tmp";
                File.WriteAllText(tmp, JsonSerializer.Serialize(s, JsonOpts));
                File.Move(tmp, path, overwrite: true);
                return true;
            }
            catch { return false; }
        }

        /// <summary>Normalização defensiva — settings editado à mão não derruba o app.</summary>
        public static void Validate(AppSettings s)
        {
            if (!TryParseAccent(s.AccentHex, out var c)) s.AccentHex = "#FF00D9FF";
            else s.AccentHex = ToHex(c);

            s.EffectsProfile = (s.EffectsProfile ?? "").Trim().ToLowerInvariant() switch
            {
                "reduced" => "reduced",
                "none" => "none",
                _ => "full"
            };

            s.Telemetry ??= new TelemetryPrefs();
            s.Telemetry.IntervalSeconds = Math.Clamp(s.Telemetry.IntervalSeconds, 1, 30);

            s.Music ??= new MusicPrefs();
            s.Music.Sensitivity = Math.Clamp(s.Music.Sensitivity, 1.0, 20.0);
            s.Music.CalculationType = (s.Music.CalculationType ?? "").Trim().ToLowerInvariant() switch
            {
                "median" => "median",
                "mean" => "mean",
                _ => "max"
            };
            s.Chat ??= new ChatPrefs();
            s.General ??= new GeneralPrefs();
            s.Reactor ??= new ReactorSettings();

            s.Games ??= new List<GameProfile>();
            foreach (var g in s.Games)
            {
                g.Id = string.IsNullOrWhiteSpace(g.Id) ? Guid.NewGuid().ToString("N")[..8] : g.Id;
                g.Name = (g.Name ?? "").Trim();
                g.Processes = (g.Processes ?? new List<string>())
                    .Select(NormalizeProcess)
                    .Where(p => p.Length > 0)
                    .Distinct()
                    .ToList();
                g.Priority = Math.Clamp(g.Priority, 0, 100);
            }
            s.Games.RemoveAll(g => g.Name.Length == 0 || g.Processes.Count == 0);
        }
    }
}
