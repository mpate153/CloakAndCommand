using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>
/// Subset of Markdown → TextMeshPro rich text (headings, lists, bold/italic, inline code, links, blockquotes).
/// </summary>
public static class CreditsMarkdown
{
    static readonly Regex RxHeading = new Regex(@"^(#{1,6})\s+(.+?)\s*#*\s*$", RegexOptions.Compiled);
    static readonly Regex RxBoldDouble = new Regex(@"\*\*(.+?)\*\*", RegexOptions.Compiled);
    static readonly Regex RxBoldUnder = new Regex(@"__(.+?)__", RegexOptions.Compiled);
    static readonly Regex RxCode = new Regex(@"`([^`]+)`", RegexOptions.Compiled);
    static readonly Regex RxLink = new Regex(@"\[([^\]]+)\]\(([^)]+)\)", RegexOptions.Compiled);
    static readonly Regex RxItalicStar = new Regex(@"(?<!\*)\*(?!\*)([^*]+?)(?<!\*)\*(?!\*)", RegexOptions.Compiled);
    static readonly Regex RxItalicUnder = new Regex(@"(?<![\w])_([^_\n]+)_(?![\w])", RegexOptions.Compiled);

    /// <summary>
    /// Heading sizes as % of the TextMeshPro default <c>fontSize</c>. Absolute <c>&lt;size=36&gt;</c> is smaller than
    /// body text when the component font size is larger than 36 — percentages avoid that.
    /// </summary>
    static readonly string[] HeadingSizePercents = { "160%", "145%", "130%", "118%", "112%", "106%" };

    public static string ToTmp(string markdown)
    {
        if (string.IsNullOrEmpty(markdown)) return markdown;

        var sb = new StringBuilder(markdown.Length * 2);
        foreach (var segment in markdown.Split('\n'))
        {
            var line = segment.TrimEnd('\r');
            var trimmed = line.Trim();

            if (trimmed.Length == 0)
            {
                sb.Append('\n');
                continue;
            }

            var m = RxHeading.Match(trimmed);
            if (m.Success)
            {
                int level = m.Groups[1].Value.Length;
                string title = m.Groups[2].Value.Trim();
                AppendHeading(sb, ApplyInline(title), level);
                continue;
            }

            if (IsHorizontalRule(trimmed))
            {
                sb.Append('\n');
                continue;
            }

            var leading = line.Length - line.TrimStart().Length;
            var indent = line.Substring(0, leading);
            var content = line.TrimStart();

            if (content.StartsWith("> "))
            {
                sb.Append(indent).Append("<i>").Append(ApplyInline(content.Substring(2))).Append("</i>\n");
                continue;
            }

            if (content.StartsWith("- ") || content.StartsWith("* "))
            {
                sb.Append(indent).Append("• ").Append(ApplyInline(content.Substring(2))).Append('\n');
                continue;
            }

            sb.Append(ApplyInline(line)).Append('\n');
        }

        return sb.ToString();
    }

    static bool IsHorizontalRule(string trimmed)
    {
        if (trimmed.Length < 3) return false;
        return trimmed.All(c => c == '-' || c == '*' || c == '_');
    }

    static void AppendHeading(StringBuilder sb, string text, int level)
    {
        level = Mathf.Clamp(level, 1, 6);
        string pct = HeadingSizePercents[level - 1];
        sb.Append("<size=").Append(pct).Append("><b>").Append(text).Append("</b></size>\n");
    }

    /// <summary>Inline **bold**, *italic*, __bold__, _italic_, `code`, [label](url).</summary>
    public static string ApplyInline(string line)
    {
        if (string.IsNullOrEmpty(line)) return line;

        line = RxCode.Replace(line, m => $"<color=#BBBBBB>{EscapeTmpLiteral(m.Groups[1].Value)}</color>");
        line = RxLink.Replace(line, m =>
            $"<color=#6CA6E8><u>{EscapeTmpLiteral(m.Groups[1].Value)}</u></color>");
        line = RxBoldDouble.Replace(line, m => $"<b>{EscapeTmpLiteral(m.Groups[1].Value)}</b>");
        line = RxBoldUnder.Replace(line, m => $"<b>{EscapeTmpLiteral(m.Groups[1].Value)}</b>");
        line = RxItalicStar.Replace(line, m => $"<i>{EscapeTmpLiteral(m.Groups[1].Value)}</i>");
        line = RxItalicUnder.Replace(line, m => $"<i>{EscapeTmpLiteral(m.Groups[1].Value)}</i>");
        return line;
    }

    static string EscapeTmpLiteral(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return s.Replace("<", "\\<");
    }
}
