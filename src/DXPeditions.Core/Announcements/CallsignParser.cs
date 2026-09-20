using System.Text.RegularExpressions;

namespace DXPeditions.Core.Announcements;

/// <summary>
/// Splits a raw cell/title fragment (e.g. "N7NU/VP9 &amp; VP9I", "FT4YM", "V51WH")
/// into individual callsigns, filtering out noise tokens ("[spots]", "Read More",
/// etc.) that don't look like a real amateur callsign.
/// </summary>
public static partial class CallsignParser
{
    // Permissive on purpose: real-world callsign shapes vary a lot globally
    // (compound calls, portable suffixes). This is a noise filter, not a strict
    // validator - false positives (an odd token that happens to look callsign-ish)
    // are far less costly here than false negatives (silently dropping a real call),
    // since the output regex is reviewed by the user before use.
    [GeneratedRegex(@"^[A-Z0-9]{2,15}(?:/[A-Z0-9]{1,6}){0,2}$", RegexOptions.IgnoreCase)]
    private static partial Regex CallsignShapeRegex();

    // Free-text DXpedition descriptions (ng3k's "Info" column, dx-world.net article
    // bodies) routinely name the actual operating callsign this way even when the
    // Call column/title only gives a generic country prefix, e.g. "By JA1UII as
    // JD1BON, JI1LET as JD1BOK ... fm Chichijima I" for a row whose Call column is
    // just "JD1". Confirmed against real ng3k text during testing.
    [GeneratedRegex(@"\bas\s+([A-Z0-9][A-Z0-9/]{1,14})\b", RegexOptions.IgnoreCase)]
    private static partial Regex MentionedAsRegex();

    private static readonly char[] Separators = ['&', ','];

    private static readonly string[] NoiseWords =
    [
        "spots", "spot", "read", "more", "and", "via", "qsl", "info", "news", "update"
    ];

    public static IReadOnlyList<string> ExtractCallsigns(string? rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
        {
            return [];
        }

        var candidates = rawText
            .Split(Separators, StringSplitOptions.RemoveEmptyEntries)
            .SelectMany(part => part.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .Select(CleanToken)
            .Where(token => token.Length > 0);

        var results = new List<string>();
        foreach (var token in candidates)
        {
            var upper = token.ToUpperInvariant();

            if (NoiseWords.Contains(upper, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!LooksLikeCallsign(upper))
            {
                continue;
            }

            if (!results.Contains(upper, StringComparer.OrdinalIgnoreCase))
            {
                results.Add(upper);
            }
        }

        return results;
    }

    /// <summary>
    /// Scans free text for "as CALLSIGN" mentions (operator attributions in
    /// DXpedition write-ups) and returns the validated, deduplicated callsigns
    /// found. Used to recover the actual operating call when a source's
    /// dedicated call field only gives a generic country prefix.
    /// </summary>
    public static IReadOnlyList<string> ExtractMentionedAs(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var results = new List<string>();
        foreach (Match match in MentionedAsRegex().Matches(text))
        {
            var upper = match.Groups[1].Value.ToUpperInvariant();
            if (LooksLikeCallsign(upper) && !results.Contains(upper, StringComparer.OrdinalIgnoreCase))
            {
                results.Add(upper);
            }
        }

        return results;
    }

    private static string CleanToken(string token) =>
        token.Trim().Trim('[', ']', '(', ')', '.', ';', ':', '"', '\'');

    /// <summary>
    /// A token looks like a callsign if it matches the general shape AND contains
    /// at least one digit and one letter (rules out pure words and pure numbers).
    /// </summary>
    private static bool LooksLikeCallsign(string token)
    {
        if (!CallsignShapeRegex().IsMatch(token))
        {
            return false;
        }

        var hasDigit = token.Any(char.IsDigit);
        var hasLetter = token.Any(char.IsLetter);
        return hasDigit && hasLetter;
    }
}
