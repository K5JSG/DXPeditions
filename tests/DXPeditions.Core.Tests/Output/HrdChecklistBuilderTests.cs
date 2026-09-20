using DXPeditions.Core.Announcements;
using DXPeditions.Core.Needed;
using DXPeditions.Core.Output;

namespace DXPeditions.Core.Tests.Output;

public class HrdChecklistBuilderTests
{
    private static DxpeditionAnnouncement MakeAnnouncement(string entity, params string[] callsigns) => new()
    {
        Source = AnnouncementSource.Ng3k,
        Callsigns = callsigns,
        RawEntityName = entity,
        SourceUrl = "http://example.com",
    };

    [Fact]
    public void ListsNeverWorkedEntityAsNeededOnAnyBandMode()
    {
        var result = new NeededResult
        {
            Announcement = MakeAnnouncement("Namibia", "V51WH"),
            IsNeeded = true,
            NeededBands = [],
            NeededModes = [],
            HasAnyQso = false,
            HasAnyConfirmedQso = false,
        };

        var checklist = HrdChecklistBuilder.Build([result]);

        Assert.Contains("Namibia", checklist);
        Assert.Contains("V51WH", checklist);
        Assert.Contains("Never worked", checklist);
    }

    [Fact]
    public void ListsNeededBandsAndModesWhenPartiallyConfirmed()
    {
        var result = new NeededResult
        {
            Announcement = MakeAnnouncement("Nepal", "9N"),
            IsNeeded = true,
            NeededBands = ["160m", "80m"],
            NeededModes = ["RTTY"],
            HasAnyQso = true,
            HasAnyConfirmedQso = true,
        };

        var checklist = HrdChecklistBuilder.Build([result]);

        Assert.Contains("160m", checklist);
        Assert.Contains("80m", checklist);
        Assert.Contains("RTTY", checklist);
    }

    [Fact]
    public void OmitsEntriesThatAreNotNeeded()
    {
        var result = new NeededResult
        {
            Announcement = MakeAnnouncement("Fully Confirmed Land", "XX1XX"),
            IsNeeded = false,
            NeededBands = [],
            NeededModes = [],
            HasAnyQso = true,
            HasAnyConfirmedQso = true,
        };

        var checklist = HrdChecklistBuilder.Build([result]);

        Assert.DoesNotContain("Fully Confirmed Land", checklist);
    }

    [Fact]
    public void ReturnsPlaceholderWhenNothingIsNeeded()
    {
        var checklist = HrdChecklistBuilder.Build([]);

        Assert.Equal("(nothing needed for this month)", checklist);
    }
}
