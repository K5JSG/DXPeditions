using System.Text.RegularExpressions;
using DXPeditions.Core.Announcements;
using DXPeditions.Core.Needed;
using DXPeditions.Core.Output;

namespace DXPeditions.Core.Tests.Output;

public class GridTrackerRegexBuilderTests
{
    private static NeededResult MakeResult(bool needed, params string[] callsigns) =>
        MakeResult(needed, neededBands: [], callsigns);

    private static NeededResult MakeResult(bool needed, string[] neededBands, params string[] callsigns) => new()
    {
        Announcement = new DxpeditionAnnouncement
        {
            Source = AnnouncementSource.Ng3k,
            Callsigns = callsigns,
            RawEntityName = "Test Entity",
            SourceUrl = "http://example.com",
        },
        IsNeeded = needed,
        NeededBands = neededBands,
        NeededModes = [],
        HasAnyQso = false,
        HasAnyConfirmedQso = false,
    };

    [Fact]
    public void BuildsAlternationOfNeededCallsignsOnly()
    {
        var results = new[]
        {
            MakeResult(needed: true, "V51WH"),
            MakeResult(needed: false, "NOTNEEDED"),
            MakeResult(needed: true, "7Q5C"),
        };

        var regex = GridTrackerRegexBuilder.Build(results);

        Assert.Contains("^V51WH$", regex);
        Assert.Contains("^7Q5C$", regex);
        Assert.DoesNotContain("NOTNEEDED", regex);
        Assert.DoesNotContain("(", regex);
        Assert.DoesNotContain(")", regex);
    }

    [Fact]
    public void EachCallsignIsIndividuallyAnchored()
    {
        var results = new[] { MakeResult(needed: true, "V51WH", "7Q5C") };

        var regex = GridTrackerRegexBuilder.Build(results);

        Assert.Equal("^7Q5C$|^V51WH$", regex);
    }

    [Fact]
    public void SplitsMultiCallsignAnnouncementsIntoSeparateAlternationMembers()
    {
        var results = new[] { MakeResult(needed: true, "N7NU/VP9", "VP9I") };

        var regex = GridTrackerRegexBuilder.Build(results);

        Assert.Matches(new Regex(regex, RegexOptions.IgnoreCase), "N7NU/VP9");
        Assert.Matches(new Regex(regex, RegexOptions.IgnoreCase), "VP9I");
    }

    [Fact]
    public void ProducesEmptyStringWhenNothingIsNeeded()
    {
        var results = new[] { MakeResult(needed: false, "SOMECALL") };

        Assert.Equal(string.Empty, GridTrackerRegexBuilder.Build(results));
    }

    [Fact]
    public void ExcludesAnEntityWhoseOnlyNeededBandIsUnworkable()
    {
        var results = new[] { MakeResult(needed: true, neededBands: ["160m"], "V51WH") };

        var regex = GridTrackerRegexBuilder.Build(results, workableBands: new HashSet<string> { "20m", "15m", "10m" });

        Assert.Equal(string.Empty, regex);
    }

    [Fact]
    public void IncludesAnEntityNeededOnAtLeastOneWorkableBand()
    {
        var results = new[] { MakeResult(needed: true, neededBands: ["160m", "20m"], "V51WH") };

        var regex = GridTrackerRegexBuilder.Build(results, workableBands: new HashSet<string> { "20m" });

        Assert.Contains("^V51WH$", regex);
    }

    [Fact]
    public void DoesNotExcludeAnEntityNeededPurelyForAModeReason()
    {
        // Fully confirmed on every band already (NeededBands empty) but still
        // needed for a mode - a band filter must not silently drop this, since
        // it isn't a band-capability issue at all.
        var results = new[] { MakeResult(needed: true, neededBands: [], "V51WH") };

        var regex = GridTrackerRegexBuilder.Build(results, workableBands: new HashSet<string> { "20m" });

        Assert.Contains("^V51WH$", regex);
    }

    [Fact]
    public void NullWorkableBandsMeansNoStationLimitation()
    {
        var results = new[] { MakeResult(needed: true, neededBands: ["160m"], "V51WH") };

        var regex = GridTrackerRegexBuilder.Build(results, workableBands: null);

        Assert.Contains("^V51WH$", regex);
    }

    [Fact]
    public void GeneratedRegexIsValidAndAnchored()
    {
        var results = new[] { MakeResult(needed: true, "V51WH") };

        var regex = GridTrackerRegexBuilder.Build(results);
        var compiled = new Regex(regex, RegexOptions.IgnoreCase);

        Assert.Matches(compiled, "V51WH");
        Assert.DoesNotMatch(compiled, "XV51WH"); // anchored - must not match as a substring.
    }
}
