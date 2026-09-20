using System.Globalization;
using AngleSharp;
using DXPeditions.Core.Announcements;

namespace DXPeditions.Core.Scraping;

/// <summary>
/// Scrapes ham365.net's Dx-peditions calendar. The visible page is a JS shell;
/// the real data comes from POSTing month/year/options to
/// /DxPeditionsPlan/DxPeditionsPartialView, which returns a table: row 0 is
/// entity/IOTA-name headers, row 1 is the matching callsign per column (same
/// column index, no leading/trailing label cell), then one row per
/// day-of-month with a non-empty cell marking that dxpedition active that
/// day. There are no explicit dates, so start/end are inferred from which
/// days are active; an activity touching day 1 or the month's last day means
/// it may extend into the neighboring month, so that neighbor is fetched too
/// and stitched together by matching callsign (column order isn't stable
/// across separate month requests).
/// </summary>
public sealed class Ham365Scraper(HttpFetcher fetcher) : IDxpeditionScraper
{
    private const string PlanUrl = "https://www.ham365.net/DxPeditionsPlan/DxPeditionsPartialView";
    private const string PageUrl = "https://www.ham365.net/Dxpeditions";

    /// <summary>One column of the grid: a callsign, its raw entity/IOTA text, and the days of that queried month it's active.</summary>
    public sealed record ColumnActivity(string Callsign, string RawEntityName, IReadOnlyList<int> ActiveDays);

    public async Task<IReadOnlyList<DxpeditionAnnouncement>> GetAnnouncementsAsync(
        int year, int month, CancellationToken cancellationToken = default)
    {
        var target = new DateOnly(year, month, 1);
        var previous = target.AddMonths(-1);
        var next = target.AddMonths(1);

        var targetColumns = ParseGrid(await FetchMonthAsync(target.Year, target.Month, cancellationToken).ConfigureAwait(false));
        if (targetColumns.Count == 0)
        {
            return [];
        }

        var daysInTargetMonth = DateTime.DaysInMonth(target.Year, target.Month);
        var needsPrevious = targetColumns.Any(c => c.ActiveDays.Contains(1));
        var needsNext = targetColumns.Any(c => c.ActiveDays.Contains(daysInTargetMonth));

        var previousColumns = needsPrevious
            ? ParseGrid(await FetchMonthAsync(previous.Year, previous.Month, cancellationToken).ConfigureAwait(false))
            : [];
        var nextColumns = needsNext
            ? ParseGrid(await FetchMonthAsync(next.Year, next.Month, cancellationToken).ConfigureAwait(false))
            : [];

        var results = BuildAnnouncements(target, targetColumns, previousColumns, nextColumns);
        return [.. results.Where(a => a.OverlapsMonth(year, month))];
    }

    /// <summary>
    /// Stitching logic factored out from the network calls so it can be unit
    /// tested directly against constructed <see cref="ColumnActivity"/> lists,
    /// without needing to fake three separate HTTP POSTs.
    /// </summary>
    public static IReadOnlyList<DxpeditionAnnouncement> BuildAnnouncements(
        DateOnly target,
        IReadOnlyList<ColumnActivity> targetColumns,
        IReadOnlyList<ColumnActivity> previousColumns,
        IReadOnlyList<ColumnActivity> nextColumns)
    {
        var previous = target.AddMonths(-1);
        var next = target.AddMonths(1);
        var daysInTargetMonth = DateTime.DaysInMonth(target.Year, target.Month);

        var results = new List<DxpeditionAnnouncement>();
        foreach (var column in targetColumns)
        {
            var callsigns = CallsignParser.ExtractCallsigns(column.Callsign.Replace('-', ','));
            if (callsigns.Count == 0)
            {
                continue;
            }

            var startDay = column.ActiveDays.Min();
            var endDay = column.ActiveDays.Max();

            var start = startDay == 1
                ? ResolveBoundaryStart(column.Callsign, target, previous, previousColumns)
                : new DateOnly(target.Year, target.Month, startDay);

            var end = endDay == daysInTargetMonth
                ? ResolveBoundaryEnd(column.Callsign, target, daysInTargetMonth, next, nextColumns)
                : new DateOnly(target.Year, target.Month, endDay);

            results.Add(new DxpeditionAnnouncement
            {
                Source = AnnouncementSource.Ham365,
                Callsigns = callsigns,
                RawEntityName = column.RawEntityName,
                StartDate = start,
                EndDate = end,
                QslVia = null,
                SourceUrl = PageUrl,
                RawInfoText = null,
            });
        }

        return results;
    }

