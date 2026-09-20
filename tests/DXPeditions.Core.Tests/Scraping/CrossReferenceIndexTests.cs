using DXPeditions.Core.Announcements;
using DXPeditions.Core.Scraping;

namespace DXPeditions.Core.Tests.Scraping;

public class CrossReferenceIndexTests
{
    private static DxpeditionAnnouncement Make(
        AnnouncementSource source, string callsign, DateOnly? start = null, DateOnly? end = null, int? code = null) => new()
    {
        Source = source,
        Callsigns = [callsign],
        RawEntityName = "Test",
        ResolvedDxccCode = code,
        StartDate = start,
        EndDate = end,
        SourceUrl = "http://example.com",
    };

    [Fact]
    public void Va3rjWinsOverEveryOtherSourceForTheSameCallsign()
    {
        var start = new DateOnly(2026, 1, 1);

        var index = CrossReferenceIndex.Build(
            va3rj: [Make(AnnouncementSource.Va3rj, "V51WH", start, code: 464)],
            ng3k: [Make(AnnouncementSource.Ng3k, "V51WH", start.AddDays(1))],
            dxn425: [Make(AnnouncementSource.Dxn425, "V51WH", start.AddDays(2))],
            ham365: [Make(AnnouncementSource.Ham365, "V51WH", start.AddDays(3))]);

        var entry = index["V51WH"];

        Assert.Equal(start, entry.StartDate);
        Assert.Equal(464, entry.DxccCode);
    }

    [Fact]
    public void FallsBackThroughThePriorityOrderWhenHigherSourcesDontHaveTheCallsign()
    {
        var ng3kDate = new DateOnly(2026, 2, 1);

        var index = CrossReferenceIndex.Build(
            va3rj: [],
            ng3k: [Make(AnnouncementSource.Ng3k, "9N", ng3kDate)],
            dxn425: [Make(AnnouncementSource.Dxn425, "9N", ng3kDate.AddDays(5))],
            ham365: [Make(AnnouncementSource.Ham365, "9N", ng3kDate.AddDays(10))]);

        Assert.Equal(ng3kDate, index["9N"].StartDate);
    }

    [Fact]
    public void IsCaseInsensitiveOnCallsign()
    {
        var index = CrossReferenceIndex.Build(
            va3rj: [Make(AnnouncementSource.Va3rj, "v51wh", new DateOnly(2026, 1, 1))],
            ng3k: [], dxn425: [], ham365: []);

        Assert.True(index.ContainsKey("V51WH"));
    }

    [Fact]
    public void EntryWithNoDatesIsNotUsable()
    {
        var index = CrossReferenceIndex.Build(
            va3rj: [Make(AnnouncementSource.Va3rj, "ZZ1ZZ")],
            ng3k: [], dxn425: [], ham365: []);

        Assert.False(index["ZZ1ZZ"].HasUsableDates);
    }
}
