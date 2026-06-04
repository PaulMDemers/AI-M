using System.Text;

namespace AIM.Core.Chat;

public static class HiddenMarkupDisplaySanitizer
{
    private static readonly string[] HiddenTags =
    [
        "aim-tools",
        "aim-management"
    ];

    public static string Sanitize(string content)
    {
        var sanitized = content;

        foreach (var tag in HiddenTags)
        {
            sanitized = StripTag(sanitized, tag);
        }

        return sanitized.TrimEnd();
    }

    private static string StripTag(string content, string tagName)
    {
        var openTag = $"<{tagName}";
        var closeTag = $"</{tagName}>";
        var cursor = 0;
        var builder = new StringBuilder(content.Length);

        while (cursor < content.Length)
        {
            var open = content.IndexOf(openTag, cursor, StringComparison.OrdinalIgnoreCase);

            if (open < 0)
            {
                builder.Append(content, cursor, content.Length - cursor);
                break;
            }

            builder.Append(content, cursor, open - cursor);
            var openEnd = content.IndexOf('>', open);

            if (openEnd < 0)
            {
                break;
            }

            var close = content.IndexOf(closeTag, openEnd + 1, StringComparison.OrdinalIgnoreCase);

            if (close < 0)
            {
                break;
            }

            cursor = close + closeTag.Length;
        }

        return builder.ToString();
    }
}
