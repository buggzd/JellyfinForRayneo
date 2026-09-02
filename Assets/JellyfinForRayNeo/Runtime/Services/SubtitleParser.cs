using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;

namespace JellyfinForRayNeo
{
    public sealed class SubtitleCue
    {
        public double StartSeconds;
        public double EndSeconds;
        public string Text;
    }

    public sealed class SubtitleTrack
    {
        private readonly List<SubtitleCue> _cues;

        public SubtitleTrack(IEnumerable<SubtitleCue> cues)
        {
            _cues = (cues ?? Enumerable.Empty<SubtitleCue>())
                .Where(cue => cue != null
                    && cue.EndSeconds > cue.StartSeconds
                    && !string.IsNullOrWhiteSpace(cue.Text))
                .OrderBy(cue => cue.StartSeconds)
                .ThenBy(cue => cue.EndSeconds)
                .ToList();
        }

        public IReadOnlyList<SubtitleCue> Cues
        {
            get { return _cues; }
        }

        public string TextAt(double seconds)
        {
            if (_cues.Count == 0 || seconds < 0d)
            {
                return string.Empty;
            }

            int low = 0;
            int high = _cues.Count;
            while (low < high)
            {
                int middle = low + (high - low) / 2;
                if (_cues[middle].StartSeconds <= seconds)
                {
                    low = middle + 1;
                }
                else
                {
                    high = middle;
                }
            }

            List<string> active = new List<string>();
            for (int index = Math.Max(0, low - 8); index < low; index++)
            {
                SubtitleCue cue = _cues[index];
                if (cue.StartSeconds <= seconds && cue.EndSeconds > seconds)
                {
                    active.Add(cue.Text);
                }
            }
            return string.Join("\n", active.Distinct());
        }
    }

    public static class SubtitleParser
    {
        private static readonly Regex HtmlTagPattern = new Regex(
            "<[^>]+>",
            RegexOptions.Compiled);
        private static readonly Regex AssOverridePattern = new Regex(
            "\\{[^}]*\\}",
            RegexOptions.Compiled);

        public static SubtitleTrack Parse(string content, string codec = null)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return new SubtitleTrack(null);
            }

            string normalized = content.TrimStart('\uFEFF')
                .Replace("\r\n", "\n")
                .Replace('\r', '\n');
            string format = PlaybackCapabilities.NormalizeCodec(codec);
            bool ass = format == "ass"
                || format == "ssa"
                || normalized.IndexOf("[Events]", StringComparison.OrdinalIgnoreCase) >= 0;
            return new SubtitleTrack(ass
                ? ParseAss(normalized)
                : ParseTimedText(normalized));
        }

        private static IEnumerable<SubtitleCue> ParseTimedText(string content)
        {
            string[] blocks = Regex.Split(content, "\\n[ \\t]*\\n");
            foreach (string block in blocks)
            {
                string[] lines = block.Split('\n');
                int timingIndex = Array.FindIndex(
                    lines,
                    line => line.IndexOf("-->", StringComparison.Ordinal) >= 0);
                if (timingIndex < 0)
                {
                    continue;
                }

                string[] timing = lines[timingIndex].Split(
                    new[] { "-->" },
                    StringSplitOptions.None);
                if (timing.Length != 2)
                {
                    continue;
                }

                double start;
                double end;
                string endValue = timing[1].Trim().Split(' ')[0];
                if (!TryParseTimestamp(timing[0].Trim(), out start)
                    || !TryParseTimestamp(endValue, out end)
                    || end <= start)
                {
                    continue;
                }

                string text = CleanTimedText(string.Join(
                    "\n",
                    lines.Skip(timingIndex + 1).ToArray()));
                if (!string.IsNullOrWhiteSpace(text))
                {
                    yield return new SubtitleCue
                    {
                        StartSeconds = start,
                        EndSeconds = end,
                        Text = text
                    };
                }
            }
        }

        private static IEnumerable<SubtitleCue> ParseAss(string content)
        {
            string[] lines = content.Split('\n');
            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();
                if (!line.StartsWith("Dialogue:", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string[] fields = line.Substring(line.IndexOf(':') + 1)
                    .Split(new[] { ',' }, 10, StringSplitOptions.None);
                if (fields.Length < 10)
                {
                    continue;
                }

                double start;
                double end;
                if (!TryParseTimestamp(fields[1].Trim(), out start)
                    || !TryParseTimestamp(fields[2].Trim(), out end)
                    || end <= start)
                {
                    continue;
                }

                string text = AssOverridePattern.Replace(fields[9], string.Empty)
                    .Replace("\\N", "\n")
                    .Replace("\\n", "\n")
                    .Replace("\\h", " ")
                    .Trim();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    yield return new SubtitleCue
                    {
                        StartSeconds = start,
                        EndSeconds = end,
                        Text = WebUtility.HtmlDecode(text)
                    };
                }
            }
        }

        private static bool TryParseTimestamp(string value, out double seconds)
        {
            seconds = 0d;
            string[] parts = (value ?? string.Empty)
                .Trim()
                .Replace(',', '.')
                .Split(':');
            if (parts.Length < 2 || parts.Length > 3)
            {
                return false;
            }

            double parsedSeconds;
            int minutes;
            int hours = 0;
            if (!double.TryParse(
                    parts[parts.Length - 1],
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out parsedSeconds)
                || !int.TryParse(parts[parts.Length - 2], out minutes))
            {
                return false;
            }
            if (parts.Length == 3 && !int.TryParse(parts[0], out hours))
            {
                return false;
            }
            seconds = hours * 3600d + minutes * 60d + parsedSeconds;
            return seconds >= 0d;
        }

        private static string CleanTimedText(string value)
        {
            string withBreaks = Regex.Replace(
                value ?? string.Empty,
                "<br\\s*/?>",
                "\n",
                RegexOptions.IgnoreCase);
            string decoded = WebUtility.HtmlDecode(HtmlTagPattern.Replace(withBreaks, string.Empty));
            return string.Join(
                "\n",
                decoded.Split('\n')
                    .Select(line => line.Trim())
                    .Where(line => !string.IsNullOrEmpty(line)))
                .Trim();
        }
    }
}
