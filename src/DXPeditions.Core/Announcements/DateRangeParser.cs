using System.Globalization;
using System.Text.RegularExpressions;

namespace DXPeditions.Core.Announcements;

/// <summary>
/// Best-effort extraction of a (start, end) date range from free-text DXpedition
/// news prose. Site authors phrase dates inconsistently, so this tries a handful
/// of common patterns in order and returns null for either end it can't determine.
/// Callers should treat a null result as "include the announcement anyway" rather
/// than dropping it - see DxpeditionAnnouncement.OverlapsMonth.
/// </summary>
public static partial class DateRangeParser
{
    private static readonly Dictionary<string, int> MonthNames = BuildMonthLookup();

    private static Dictionary<string, int> BuildMonthLookup()
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var m = 1; m <= 12; m++)
        {
            var full = CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(m);
            var abbr = CultureInfo.InvariantCulture.DateTimeFormat.GetAbbreviatedMonthName(m);
            map[full] = m;
            map[abbr] = m;
            map[abbr.TrimEnd('.')] = m;
        }
        return map;
    }

    // "September 23-29, 2026" / "September 23 to 29, 2026"
    [GeneratedRegex(
        @"(?<month>[A-Za-z]+)\.?\s+(?<day1>\d{1,2})(?:st|nd|rd|th)?\s*(?:-|–|—|to)\s*(?<day2>\d{1,2})(?:st|nd|rd|th)?,?\s*(?<year>\d{4})",
        RegexOptions.IgnoreCase)]
    private static partial Regex SameMonthRangeWithYear();

    // "5 to 15 October 2026" / "5-15 Oct 2026"
    [GeneratedRegex(
        @"(?<day1>\d{1,2})(?:st|nd|rd|th)?\s*(?:-|–|—|to)\s*(?<day2>\d{1,2})(?:st|nd|rd|th)?\s+(?<month>[A-Za-z]+)\.?,?\s*(?<year>\d{4})",
        RegexOptions.IgnoreCase)]
    private static partial Regex DayFirstRangeWithYear();

    // "from October 5th to 15th" (no year given - caller supplies the archive year)
    [GeneratedRegex(
        @"from\s+(?<month>[A-Za-z]+)\.?\s+(?<day1>\d{1,2})(?:st|nd|rd|th)?\s+to\s+(?<day2>\d{1,2})(?:st|nd|rd|th)?",
        RegexOptions.IgnoreCase)]
    private static partial Regex FromToNoYear();

    // "QRV until October 15" / "until Oct 15th" - end date only, no start.
    [GeneratedRegex(
        @"until\s+(?<month>[A-Za-z]+)\.?\s+(?<day1>\d{1,2})(?:st|nd|rd|th)?",
        RegexOptions.IgnoreCase)]
    private static partial Regex UntilOnly();

    /// <summary>
    /// Tries each pattern in order against the given text. <paramref name="fallbackYear"/>
    /// is used for patterns that don't capture a year explicitly (assumes the range falls
    /// in the archive month/year being scraped, which is true for the vast majority of
    /// near-term DXpedition announcements).
    /// </summary>
    public static (DateOnly? Start, DateOnly? End) TryParse(string text, int fallbackYear)
    {
        var match = SameMonthRangeWithYear().Match(text);
        if (match.Success && TryResolveMonth(match.Groups["month"].Value, out var month1))
        {
            var year = int.Parse(match.Groups["year"].Value, CultureInfo.InvariantCulture);
            if (TryMakeDate(year, month1, match.Groups["day1"].Value, out var start) &&
                TryMakeDate(year, month1, match.Groups["day2"].Value, out var end))
            {
                return (start, end);
            }
        }

        match = DayFirstRangeWithYear().Match(text);
        if (match.Success && TryResolveMonth(match.Groups["month"].Value, out var month2))
        {
            var year = int.Parse(match.Groups["year"].Value, CultureInfo.InvariantCulture);
            if (TryMakeDate(year, month2, match.Groups["day1"].Value, out var start) &&
                TryMakeDate(year, month2, match.Groups["day2"].Value, out var end))
            {
                return (start, end);
            }
        }

        match = FromToNoYear().Match(text);
        if (match.Success && TryResolveMonth(match.Groups["month"].Value, out var month3))
        {
            if (TryMakeDate(fallbackYear, month3, match.Groups["day1"].Value, out var start) &&
                TryMakeDate(fallbackYear, month3, match.Groups["day2"].Value, out var end))
            {
                return (start, end);
            }
        }

        match = UntilOnly().Match(text);
        if (match.Success && TryResolveMonth(match.Groups["month"].Value, out var month4))
        {
            if (TryMakeDate(fallbackYear, month4, match.Groups["day1"].Value, out var end))
            {
                return (null, end);
            }
        }

        return (null, null);
    }

    private static bool TryResolveMonth(string name, out int month) =>
        MonthNames.TryGetValue(name, out month);

    /// <summary>Public for scrapers that need to resolve a bare month name/abbreviation themselves.</summary>
    public static bool TryResolveMonthName(string name, out int month) =>
        MonthNames.TryGetValue(name, out month);

    private static bool TryMakeDate(int year, int month, string dayText, out DateOnly date)
    {
        date = default;
        if (!int.TryParse(dayText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var day))
        {
            return false;
        }

        if (day < 1 || day > DateTime.DaysInMonth(year, month))
        {
            return false;
        }

        date = new DateOnly(year, month, day);
        return true;
    }
}
