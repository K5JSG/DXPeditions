using System.Text.RegularExpressions;
using AngleSharp;
using AngleSharp.Dom;
using DXPeditions.Core.Announcements;

namespace DXPeditions.Core.Scraping;

/// <summary>
/// Scrapes https://www.425dxn.org/index.php?op=wcal - a single, unpaginated
/// HTML table (Period/Operation/Bulletin columns) covering current and
/// upcoming activity. "Operation" is free text of the shape
/// "CALLSIGN: EntityName PREFIX  -description..."; "Period" uses its own
/// compact non-prose date format, parsed by <see cref="Dxn425DateParser"/>.
/// </summary>
public sealed partial class Dxn425Scraper(HttpFetcher fetcher) : IDxpeditionScraper
{
    private const string Url = "https://www.425dxn.org/index.php?op=wcal";

    [GeneratedRegex(@"^[A-Z0-9]{1,5}$", RegexOptions.IgnoreCase)]
    private static partial Regex BarePrefixToken();

    // The real "entity - description" separator is always preceded by
    // whitespace (the site pads it with several spaces); a dash embedded in an
    // IOTA reference alongside the entity name (e.g. "(NA-067)") never has
    // whitespace immediately before it, so this reliably tells them apart.
    [GeneratedRegex(@"\s-")]
    private static partial Regex DescriptionSeparator();

    public async Task<IReadOnlyList<DxpeditionAnnouncement>> GetAnnouncementsAsync(
        int year, int month, CancellationToken cancellationToken = default)
    {
        var html = await fetcher.GetStringAsync(Url, cancellationToken).ConfigureAwait(false);
        var all = ParseHtml(html, DateOnly.FromDateTime(DateTime.Today));
        return [.. all.Where(a => a.OverlapsMonth(year, month))];
    }

    /// <summary>Parsing logic factored out from the network call so it can be unit tested against fixture HTML.</summary>
    public static IReadOnlyList<DxpeditionAnnouncement> ParseHtml(string html, DateOnly today)
    {
        var context = BrowsingContext.New(Configuration.Default);
        var document = context.OpenAsync(req => req.Content(html)).GetAwaiter().GetResult();

        var results = new List<DxpeditionAnnouncement>();
        var table = document.QuerySelector("table");
        if (table is null)
        {
            return results;
        }

        foreach (var row in table.QuerySelectorAll("tr"))
        {
            var announcement = TryParseRow(row, today);
            if (announcement is not null)
            {
                results.Add(announcement);
            }
        }

        return results;
    }

    private static DxpeditionAnnouncement? TryParseRow(IElement row, DateOnly today)
    {
        var cells = row.QuerySelectorAll("td");
        if (cells.Length < 2)
        {
            return null;
        }

        var operationText = Normalize(cells[1].TextContent);
        var colonIndex = operationText.IndexOf(':');
        if (colonIndex < 0)
        {
            // Doesn't fit the "CALL(S): Entity - description" shape (e.g. the
            // header row itself) - skip rather than guess.
            return null;
        }

        var callsigns = CallsignParser.ExtractCallsigns(operationText[..colonIndex]);
        if (callsigns.Count == 0)
        {
            return null;
        }

        var afterColon = operationText[(colonIndex + 1)..].Trim();
        var separatorMatch = DescriptionSeparator().Match(afterColon);
        var entityAndPrefix = (separatorMatch.Success ? afterColon[..separatorMatch.Index] : afterColon).Trim();
        var rawEntityName = StripTrailingPrefixToken(entityAndPrefix);

        var (start, end) = Dxn425DateParser.TryParse(Normalize(cells[0].TextContent), today);

        return new DxpeditionAnnouncement
        {
            Source = AnnouncementSource.Dxn425,
            Callsigns = callsigns,
            RawEntityName = rawEntityName,
            StartDate = start,
            EndDate = end,
            QslVia = null,
            SourceUrl = Url,
            RawInfoText = afterColon,
        };
    }

    /// <summary>
    /// 425dxn's Operation text duplicates the entity's DXCC prefix right after
    /// the name (e.g. "Nepal 9N", "St Kitts &amp; Nevis V4") - drop a trailing
    /// bare prefix-shaped token (short, all letters/digits) so the remaining
    /// text matches DxccReference's canonical entity names.
    /// </summary>
    private static string StripTrailingPrefixToken(string entityAndPrefix)
    {
        var words = entityAndPrefix.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length > 1 && BarePrefixToken().IsMatch(words[^1]))
        {
            return string.Join(' ', words[..^1]);
        }

        return entityAndPrefix;
    }

    private static string Normalize(string text) =>
        Regex.Replace(text.Replace(' ', ' '), @"\s+", " ").Trim();
}
