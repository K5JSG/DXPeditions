using System.Text.RegularExpressions;
using AngleSharp;
using AngleSharp.Dom;
using DXPeditions.Core.Announcements;

namespace DXPeditions.Core.Scraping;

/// <summary>
/// Scrapes https://www.dx-world.net/ via its monthly archive URLs
/// (confirmed working: https://www.dx-world.net/YYYY/MM/, paginated /page/N/).
/// Archive pages only show excerpts, so an accurate date range requires fetching
/// each article's own page. Article titles reliably follow "CALLSIGN(S) – Entity"
/// (confirmed against live posts), which is the primary, more reliable path for
/// callsign + entity extraction; the article body is used only for the date range.
/// Also checks the previous month's archive for carryover announcements whose
/// range extends into the target month, per the agreed month-scope semantics.
/// </summary>
public sealed partial class DxWorldScraper(HttpFetcher fetcher, DxWorldArticleCache? articleCache = null) : IDxpeditionScraper
{
    [GeneratedRegex(@"^(?<calls>[A-Za-z0-9/,&\s]+?)\s*[–—-]\s*(?<entity>.+)$")]
    private static partial Regex TitleRegex();

    private readonly DxWorldArticleCache _articleCache = articleCache ?? DxWorldArticleCache.Disabled();

    public Task<IReadOnlyList<DxpeditionAnnouncement>> GetAnnouncementsAsync(
        int year, int month, CancellationToken cancellationToken = default) =>
        GetAnnouncementsAsync(year, month, crossReference: null, cancellationToken);

    /// <summary>
    /// Same as the interface method, but takes a callsign -> dates/DXCC-code
    /// index built from the cheap, single-fetch sources (ng3k/va3rj/ham365/
    /// 425dxn). dx-world's per-article fetch is skipped entirely whenever a
    /// teaser's callsign is already covered there - dx-world is the last
    /// resort, only paid for when nothing cheaper already has the dates.
    /// </summary>
    public async Task<IReadOnlyList<DxpeditionAnnouncement>> GetAnnouncementsAsync(
        int year, int month, IReadOnlyDictionary<string, CrossReferenceEntry>? crossReference, CancellationToken cancellationToken = default)
    {
        var results = new List<DxpeditionAnnouncement>();

        foreach (var (y, m) in MonthsToCrawl(year, month))
        {
            await CrawlMonthAsync(y, m, results, crossReference, cancellationToken).ConfigureAwait(false);
        }

        _articleCache.Save();

        return [.. results.Where(a => a.OverlapsMonth(year, month))];
    }

    private static IEnumerable<(int Year, int Month)> MonthsToCrawl(int year, int month)
    {
        yield return (year, month);
        var previous = new DateOnly(year, month, 1).AddMonths(-1);
        yield return (previous.Year, previous.Month);
    }

    private async Task CrawlMonthAsync(
        int year, int month, List<DxpeditionAnnouncement> results,
        IReadOnlyDictionary<string, CrossReferenceEntry>? crossReference, CancellationToken cancellationToken)
    {
        var baseUrl = $"https://www.dx-world.net/{year:D4}/{month:D2}/";
        var firstPageHtml = await fetcher.TryGetStringAsync(baseUrl, cancellationToken).ConfigureAwait(false);
        if (firstPageHtml is null)
        {
            return; // No archive for this month (e.g. before the site existed) - skip.
        }

        var (teasers, lastPage) = ParseArchivePage(firstPageHtml, baseUrl);
        var allTeasers = new List<ArticleTeaser>(teasers);

        for (var page = 2; page <= lastPage; page++)
        {
            await HttpFetcher.PoliteDelay(cancellationToken).ConfigureAwait(false);
            var pageHtml = await fetcher.TryGetStringAsync($"{baseUrl}page/{page}/", cancellationToken).ConfigureAwait(false);
            if (pageHtml is null)
            {
                break;
            }

            allTeasers.AddRange(ParseArchivePage(pageHtml, baseUrl).Teasers);
        }

        foreach (var teaser in allTeasers)
        {
            var titleMatch = ParseTitle(teaser.Title);
            if (titleMatch is null)
            {
                continue; // Doesn't fit the "CALL(S) - Entity" shape - skip rather than guess.
            }

            var crossReferenced = TryFindCrossReference(titleMatch.Value.Callsigns, crossReference);
            if (crossReferenced is not null)
            {
                // Already covered by a cheap, single-fetch source - skip the
                // per-article fetch entirely and just take its dates/code.
                results.Add(new DxpeditionAnnouncement
                {
                    Source = AnnouncementSource.DxWorld,
                    Callsigns = titleMatch.Value.Callsigns,
                    RawEntityName = titleMatch.Value.EntityName,
                    ResolvedDxccCode = crossReferenced.DxccCode,
                    StartDate = crossReferenced.StartDate,
                    EndDate = crossReferenced.EndDate,
                    QslVia = null,
                    SourceUrl = teaser.Url,
                    RawInfoText = null,
                });
                continue;
            }

            var cachedBody = _articleCache.TryGetBody(teaser.Url);
            string bodyText;
            if (cachedBody is not null)
            {
                bodyText = cachedBody;
            }
            else
            {
                await HttpFetcher.PoliteDelay(cancellationToken).ConfigureAwait(false);
                var articleHtml = await fetcher.TryGetStringAsync(teaser.Url, cancellationToken).ConfigureAwait(false);
                bodyText = articleHtml is not null ? ExtractArticleText(articleHtml) : string.Empty;
                _articleCache.Store(teaser.Url, bodyText, year, month);
            }

            var (start, end) = DateRangeParser.TryParse(bodyText, year);

            var callsigns = titleMatch.Value.Callsigns
                .Concat(CallsignParser.ExtractMentionedAs(bodyText))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            results.Add(new DxpeditionAnnouncement
            {
                Source = AnnouncementSource.DxWorld,
                Callsigns = callsigns,
                RawEntityName = titleMatch.Value.EntityName,
                StartDate = start,
                EndDate = end,
                QslVia = null,
                SourceUrl = teaser.Url,
                RawInfoText = bodyText,
            });
        }

        _articleCache.PruneMonth(year, month, allTeasers.Select(t => t.Url).ToHashSet());
    }

