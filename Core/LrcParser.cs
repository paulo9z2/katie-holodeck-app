using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace KatieHoloDeck.Core
{
    /// <summary>
    /// Parser LRC / LRC-file — porte fiel do LyricsService.cs do Translucid (do próprio sir).
    /// Suporta LRC estendido (marcações &lt;mm:ss.xx&gt; por palavra, karaokê) e o
    /// "lyricsfile" YAML da LRCLIB (start_ms/end_ms reais por linha).
    /// </summary>
    internal static class LrcParser
    {
        private static readonly Regex TimeTag = new(@"\[(\d{1,3}):(\d{1,2}(?:[.:]\d{1,3})?)\]", RegexOptions.Compiled);
        private static readonly Regex WordTag = new(@"<(\d{1,3}):(\d{1,2}(?:[.:]\d{1,3})?)>", RegexOptions.Compiled);

        private static TimeSpan TagTime(Match m) => TimeSpan.FromSeconds(
            int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) * 60 +
            double.Parse(m.Groups[2].Value.Replace(':', '.'), CultureInfo.InvariantCulture));

        /// <summary>Parse do LRC padrão (timestamp por linha).</summary>
        public static LyricLine[]? ParseLrc(string lrc)
        {
            var raw = new List<(TimeSpan Time, string Text)>();

            foreach (Match m in TimeTag.Matches(lrc))
            {
                var start = m.Index + m.Length;
                var end = lrc.IndexOf('\n', start);
                var text = (end < 0 ? lrc[start..] : lrc[start..end]).Trim();
                if (text.Length == 0 || text.StartsWith("[", StringComparison.Ordinal))
                {
                    continue;
                }
                raw.Add((TagTime(m), text));
            }

            if (raw.Count == 0)
            {
                return null;
            }

            var ordered = raw.OrderBy(r => r.Time).ToList();
            var result = new List<LyricLine>(ordered.Count);
            for (var i = 0; i < ordered.Count; i++)
            {
                var (time, text) = ordered[i];
                var endTime = i + 1 < ordered.Count ? ordered[i + 1].Time : time + TimeSpan.FromSeconds(5);
                var words = ParseWords(text, time, endTime);
                var clean = WordTag.Replace(text, "").Trim();
                if (clean.Length == 0)
                {
                    continue;
                }
                result.Add(new LyricLine(time, clean, endTime, words));
            }

            return result.Count == 0 ? null : result.ToArray();
        }

        /// <summary>Parse do lyricsfile da LRCLIB (YAML mínimo: lines com text/start_ms/end_ms).</summary>
        public static LyricLine[]? ParseLrcFile(string yaml)
        {
            var lines = new List<(TimeSpan Start, TimeSpan End, string Text)>();
            string? currentText = null;
            TimeSpan currentStart = default;

            foreach (var raw in yaml.Split('\n'))
            {
                var line = raw.TrimEnd('\r');
                if (line.StartsWith("- text:", StringComparison.Ordinal))
                {
                    currentText = line["- text:".Length..].Trim().Trim('\'', '"');
                }
                else if (currentText is not null && line.StartsWith("start_ms:", StringComparison.Ordinal))
                {
                    _ = int.TryParse(line["start_ms:".Length..].Trim(), out var ms);
                    currentStart = TimeSpan.FromMilliseconds(ms);
                }
                else if (currentText is not null && line.StartsWith("end_ms:", StringComparison.Ordinal))
                {
                    if (int.TryParse(line["end_ms:".Length..].Trim(), out var me))
                    {
                        lines.Add((currentStart, TimeSpan.FromMilliseconds(me), currentText));
                    }
                    currentText = null;
                }
            }

            if (lines.Count == 0)
            {
                return null;
            }

            var result = new List<LyricLine>(lines.Count);
            for (var i = 0; i < lines.Count; i++)
            {
                var (time, end, text) = lines[i];
                var words = ParseWords(text, time, end);
                var clean = WordTag.Replace(text, "").Trim();
                if (clean.Length == 0)
                {
                    continue;
                }
                result.Add(new LyricLine(time, clean, end, words));
            }

            return result.Count == 0 ? null : result.ToArray();
        }

        /// <summary>Extrai marcações &lt;mm:ss.xx&gt; por palavra; null se houver menos de duas.</summary>
        private static IReadOnlyList<WordSpan>? ParseWords(string text, TimeSpan lineStart, TimeSpan lineEnd)
        {
            var marks = WordTag.Matches(text);
            if (marks.Count < 2)
            {
                return null;
            }

            var words = new List<WordSpan>(marks.Count);
            for (var i = 0; i < marks.Count; i++)
            {
                var m = marks[i];
                var start = TagTime(m);

                var chunkEnd = i + 1 < marks.Count ? marks[i + 1].Index : text.Length;
                var wordText = text[m.Index..chunkEnd]
                    .Replace(WordTag.Replace(m.Value, ""), "")
                    .Trim();
                if (wordText.Length == 0)
                {
                    continue;
                }

                var end = i + 1 < marks.Count ? TagTime(marks[i + 1]) : lineEnd;

                words.Add(new WordSpan(wordText, start < lineStart ? lineStart : start, end));
            }

            return words.Count >= 2 ? words : null;
        }
    }
}
