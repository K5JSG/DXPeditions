using DXPeditions.Core.Announcements;
using DXPeditions.Core.Scraping;

namespace DXPeditions.Core.Tests.Scraping;

public class DxWorldScraperTests
{
    private static string ArchiveFixtureHtml =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "dxworld_archive_sample.html"));

    private static string ArticleFixtureHtml =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "dxworld_article_sample.html"));

    [Fact]
    public void ParsesSimpleTitleIntoCallsignAndEntity()
    {
        var parsed = DxWorldScraper.ParseTitle("FT4YM – Antarctica");

        Assert.NotNull(parsed);
        Assert.Equal(["FT4YM"], parsed!.Value.Callsigns);
        Assert.Equal("Antarctica", parsed.Value.EntityName);
    }

    [Fact]
    public void ParsesMultiCallsignTitle()
    {
        var parsed = DxWorldScraper.ParseTitle("N7NU/VP9 & VP9I – Bermuda");

        Assert.NotNull(parsed);
        Assert.Equal(["N7NU/VP9", "VP9I"], parsed!.Value.Callsigns);
        Assert.Equal("Bermuda", parsed.Value.EntityName);
    }

    [Fact]
    public void ParsesRealCapturedArchivePage()
    {
        var (teasers, lastPage) = DxWorldScraper.ParseArchivePageForTest(
            ArchiveFixtureHtml, "https://www.dx-world.net/2026/09/");

        Assert.NotEmpty(teasers);
        Assert.Contains(teasers, t => t.Title.Contains("FT4YM"));
        // Fixture's pagination shows pages 1, 2, 3, ..., 14.
        Assert.Equal(14, lastPage);
    }

    [Fact]
    public void ExtractsArticleBodyTextContainingDateRange()
    {
        var text = DxWorldScraper.ExtractArticleText(ArticleFixtureHtml);

        Assert.Contains("September 23-29, 2026", text);
    }

    [Fact]
    public void DateRangeParserResolvesTheExtractedArticleText()
    {
        var text = DxWorldScraper.ExtractArticleText(ArticleFixtureHtml);

        var (start, end) = DateRangeParser.TryParse(text, fallbackYear: 2026);

        Assert.Equal(new DateOnly(2026, 9, 23), start);
        Assert.Equal(new DateOnly(2026, 9, 29), end);
    }

    [Fact]
    public void TitleNotMatchingExpectedShapeReturnsNull()
    {
        Assert.Null(DxWorldScraper.ParseTitle("Just A News Headline With No Dash"));
    }

    [Fact]
    public void SkipsTheArticleFetchWhenACallsignIsAlreadyCrossReferenced()
    {
        var crossReference = new Dictionary<string, CrossReferenceEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["V51WH"] = new CrossReferenceEntry(new DateOnly(2026, 8, 25), new DateOnly(2026, 10, 10), 464),
        };

        var found = DxWorldScraper.TryFindCrossReference(["V51WH"], crossReference);

        Assert.NotNull(found);
        Assert.Equal(464, found!.DxccCode);
    }

    [Fact]
    public void DoesNotSkipWhenTheCrossReferenceEntryHasNoUsableDates()
    {
        // A source that resolved nothing useful for this callsign (e.g. a 425dxn
        // row with an unparseable Period) must not short-circuit dx-world's own
        // per-article fetch - that fetch is the only way to still get real dates.
        var crossReference = new Dictionary<string, CrossReferenceEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["ZZ1ZZ"] = new CrossReferenceEntry(null, null, null),
        };

        Assert.Null(DxWorldScraper.TryFindCrossReference(["ZZ1ZZ"], crossReference));
    }

    [Fact]
    public void ReturnsNullWhenNoCrossReferenceIndexWasSupplied()
    {
        Assert.Null(DxWorldScraper.TryFindCrossReference(["V51WH"], null));
    }

    [Fact]
    public void MatchesOnAnySecondaryCallsignNotJustTheFirst()
    {
        var crossReference = new Dictionary<string, CrossReferenceEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["V47RCX"] = new CrossReferenceEntry(new DateOnly(2026, 9, 9), new DateOnly(2026, 9, 30), null),
        };

        var found = DxWorldScraper.TryFindCrossReference(["V47YA", "V47RCX"], crossReference);

        Assert.NotNull(found);
    }
}
