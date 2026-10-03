using DXPeditions.Core.Announcements;
using DXPeditions.Core.Needed;

namespace DXPeditions.Core.Tests.Needed;

public class NeededResultTests
{
    private static readonly HashSet<string> FortyToTen =
        new(["40m", "30m", "20m", "17m", "15m", "12m", "10m"], StringComparer.OrdinalIgnoreCase);

    private static NeededResult MakeResult(Dictionary<string, IReadOnlyList<string>> neededModesByBand) => new()
    {
        Announcement = new DxpeditionAnnouncement
        {
            Source = AnnouncementSource.Ng3k,
            Callsigns = ["V51WH"],
            RawEntityName = "Test Entity",
            SourceUrl = "http://example.com",
        },
        IsNeeded = neededModesByBand.Count > 0,
        NeededBands = [],
        NeededModes = [],
        NeededModesByBand = neededModesByBand,
        HasAnyQso = true,
        HasAnyConfirmedQso = true,
    };

    [Fact]
    public void NotNeededWhenOnlyUncheckedBandsHaveMissingModes()
    {
        var result = MakeResult(new() { ["160m"] = ["CW", "SSB"], ["6m"] = ["Digital"] });

        Assert.False(result.IsNeededOn(FortyToTen));
    }

    [Fact]
    public void NeededWhenACheckedBandIsMissingAMode()
    {
        // 40m SSB confirmed, but 40m CW isn't - still needed on 40m.
        var result = MakeResult(new() { ["40m"] = ["CW"] });

        Assert.True(result.IsNeededOn(FortyToTen));
    }

    [Fact]
    public void NothingIsNeededWithNoBandsChecked()
    {
        var result = MakeResult(new() { ["20m"] = ["CW"] });

        Assert.False(result.IsNeededOn(new HashSet<string>()));
    }

    [Fact]
    public void NullWorkableBandsMeansEveryBand()
    {
        Assert.True(MakeResult(new() { ["160m"] = ["CW"] }).IsNeededOn(null));
        Assert.False(MakeResult([]).IsNeededOn(null));
    }
}
