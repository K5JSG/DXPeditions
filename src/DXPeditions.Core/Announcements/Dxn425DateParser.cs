using System.Globalization;
using System.Text.RegularExpressions;

namespace DXPeditions.Core.Announcements;

/// <summary>
/// Parses 425dxn.org's "Period" column - a compact, non-prose format distinct
/// from the free-text prose <see cref="DateRangeParser"/> handles for dx-world.
/// Confirmed live formats: "DD/MM-DD/MM", "till DD/MM" (open start), bare
/// "DD/MM" (single day), and an optional trailing explicit year for far-future
/// overflow ("DD/MM-DD/MM YYYY"). This is a live current-calendar page, not an
/// archived month, so a missing year is inferred relative to "today" rather
/// than an archive year. Anything that doesn't match a known shape (e.g.
/// "till ??/??", "till December") returns (null, null) rather than a guess.
/// </summary>
public static partial class Dxn425DateParser
{
    [GeneratedRegex(@"^(?<d1>\d{1,2})/(?<m1>\d{1,2})\s*-\s*(?<d2>\d{1,2})/(?<m2>\d{1,2})(?:\s+(?<year>\d{4}))?$")]
    private static partial Regex RangeRegex();

    [GeneratedRegex(@"^till\s+(?<d>\d{1,2})/(?<m>\d{1,2})$", RegexOptions.IgnoreCase)]
    private static partial Regex TillRegex();

    [GeneratedRegex(@"^(?<d>\d{1,2})/(?<m>\d{1,2})$")]
    private static partial Regex SingleDayRegex();

    public static (DateOnly? Start, DateOnly? End) TryParse(string text, DateOnly today)
    {
        var trimmed = text.Trim();

        var range = RangeRegex().Match(trimmed);
        if (range.Success)
        {
            var m1 = int.Parse(range.Groups["m1"].Value, CultureInfo.InvariantCulture);
            var d1 = int.Parse(range.Groups["d1"].Value, CultureInfo.InvariantCulture);
            var m2 = int.Parse(range.Groups["m2"].Value, CultureInfo.InvariantCulture);
            var d2 = int.Parse(range.Groups["d2"].Value, CultureInfo.InvariantCulture);

            var startYear = range.Groups["year"].Success
                ? int.Parse(range.Groups["year"].Value, CultureInfo.InvariantCulture)
                : ResolveYear(m1, d1, today);

            if (!TryMakeDate(startYear, m1, d1, out var start))
            {
                return (null, null);
            }

            var endYear = range.Groups["year"].Success
                ? int.Parse(range.Groups["year"].Value, CultureInfo.InvariantCulture)
                : startYear;

            if (!TryMakeDate(endYear, m2, d2, out var end))
            {
                return (null, null);
            }

            if (end < start && !range.Groups["year"].Success)
            {
                // No explicit year and the range crosses a year boundary (e.g. Dec->Jan).
                TryMakeDate(endYear + 1, m2, d2, out end);
            }

            return (start, end);
        }

        var till = TillRegex().Match(trimmed);
        if (till.Success)
        {
            var m = int.Parse(till.Groups["m"].Value, CultureInfo.InvariantCulture);
            var d = int.Parse(till.Groups["d"].Value, CultureInfo.InvariantCulture);
            return TryMakeDate(ResolveYear(m, d, today), m, d, out var end) ? (null, end) : (null, null);
        }

        var single = SingleDayRegex().Match(trimmed);
        if (single.Success)
        {
            var m = int.Parse(single.Groups["m"].Value, CultureInfo.InvariantCulture);
            var d = int.Parse(single.Groups["d"].Value, CultureInfo.InvariantCulture);
            return TryMakeDate(ResolveYear(m, d, today), m, d, out var date) ? (date, date) : (null, null);
        }

        return (null, null);
    }

    /// <summary>
    /// A bare "DD/MM" has no year. Try the current year first; if that date would
    /// already be more than ~60 days in the past relative to today, it almost
    /// certainly refers to next year instead (this is a live "current and
    /// upcoming" calendar, not a historical archive).
    /// </summary>
    private static int ResolveYear(int month, int day, DateOnly today)
    {
        if (!TryMakeDate(today.Year, month, day, out var candidate))
        {
            return today.Year;
        }

        return candidate < today.AddDays(-60) ? today.Year + 1 : today.Year;
    }

    private static bool TryMakeDate(int year, int month, int day, out DateOnly date)
    {
        date = default;
        if (month is < 1 or > 12 || day < 1 || day > DateTime.DaysInMonth(year, month))
        {
            return false;
        }

        date = new DateOnly(year, month, day);
        return true;
    }
}
