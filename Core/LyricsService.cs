using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace KatieHoloDeck.Core
{
    /// <summary>Uma palavra da linha com o instante exato em que é cantada (LRC estendido).</summary>
    public sealed record WordSpan(string Text, TimeSpan Start, TimeSpan End);

    /// <summary>Uma linha de letra: instante inicial, texto e (opcional) marcações por palavra.</summary>
    public sealed record LyricLine(TimeSpan Time, string Text, TimeSpan End, IReadOnlyList<WordSpan>? Words);

    /// <summary>
    /// Letras sincronizadas (LRC) na API pública do LRCLIB (lrclib.net) — porte do Translucid
    /// (do próprio sir). /get com fallback pro /search; cache por faixa; falha de rede NÃO
    /// entra no cache (próxima tentativa tenta de novo).
    /// </summary>
    internal static class LyricsService
    {
        private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(6) };
        private static readonly ConcurrentDictionary<string, LyricLine[]> Cache = new();

        static LyricsService()
        {
            Http.DefaultRequestHeaders.UserAgent.ParseAdd("KatieHoloDeck/1.0");
        }

        /// <summary>Letras sincronizadas da faixa, ou null se não houver.</summary>
        public static async Task<LyricLine[]?> GetAsync(string title, string artist)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return null;
            }

            var key = $"{title}\u0001{artist}";
            if (Cache.TryGetValue(key, out var cached))
            {
                return cached.Length == 0 ? null : cached;
            }

            LyricLine[]? lines = null;
            var networkError = false;

            try
            {
                lines = await FetchAsync($"https://lrclib.net/api/get?track_name={Uri.EscapeDataString(title)}"
                    + (string.IsNullOrWhiteSpace(artist) ? "" : $"&artist_name={Uri.EscapeDataString(artist)}"))
                    .ConfigureAwait(false);

                // o /get retorna 404 quando há várias versões da música; tenta o /search
                lines ??= await FetchSearchAsync(title, artist).ConfigureAwait(false);
            }
            catch
            {
                networkError = true;
            }

            if (!networkError)
            {
                Cache.TryAdd(key, lines ?? Array.Empty<LyricLine>());
            }

            return lines;
        }

        private static async Task<LyricLine[]?> FetchSearchAsync(string title, string artist)
        {
            var url = $"https://lrclib.net/api/search?track_name={Uri.EscapeDataString(title)}"
                + (string.IsNullOrWhiteSpace(artist) ? "" : $"&artist_name={Uri.EscapeDataString(artist)}");

            using var response = await Http.GetAsync(url).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                if (item.TryGetProperty("syncedLyrics", out var lrc) && lrc.ValueKind == JsonValueKind.String)
                {
                    var lines = LrcParser.ParseLrc(lrc.GetString() ?? string.Empty);
                    if (lines is { Length: > 0 })
                    {
                        return lines;
                    }
                }
            }

            return null;
        }

        private static async Task<LyricLine[]?> FetchAsync(string url)
        {
            using var response = await Http.GetAsync(url).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
            var root = doc.RootElement;

            // Preferência: lyricsfile (YAML com start_ms/end_ms REAIS por linha).
            if (root.TryGetProperty("lyricsfile", out var lf) && lf.ValueKind == JsonValueKind.String)
            {
                var lines = LrcParser.ParseLrcFile(lf.GetString() ?? string.Empty);
                if (lines is { Length: > 0 })
                {
                    return lines;
                }
            }

            if (root.TryGetProperty("syncedLyrics", out var lrc) && lrc.ValueKind == JsonValueKind.String)
            {
                return LrcParser.ParseLrc(lrc.GetString() ?? string.Empty);
            }

            return null;
        }
    }
}
