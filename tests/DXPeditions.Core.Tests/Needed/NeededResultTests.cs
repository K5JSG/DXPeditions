using DXPeditions.Core.Announcements;
using DXPeditions.Core.Needed;

namespace DXPeditions.Core.Tests.Needed;

public class NeededResultTests
{
    private static readonly HashSet<string> FortyToTen =
        new(["40m", "30m", "20m", "17m", "15m", "12m", "10m"], StringComparer.OrdinalIgnoreCase);

    private static NeededResult MakeResult(bool needed, string[] neededBands, string[] neededModes) => new()
    {
        Announcement = new DxpeditionAnnouncement
        {
            Source = AnnouncementSource.Ng3k,
            Callsigns = ["V51WH"],
            RawEntityName = "Test Entity",
            SourceUrl = "http://example.com",
        },
        IsNeeded = needed,
        NeededBands = neededBands,
        NeededModes = neededModes,
        HasAnyQso = true,
        HasAnyConfirmedQso = true,
    };

    [Fact]
    public void NotNeededOnUnworkableBandsOnly()
    {
        var result = MakeResult(needed: true, ["160m", "80m", "60m", "6m"], []);

        Assert.False(result.IsNeededOn(FortyToTen));
    }

    [Fact]
    public void NeededWhenAnyNeededBandIsWorkable()
    {
        var result = MakeResult(needed: true, ["160m", "20m"], []);

        Assert.True(result.IsNeededOn(FortyToTen));
    }

    [Fact]
    public void ModeNeedDoesNotCountWhenNeededBandsAreUnworkable()
    {
        Assert.False(MakeResult(needed: true, ["160m"], ["CW"]).IsNeededOn(FortyToTen));
        Assert.False(MakeResult(needed: true, [], ["CW"]).IsNeededOn(FortyToTen));
    }

    [Fact]
    public void NothingIsNeededWithNoBandsChecked()
    {
        var empty = new HashSet<string>();

        Assert.False(MakeResult(needed: true, ["20m"], ["CW"]).IsNeededOn(empty));
    }

    [Fact]
    public void NullWorkableBandsFallsBackToIsNeeded()
    {
        Assert.True(MakeResult(needed: true, ["160m"], []).IsNeededOn(null));
        Assert.False(MakeResult(needed: false, [], []).IsNeededOn(null));
    }

    [Fact]
    public void NeverNeededWhenNotNeededAtAll()
    {
        Assert.False(MakeResult(needed: false, [], []).IsNeededOn(FortyToTen));
    }
}
