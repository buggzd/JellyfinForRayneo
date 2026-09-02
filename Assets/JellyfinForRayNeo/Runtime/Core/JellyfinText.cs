using System.Net;
using System.Text.RegularExpressions;

namespace JellyfinForRayNeo
{
    public static class JellyfinText
    {
        private static readonly Regex BreakTags = new Regex(
            @"<\s*br\s*/?\s*>",
            RegexOptions.IgnoreCase);

        private static readonly Regex BlockEndTags = new Regex(
            @"</\s*(p|div|li|h[1-6])\s*>",
            RegexOptions.IgnoreCase);

        private static readonly Regex ListItemTags = new Regex(
            @"<\s*li(?:\s+[^>]*)?>",
            RegexOptions.IgnoreCase);

        private static readonly Regex AnyTag = new Regex(
            @"<[^>]+>",
            RegexOptions.IgnoreCase);

        private static readonly Regex SpaceBeforeLineBreak = new Regex(@"[ \t]+\n");
        private static readonly Regex SpaceAfterLineBreak = new Regex(@"\n[ \t]+");
        private static readonly Regex ExcessBlankLines = new Regex(@"\n{3,}");

        public static string ToPlainText(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string text = WebUtility.HtmlDecode(value)
                .Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .Replace('\u00a0', ' ');
            text = BreakTags.Replace(text, "\n");
            text = ListItemTags.Replace(text, "• ");
            text = BlockEndTags.Replace(text, "\n");
            text = AnyTag.Replace(text, string.Empty);
            text = SpaceBeforeLineBreak.Replace(text, "\n");
            text = SpaceAfterLineBreak.Replace(text, "\n");
            text = ExcessBlankLines.Replace(text, "\n\n");
            return text.Trim();
        }
    }
}
