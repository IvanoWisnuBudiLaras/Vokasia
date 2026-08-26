using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace Vokasia.Api.Validation;

/// <summary>
/// Sanitizer HTML (Quill.js) aman dari XSS injection.
/// Pendekatan: extract tag yang diizinkan + text, buang atribut berbahaya / strip atribut ke format bersih.
/// Tag yang diizinkan: p, br, b, strong, i, em, u, s, strike, h1, h2, ul, ol, li.
/// </summary>
public static class TextSanitizer
{
    private static readonly Regex Tokenizer = new(
        @"(<[^>]+>)|([^<]+)",
        RegexOptions.Compiled);

    private static readonly HashSet<string> AllowedTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "p", "br", "b", "strong", "i", "em", "u", "s", "strike",
        "h1", "h2", "ul", "ol", "li"
    };

    public static string Clean(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        // Pre-strip dangerous executable/meta blocks entirely (script, style, iframe, etc.)
        var stripped = DangerousBlockRegex.Replace(input, string.Empty);

        var result = new StringBuilder();
        foreach (Match match in Tokenizer.Matches(stripped))
        {
            if (match.Groups[2].Success)
            {
                // Plain text — keep as-is
                result.Append(match.Value);
                continue;
            }

            // Tag token — sanitize tag and keep if allowed
            var rawTag = match.Groups[1].Value;
            if (TrySanitizeTag(rawTag, out var cleanTag))
            {
                result.Append(cleanTag);
            }
        }

        return result.ToString().Trim();
    }

    public static string ToPlainText(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var cleaned = DangerousBlockRegex.Replace(input, string.Empty);
        var stripped = TagRegex.Replace(cleaned, " ");
        var decoded = HttpUtility.HtmlDecode(stripped);
        return WhitespaceRegex.Replace(decoded, " ").Trim();
    }

    private static bool TrySanitizeTag(string tag, out string cleanTag)
    {
        cleanTag = string.Empty;

        // Extract tag name & closing slash
        var match = TagStructureRegex.Match(tag);
        if (!match.Success) return false;

        var isClosing = match.Groups[1].Value == "/";
        var tagName = match.Groups[2].Value.ToLowerInvariant();

        if (!AllowedTags.Contains(tagName)) return false;

        if (isClosing)
        {
            cleanTag = $"</{tagName}>";
            return true;
        }

        // Void elements like <br>
        if (tagName == "br")
        {
            cleanTag = "<br>";
            return true;
        }

        // For all other allowed tags, strip any dangerous attributes (keep only clean tag)
        cleanTag = $"<{tagName}>";
        return true;
    }

    // Regexes
    private static readonly Regex DangerousBlockRegex = new(
        @"<(script|style|iframe|object|embed|applet|form|input|textarea|select|button|link|meta|base)\b[^>]*>[\s\S]*?</\1\s*>|<(script|style|iframe|object|embed|applet|form|input|textarea|select|button|link|meta|base)\b[^>]*/?>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex TagStructureRegex = new(
        @"<(/)?([a-zA-Z][a-zA-Z0-9]*)",
        RegexOptions.Compiled);

    private static readonly Regex TagRegex = new(
        @"<[^>]*>",
        RegexOptions.Compiled);

    private static readonly Regex WhitespaceRegex = new(
        @"\s+",
        RegexOptions.Compiled);
}
