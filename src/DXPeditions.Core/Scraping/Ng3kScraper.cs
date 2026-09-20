using System.Globalization;
using System.Text.RegularExpressions;
using AngleSharp;
using AngleSharp.Dom;
using DXPeditions.Core.Announcements;

namespace DXPeditions.Core.Scraping;

/// <summary>
/// Scrapes https://www.ng3k.com/misc/adxo.html - a single HTML table (row class
/// "adxoitem") covering many months at once, with each row's Start/End date cells
/// already carrying the full year ("2026 Aug25"), so no separate month/year
/// tracking from the table's visual section headers is needed.
/// </summary>
public sealed partial class Ng3kScraper(HttpFetcher fetcher) : IDxpeditionScraper
{
    private const string Url = "https://www.ng3k.com/misc/adxo.html";

    [GeneratedRegex(@"^(?<year>\d{4})\s+(?<mon>[A-Za-z]{3})(?<day>\d{1,2})$")]
    private static partial Regex DatePattern();

    public async Task<IReadOnlyList<DxpeditionAnnouncement>> GetAnnouncementsAsync(
        int year, int month, CancellationToken cancellationToken = default)
    {
        var html = await fetcher.GetStringAsync(Url, cancellationToken).ConfigureAwait(false);
        var all = ParseHtml(html);
        return [.. all.Where(a => a.OverlapsMonth(year, month))];
    }

    /// <summary>Parsing logic factored out from the network call so it can be unit tested against fixture HTML.</summary>
    public static IReadOnlyList<DxpeditionAnnouncement> ParseHtml(string html)
    {
        var context = BrowsingContext.New(Configuration.Default);
        var document = context.OpenAsync(req => req.Content(html)).GetAwaiter().GetResult();

        var results = new List<DxpeditionAnnouncement>();

        foreach (var row in document.QuerySelectorAll("tr.adxoitem"))
        {
            var announcement = TryParseRow(row);
            if (announcement is not null)
            {
                results.Add(announcement);
            }
        }

        return results;
    }

    private static DxpeditionAnnouncement? TryParseRow(IElement row)
    {
        var dateCells = row.QuerySelectorAll("td.date");
        var ctyCell = row.QuerySelector("td.cty");
        var qslCell = row.QuerySelector("td.qsl");
        var infoCell = row.QuerySelector("td.info");
        var callSpan = row.QuerySelector("span.call");

        if (ctyCell is null || callSpan is null || dateCells.Length < 2)
        {
            // Not a well-formed data row - skip rather than throw, per the
            // "skip unparseable rows" resilience requirement.
            return null;
        }

        var rawInfoText = infoCell?.TextContent.Trim();
        var callsigns = CallsignParser.ExtractCallsigns(callSpan.TextContent)
            .Concat(CallsignParser.ExtractMentionedAs(rawInfoText))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (callsigns.Count == 0)
        {
            return null;
        }

        var start = TryParseDate(dateCells[0].TextContent);
        var end = TryParseDate(dateCells[1].TextContent);

        return new DxpeditionAnnouncement
        {
            Source = AnnouncementSource.Ng3k,
            Callsigns = callsigns,
            RawEntityName = ctyCell.TextContent.Trim(),
            StartDate = start,
            EndDate = end,
            QslVia = qslCell?.TextContent.Trim(),
            SourceUrl = Url,
            RawInfoText = rawInfoText,
        };
    }

    private static DateOnly? TryParseDate(string text)
    {
        var match = DatePattern().Match(text.Trim());
        if (!match.Success)
        {
            return null;
        }

        if (!DateRangeParser.TryResolveMonthName(match.Groups["mon"].Value, out var month))
        {
            return null;
        }

        var year = int.Parse(match.Groups["year"].Value, CultureInfo.InvariantCulture);
        var day = int.Parse(match.Groups["day"].Value, CultureInfo.InvariantCulture);

        if (day < 1 || day > DateTime.DaysInMonth(year, month))
        {
            return null;
        }

        return new DateOnly(year, month, day);
    }
}
