using System.Globalization;
using System.Text.RegularExpressions;
using AngleSharp;
using AngleSharp.Dom;
using DXPeditions.Core.Announcements;

namespace DXPeditions.Core.Scraping;

/// <summary>
/// Scrapes https://www.qsl.net/va3rj/dx_cal.html - a single, unpaginated HTML
/// table (summary="DX Calendar") covering current and upcoming activity. Each
/// row's DXCC cell carries a title attribute of the form "168  (V7) Marshall
/// Islands", giving the DXCC code directly - no name-matching against
/// DxccReference is needed for anything this source covers, which sidesteps
/// the whole aliases.json problem for those rows. Dates are explicit ISO
/// yyyy-MM-dd, sometimes with a trailing "?" marking a tentative end date.
/// </summary>
public sealed partial class Va3rjScraper(HttpFetcher fetcher) : IDxpeditionScraper
{
    private const string Url = "https://www.qsl.net/va3rj/dx_cal.html";

    // "168  (V7) Marshall Islands" - code is usually present, but at least one
    // real row omits it entirely ("   (ZL) New Zealand"), so it's optional here
    // and the name portion is always extracted independently as a name-based
    // resolution fallback for whenever the code is missing.
    [GeneratedRegex(@"^\s*(?:(?<code>\d+)\s*)?\((?<prefix>[^)]*)\)\s*(?<name>.+)$")]
    private static partial Regex DxccTitlePattern();

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
        var table = document.QuerySelector("table[summary='DX Calendar']");
        if (table is null)
        {
            return results;
        }

        foreach (var row in table.QuerySelectorAll("tr"))
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
        var cells = row.QuerySelectorAll("td");
        if (cells.Length < 6)
        {
            // Header row or malformed row - skip rather than throw.
            return null;
        }

        var callsigns = CallsignParser.ExtractCallsigns(cells[0].TextContent);
        if (callsigns.Count == 0)
        {
            return null;
        }

        var dxccTitle = cells[1].GetAttribute("title");
        var (dxccCode, dxccName) = ParseDxccTitle(dxccTitle);

        var start = TryParseDate(cells[3].TextContent);
        var end = TryParseDate(cells[4].TextContent);

        return new DxpeditionAnnouncement
        {
            Source = AnnouncementSource.Va3rj,
            Callsigns = callsigns,
            RawEntityName = dxccName ?? cells[1].TextContent.Trim(),
            ResolvedDxccCode = dxccCode,
            StartDate = start,
            EndDate = end,
            QslVia = null,
            SourceUrl = Url,
            RawInfoText = cells[5].TextContent.Trim(),
        };
    }

    /// <summary>
    /// The DXCC code is usually present but not guaranteed (see
    /// <see cref="DxccTitlePattern"/>'s comment) - the name is extracted
    /// independently so name-based resolution still works when the code is
    /// missing, rather than falling back to the raw, unparsed title text.
    /// </summary>
    private static (int? Code, string? Name) ParseDxccTitle(string? dxccTitle)
    {
        if (string.IsNullOrWhiteSpace(dxccTitle))
        {
            return (null, null);
        }

        var match = DxccTitlePattern().Match(dxccTitle);
        if (!match.Success)
        {
            return (null, null);
        }

        var code = match.Groups["code"].Success &&
                   int.TryParse(match.Groups["code"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedCode)
            ? parsedCode
            : (int?)null;

        return (code, match.Groups["name"].Value.Trim());
    }

    private static DateOnly? TryParseDate(string text) =>
        DateOnly.TryParse(text.Trim().TrimEnd('?'), CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;
}
