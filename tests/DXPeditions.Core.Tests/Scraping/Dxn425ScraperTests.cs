using DXPeditions.Core.Scraping;

namespace DXPeditions.Core.Tests.Scraping;

public class Dxn425ScraperTests
{
    private static readonly DateOnly Today = new(2026, 9, 20);

    private static string FixtureHtml =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "dxn425_sample.html"));

    [Fact]
    public void ExtractsTheCompoundCallsignFromBeforeTheColon()
    {
        var results = Dxn425Scraper.ParseHtml(FixtureHtml, Today);

        Assert.Contains(results, a => a.Callsigns.Contains("9N/OM0GA"));
    }

    [Fact]
    public void StripsTheTrailingDuplicatedPrefixFromTheEntityName()
    {
        var results = Dxn425Scraper.ParseHtml(FixtureHtml, Today);

        var nepal = results.Single(a => a.Callsigns.Contains("9N/OM0GA"));

        Assert.Equal("Nepal", nepal.RawEntityName);
    }

    [Fact]
    public void PreservesAmpersandInMultiWordEntityNames()
    {
        var results = Dxn425Scraper.ParseHtml(FixtureHtml, Today);

        var stKitts = results.Single(a => a.Callsigns.Contains("V47YA"));

        Assert.Equal("St Kitts & Nevis", stKitts.RawEntityName);
        Assert.Equal(new DateOnly(2026, 9, 28), stKitts.StartDate);
        Assert.Equal(new DateOnly(2026, 10, 4), stKitts.EndDate);
    }

    [Fact]
    public void ParsesABareSingleDayPeriod()
    {
        var results = Dxn425Scraper.ParseHtml(FixtureHtml, Today);

        var special = results.Single(a => a.Callsigns.Contains("PH82MG"));

        Assert.Equal(new DateOnly(2026, 9, 26), special.StartDate);
        Assert.Equal(new DateOnly(2026, 9, 26), special.EndDate);
    }

    [Fact]
    public void UsesAnExplicitOverflowYearWhenPresent()
    {
        var results = Dxn425Scraper.ParseHtml(FixtureHtml, Today);

        var andaman = results.Single(a => a.Callsigns.Contains("VU4X"));

        Assert.Equal(new DateOnly(2027, 11, 13), andaman.StartDate);
        Assert.Equal(new DateOnly(2027, 11, 23), andaman.EndDate);
    }

    [Fact]
    public void KeepsAnUnparseablePeriodAsNullRatherThanGuessing()
    {
        var results = Dxn425Scraper.ParseHtml(FixtureHtml, Today);

        var nowhere = results.Single(a => a.Callsigns.Contains("ZZ1ZZ"));

        Assert.Null(nowhere.StartDate);
        Assert.Null(nowhere.EndDate);
    }

    [Fact]
    public void DoesNotTruncateTheEntityNameAtADashEmbeddedInAnIotaReference()
    {
        // Regression: an early version split on the *first* '-' anywhere in the
        // text, which cut "Ocracoke Island (NA-067) W" off at "(NA" because the
        // dash inside the IOTA code was found before the real "- description"
        // separator. The IOTA code must survive intact so NeededCalculator's
        // existing IOTA-fallback resolution can use it.
        var results = Dxn425Scraper.ParseHtml(FixtureHtml, Today);

        var ocracoke = results.Single(a => a.Callsigns.Contains("WB8YJF"));

        Assert.Equal("Ocracoke Island (NA-067)", ocracoke.RawEntityName);
    }

    [Fact]
    public void SkipsTheHeaderRow()
    {
        var results = Dxn425Scraper.ParseHtml(FixtureHtml, Today);

        Assert.DoesNotContain(results, a => a.RawEntityName == "Operation");
    }
}