    /// <summary>Public for unit testing the skip-fetch decision in isolation.</summary>
    public static CrossReferenceEntry? TryFindCrossReference(
        IReadOnlyList<string> callsigns, IReadOnlyDictionary<string, CrossReferenceEntry>? crossReference)
    {
        if (crossReference is null)
        {
            return null;
        }

        foreach (var callsign in callsigns)
        {
            if (crossReference.TryGetValue(callsign, out var entry) && entry.HasUsableDates)
            {
                return entry;
            }
        }

        return null;
    }

    private readonly record struct ArticleTeaser(string Title, string Url);

    /// <summary>Parsing logic factored out from the network call so it can be unit tested against fixture HTML.</summary>
    public static (IReadOnlyList<(string Title, string Url)> Teasers, int LastPage) ParseArchivePageForTest(string html, string baseUrl)
    {
        var (teasers, lastPage) = ParseArchivePage(html, baseUrl);
        return ([.. teasers.Select(t => (t.Title, t.Url))], lastPage);
    }

    private static (List<ArticleTeaser> Teasers, int LastPage) ParseArchivePage(string html, string baseUrl)
    {
        var context = BrowsingContext.New(Configuration.Default);
        var document = context.OpenAsync(req => req.Content(html)).GetAwaiter().GetResult();

        var teasers = new List<ArticleTeaser>();
        foreach (var article in document.QuerySelectorAll("article"))
        {
            var titleLink = article.QuerySelector("h2.entry-title a, .post-title a");
            if (titleLink is null)
            {
                continue;
            }

            var url = titleLink.GetAttribute("href");
            var title = titleLink.TextContent.Trim();
            if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(title))
            {
                continue;
            }

            teasers.Add(new ArticleTeaser(title, url));
        }

        var lastPage = 1;
        foreach (var pageLink in document.QuerySelectorAll("a.page-numbers"))
        {
            if (int.TryParse(pageLink.TextContent.Trim(), out var pageNumber) && pageNumber > lastPage)
            {
                lastPage = pageNumber;
            }
        }

        return (teasers, lastPage);
    }

    /// <summary>Public for unit testing the title-splitting heuristic in isolation.</summary>
    public static (IReadOnlyList<string> Callsigns, string EntityName)? ParseTitle(string title)
    {
        var match = TitleRegex().Match(title);
        if (!match.Success)
        {
            return null;
        }

        var callsigns = CallsignParser.ExtractCallsigns(match.Groups["calls"].Value);
        if (callsigns.Count == 0)
        {
            return null;
        }

        return (callsigns, match.Groups["entity"].Value.Trim());
    }

    /// <summary>Public for unit testing against saved fixture HTML.</summary>
    public static string ExtractArticleText(string articleHtml)
    {
        var context = BrowsingContext.New(Configuration.Default);
        var document = context.OpenAsync(req => req.Content(articleHtml)).GetAwaiter().GetResult();

        var content = document.QuerySelector(".entry-content");
        return content?.TextContent.Trim() ?? document.Body?.TextContent.Trim() ?? string.Empty;
    }
}
