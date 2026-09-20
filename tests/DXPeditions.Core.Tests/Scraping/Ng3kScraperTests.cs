using DXPeditions.Core.Scraping;

namespace DXPeditions.Core.Tests.Scraping;

public class Ng3kScraperTests
{
    private static string FixtureHtml =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "ng3k_sample.html"));

    [Fact]
    public void ParsesRealCapturedTableIntoAnnouncements()
    {
        var results = Ng3kScraper.ParseHtml(FixtureHtml);

        Assert.NotEmpty(results);
        // Fixture is a real capture of the live page as of 2026-09-19 - regression
        // test against known content; if the site redesigns, refresh the fixture.
        Assert.Contains(results, a => a.RawEntityName == "Namibia" && a.Callsigns.Contains("V51WH"));
    }

    [Fact]
    public void ParsesStartAndEndDatesInNg3kFormat()
    {
        var results = Ng3kScraper.ParseHtml(FixtureHtml);

        var namibia = results.Single(a => a.RawEntityName == "Namibia");

        Assert.Equal(new DateOnly(2026, 8, 25), namibia.StartDate);
        Assert.Equal(new DateOnly(2026, 10, 10), namibia.EndDate);
    }

    [Fact]
    public void ExtractsMultipleAndPortableCallsignsFromTheCallCell()
    {
        var results = Ng3kScraper.ParseHtml(FixtureHtml);

        var nepal = results.First(a => a.RawEntityName == "Nepal");
        Assert.Contains("9N", nepal.Callsigns);
    }

    [Fact]
    public void RecoversSpecificOperatingCallsFromInfoTextWhenCallColumnIsAGenericPrefix()
    {
        var results = Ng3kScraper.ParseHtml(FixtureHtml);

        // The real "Ogasawara" row's Call column is just "JD1", but its Info text
        // names four specific operating calls ("By JA1UII as JD1BON, JI1LET as
        // JD1BOK, JE1NVD as JE1NVD/JD1, JI1CRM as JI1CRM/JD1 fm Chichijima I").
        var ogasawara = results.First(a => a.RawEntityName == "Ogasawara" && a.Callsigns.Contains("JD1BON"));

        Assert.Contains("JD1", ogasawara.Callsigns);
        Assert.Contains("JD1BON", ogasawara.Callsigns);
        Assert.Contains("JD1BOK", ogasawara.Callsigns);
        Assert.Contains("JE1NVD/JD1", ogasawara.Callsigns);
        Assert.Contains("JI1CRM/JD1", ogasawara.Callsigns);
    }

    [Fact]
    public void SkipsMonthAndYearSeparatorRows()
    {
        var results = Ng3kScraper.ParseHtml(FixtureHtml);

        Assert.DoesNotContain(results, a => a.RawEntityName.Length == 0);
        Assert.All(results, a => Assert.NotEmpty(a.Callsigns));
    }
}
