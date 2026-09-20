using DXPeditions.Core.Scraping;

namespace DXPeditions.Core.Tests.Scraping;

public class Va3rjScraperTests
{
    private static string FixtureHtml =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "va3rj_sample.html"));

    [Fact]
    public void ParsesCallsignAndResolvedDxccCodeFromTheTitleAttribute()
    {
        var results = Va3rjScraper.ParseHtml(FixtureHtml);

        var namibia = results.Single(a => a.Callsigns.Contains("V51WH"));

        Assert.Equal(464, namibia.ResolvedDxccCode);
    }

    [Fact]
    public void ParsesIsoStartAndEndDates()
    {
        var results = Va3rjScraper.ParseHtml(FixtureHtml);

        var namibia = results.Single(a => a.Callsigns.Contains("V51WH"));

        Assert.Equal(new DateOnly(2026, 8, 25), namibia.StartDate);
        Assert.Equal(new DateOnly(2026, 10, 10), namibia.EndDate);
    }

    [Fact]
    public void StripsTrailingQuestionMarkFromTentativeEndDates()
    {
        var results = Va3rjScraper.ParseHtml(FixtureHtml);

        var marshallIslands = results.Single(a => a.Callsigns.Contains("V73HW"));

        Assert.Equal(new DateOnly(2027, 3, 15), marshallIslands.EndDate);
    }

    [Fact]
    public void HandlesNotAnIotaWithoutAffectingDxccResolution()
    {
        var results = Va3rjScraper.ParseHtml(FixtureHtml);

        var hungary = results.Single(a => a.Callsigns.Contains("HG60IPA"));

        Assert.Equal(239, hungary.ResolvedDxccCode);
    }

    [Fact]
    public void AllRowsCarryAResolvedDxccCodeSoNameMatchingIsNeverNeeded()
    {
        var results = Va3rjScraper.ParseHtml(FixtureHtml);

        // Real data has one confirmed exception (see below) - excluded here so
        // this stays a meaningful regression guard for the normal case.
        var withCode = results.Where(a => !a.Callsigns.Contains("ZL30CW"));

        Assert.NotEmpty(withCode);
        Assert.All(withCode, a => Assert.NotNull(a.ResolvedDxccCode));
    }

    [Fact]
    public void FallsBackToTheNamePortionWhenTheDxccCodeIsMissingFromTheTitle()
    {
        // A real observed row has no leading code at all: title="   (ZL) New
        // Zealand". The name must still be extracted so name-based resolution
        // works, rather than leaving the raw, unparsed title as RawEntityName.
        var results = Va3rjScraper.ParseHtml(FixtureHtml);

        var newZealand = results.Single(a => a.Callsigns.Contains("ZL30CW"));

        Assert.Null(newZealand.ResolvedDxccCode);
        Assert.Equal("New Zealand", newZealand.RawEntityName);
    }
}
