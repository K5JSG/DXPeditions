using DXPeditions.Core.Scraping;

namespace DXPeditions.Core.Tests.Scraping;

public class Ham365ScraperTests
{
    private static string FixtureHtml =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "ham365_sample.html"));

    [Fact]
    public void ParsesEachColumnsCallsignPairedWithItsHeaderName()
    {
        var columns = Ham365Scraper.ParseGrid(FixtureHtml);

        Assert.Contains(columns, c => c.Callsign == "V51WH" && c.RawEntityName == "Namibia");
        Assert.Contains(columns, c => c.Callsign == "RI1FJZ" && c.RawEntityName == "Franz Josef Land");
    }

    [Fact]
    public void OnlyMarksTheDaysWithANonEmptyCellAsActive()
    {
        var columns = Ham365Scraper.ParseGrid(FixtureHtml);

        var namibia = columns.Single(c => c.Callsign == "V51WH");
        Assert.Equal([1, 2, 3, 4], namibia.ActiveDays);

        var iota = columns.Single(c => c.Callsign == "H44GJ");
        Assert.Equal([3], iota.ActiveDays);
    }

    [Fact]
    public void SkipsTheColorAndDayLabelRows()
    {
        // If those rows leaked in as "day data", every column would pick up
        // bogus extra active days from them.
        var columns = Ham365Scraper.ParseGrid(FixtureHtml);

        Assert.All(columns, c => Assert.All(c.ActiveDays, day => Assert.InRange(day, 1, 5)));
    }

    [Fact]
    public void BuildAnnouncementsUsesTheMonthsOwnDatesWhenNeitherBoundaryIsTouched()
    {
        var target = new DateOnly(2026, 9, 1);
        var targetColumns = new[] { new Ham365Scraper.ColumnActivity("H44GJ", "Iota OC-149", [3]) };

        var results = Ham365Scraper.BuildAnnouncements(target, targetColumns, [], []);

        var result = Assert.Single(results);
        Assert.Equal(new DateOnly(2026, 9, 3), result.StartDate);
        Assert.Equal(new DateOnly(2026, 9, 3), result.EndDate);
    }

    [Fact]
    public void ExtendsStartIntoThePreviousMonthWhenDayOneIsActiveAndThePreviousMonthHasAMatch()
    {
        var target = new DateOnly(2026, 9, 1);
        var targetColumns = new[] { new Ham365Scraper.ColumnActivity("V51WH", "Namibia", [1, 2, 3, 4]) };
        var previousColumns = new[] { new Ham365Scraper.ColumnActivity("V51WH", "Namibia", [28, 29, 30]) };

        var results = Ham365Scraper.BuildAnnouncements(target, targetColumns, previousColumns, []);

        var result = Assert.Single(results);
        Assert.Equal(new DateOnly(2026, 8, 28), result.StartDate);
        Assert.Equal(new DateOnly(2026, 9, 4), result.EndDate);
    }

    [Fact]
    public void FallsBackToDayOneWhenTheStartBoundaryIsTouchedButThePreviousMonthHasNoMatch()
    {
        var target = new DateOnly(2026, 9, 1);
        var targetColumns = new[] { new Ham365Scraper.ColumnActivity("V51WH", "Namibia", [1, 2, 3]) };

        var results = Ham365Scraper.BuildAnnouncements(target, targetColumns, [], []);

        Assert.Equal(new DateOnly(2026, 9, 1), Assert.Single(results).StartDate);
    }

    [Fact]
    public void ExtendsEndIntoTheNextMonthWhenTheLastDayIsActiveAndTheNextMonthHasAMatch()
    {
        var target = new DateOnly(2026, 9, 1);
        var targetColumns = new[] { new Ham365Scraper.ColumnActivity("P29YY", "Papua New Guinea", [28, 29, 30]) };
        var nextColumns = new[] { new Ham365Scraper.ColumnActivity("P29YY", "Papua New Guinea", [1, 2, 3]) };

        var results = Ham365Scraper.BuildAnnouncements(target, targetColumns, [], nextColumns);

        Assert.Equal(new DateOnly(2026, 10, 3), Assert.Single(results).EndDate);
    }

    [Fact]
    public void TreatsHyphenJoinedMultiOperatorCallsignsAsSeparateCallsigns()
    {
        var target = new DateOnly(2026, 9, 1);
        var targetColumns = new[] { new Ham365Scraper.ColumnActivity("V47YA-V47RCX", "St Kitts & Nevis", [9]) };

        var results = Ham365Scraper.BuildAnnouncements(target, targetColumns, [], []);

        var result = Assert.Single(results);
        Assert.Contains("V47YA", result.Callsigns);
        Assert.Contains("V47RCX", result.Callsigns);
    }
}