    private static DateOnly ResolveBoundaryStart(string callsign, DateOnly target, DateOnly previousMonth, IReadOnlyList<ColumnActivity> previousColumns)
    {
        var match = previousColumns.FirstOrDefault(c => c.Callsign.Equals(callsign, StringComparison.OrdinalIgnoreCase));
        return match is not null
            ? new DateOnly(previousMonth.Year, previousMonth.Month, match.ActiveDays.Min())
            : new DateOnly(target.Year, target.Month, 1);
    }

    private static DateOnly ResolveBoundaryEnd(string callsign, DateOnly target, int daysInTargetMonth, DateOnly nextMonth, IReadOnlyList<ColumnActivity> nextColumns)
    {
        var match = nextColumns.FirstOrDefault(c => c.Callsign.Equals(callsign, StringComparison.OrdinalIgnoreCase));
        return match is not null
            ? new DateOnly(nextMonth.Year, nextMonth.Month, match.ActiveDays.Max())
            : new DateOnly(target.Year, target.Month, daysInTargetMonth);
    }

    private async Task<string> FetchMonthAsync(int year, int month, CancellationToken cancellationToken)
    {
        var fields = new Dictionary<string, string>
        {
            ["month"] = month.ToString(CultureInfo.InvariantCulture),
            ["year"] = year.ToString(CultureInfo.InvariantCulture),
            ["options"] = "V*", // vertical layout, "all" dxpeditions (not just ones active today) - confirmed via the site's own JS defaults.
        };

        return await fetcher.PostFormAsync(PlanUrl, fields, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Parsing logic factored out from the network call so it can be unit tested against fixture HTML.</summary>
    public static IReadOnlyList<ColumnActivity> ParseGrid(string html)
    {
        var context = BrowsingContext.New(Configuration.Default);
        var document = context.OpenAsync(req => req.Content(html)).GetAwaiter().GetResult();

        var table = document.QuerySelector("table");
        if (table is null)
        {
            return [];
        }

        var rows = table.QuerySelectorAll("tr");
        if (rows.Length < 2)
        {
            return [];
        }

        var headerCells = rows[0].QuerySelectorAll("td");
        var callsignCells = rows[1].QuerySelectorAll("td");
        var columnCount = callsignCells.Length;
        if (columnCount == 0 || headerCells.Length < columnCount + 1)
        {
            return [];
        }

        var names = new string[columnCount];
        var callsigns = new string[columnCount];
        var activeDaysByColumn = new List<int>[columnCount];
        for (var i = 0; i < columnCount; i++)
        {
            names[i] = headerCells[i + 1].TextContent.Trim();
            callsigns[i] = callsignCells[i].TextContent.Trim();
            activeDaysByColumn[i] = [];
        }

        for (var r = 2; r < rows.Length; r++)
        {
            var cells = rows[r].QuerySelectorAll("td");
            if (cells.Length < columnCount + 1 || !int.TryParse(cells[0].TextContent.Trim(), out var day))
            {
                continue; // Not a day-data row (e.g. the "color"/"DAY" header rows) - skip.
            }

            for (var i = 0; i < columnCount; i++)
            {
                if (!string.IsNullOrWhiteSpace(cells[i + 1].TextContent))
                {
                    activeDaysByColumn[i].Add(day);
                }
            }
        }

        var results = new List<ColumnActivity>();
        for (var i = 0; i < columnCount; i++)
        {
            if (string.IsNullOrWhiteSpace(callsigns[i]) || activeDaysByColumn[i].Count == 0)
            {
                continue;
            }

            results.Add(new ColumnActivity(callsigns[i], names[i], activeDaysByColumn[i]));
        }

        return results;
    }
}
