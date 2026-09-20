using DXPeditions.Core.Adif;
using DXPeditions.Core.Announcements;
using DXPeditions.Core.Dxcc;
using DXPeditions.Core.Iota;
using DXPeditions.Core.Needed;

namespace DXPeditions.Core.Tests.Needed;

public class NeededCalculatorTests
{
    private const int Namibia = 464;
    private const int Turkey = 390;

    private static readonly DxccReference Reference = DxccReference.LoadEmbedded();

    private static AdifRecord MakeRecord(int dxcc, string band, string mode, bool confirmed)
    {
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["dxcc"] = dxcc.ToString(),
            ["band"] = band,
            ["mode"] = mode,
            ["lotw_qsl_rcvd"] = confirmed ? "Y" : "N",
        };
        return new AdifRecord(fields);
    }

    private static DxpeditionAnnouncement MakeAnnouncement(string entityName) => new()
    {
        Source = AnnouncementSource.Ng3k,
        Callsigns = ["TEST1"],
        RawEntityName = entityName,
        SourceUrl = "http://example.com",
    };

    private static DxpeditionAnnouncement MakeAnnouncementWithResolvedCode(int dxccCode, string rawEntityName) => new()
    {
        Source = AnnouncementSource.Va3rj,
        Callsigns = ["TEST1"],
        RawEntityName = rawEntityName,
        ResolvedDxccCode = dxccCode,
        SourceUrl = "http://example.com",
    };

    [Fact]
    public void NeverWorkedEntityIsNeededOnEveryStandardBand()
    {
        var calculator = new NeededCalculator([], Reference);

        var result = calculator.Evaluate(MakeAnnouncement("Namibia"));

        Assert.True(result.IsNeeded);
        Assert.False(result.HasAnyQso);
        Assert.False(result.HasAnyConfirmedQso);
        Assert.Equal(StandardBands.All.Count, result.NeededBands.Count);
    }

    [Fact]
    public void WorkedButNeverConfirmedIsStillNeeded()
    {
        var records = new List<AdifRecord> { MakeRecord(Namibia, "20m", "FT8", confirmed: false) };
        var calculator = new NeededCalculator(records, Reference);

        var result = calculator.Evaluate(MakeAnnouncement("Namibia"));

        Assert.True(result.IsNeeded);
        Assert.True(result.HasAnyQso);
        Assert.False(result.HasAnyConfirmedQso);
        Assert.Equal(StandardBands.All.Count, result.NeededBands.Count);
    }

    [Fact]
    public void PartiallyConfirmedBandsLeavesOnlyTheMissingOnesNeeded()
    {
        var records = new List<AdifRecord>
        {
            MakeRecord(Namibia, "20m", "FT8", confirmed: true),
            MakeRecord(Namibia, "40m", "FT8", confirmed: true),
        };
        var calculator = new NeededCalculator(records, Reference);

        var result = calculator.Evaluate(MakeAnnouncement("Namibia"));

        Assert.True(result.IsNeeded);
        Assert.True(result.HasAnyConfirmedQso);
        Assert.DoesNotContain("20m", result.NeededBands);
        Assert.DoesNotContain("40m", result.NeededBands);
        Assert.Equal(StandardBands.All.Count - 2, result.NeededBands.Count);
        // FT8 confirms the Digital bucket; CW and SSB are still needed.
        Assert.DoesNotContain(StandardModes.Digital, result.NeededModes);
        Assert.Contains(StandardModes.Cw, result.NeededModes);
        Assert.Contains(StandardModes.Ssb, result.NeededModes);
    }

    [Fact]
    public void FullyConfirmedAcrossEveryBandAndModeIsNotNeeded()
    {
        var records = new List<AdifRecord>();

        foreach (var band in StandardBands.All)
        {
            records.Add(MakeRecord(Namibia, band, "FT8", confirmed: true));
        }
        records.Add(MakeRecord(Namibia, "20m", "SSB", confirmed: true));
        records.Add(MakeRecord(Namibia, "20m", "CW", confirmed: true));

        var calculator = new NeededCalculator(records, Reference);
        var result = calculator.Evaluate(MakeAnnouncement("Namibia"));

        Assert.False(result.IsNeeded);
        Assert.Empty(result.NeededBands);
        Assert.Empty(result.NeededModes);
    }

    [Fact]
    public void ModeUniverseIsFixedToCwSsbAndDigitalRegardlessOfLogContent()
    {
        // The mode universe is a fixed 3-bucket set (CW/SSB/Digital), mirroring
        // StandardBands - it is never derived from what happens to be in the log.
        var calculator = new NeededCalculator([], Reference);

        var result = calculator.Evaluate(MakeAnnouncement("Namibia"));

        Assert.Equal(StandardModes.All.Count, result.NeededModes.Count);
        Assert.Contains(StandardModes.Cw, result.NeededModes);
        Assert.Contains(StandardModes.Ssb, result.NeededModes);
        Assert.Contains(StandardModes.Digital, result.NeededModes);
    }

    [Theory]
    [InlineData("FT8")]
    [InlineData("FT4")]
    [InlineData("RTTY")]
    [InlineData("JT65")]
    public void AnyDigitalVariantConfirmsTheSharedDigitalBucket(string digitalMode)
    {
        // Confirming via one digital mode (e.g. RTTY) must satisfy "Digital" as a
        // whole - the user does not track FT8 vs RTTY vs JT65 separately.
        var records = new List<AdifRecord> { MakeRecord(Namibia, "20m", digitalMode, confirmed: true) };
        var calculator = new NeededCalculator(records, Reference);

        var result = calculator.Evaluate(MakeAnnouncement("Namibia"));

        Assert.DoesNotContain(StandardModes.Digital, result.NeededModes);
    }

    [Theory]
    [InlineData("SSB")]
    [InlineData("USB")]
    [InlineData("LSB")]
    public void AnyVoiceVariantConfirmsTheSharedSsbBucket(string voiceMode)
    {
        var records = new List<AdifRecord> { MakeRecord(Namibia, "20m", voiceMode, confirmed: true) };
        var calculator = new NeededCalculator(records, Reference);

        var result = calculator.Evaluate(MakeAnnouncement("Namibia"));

        Assert.DoesNotContain(StandardModes.Ssb, result.NeededModes);
    }

    [Fact]
    public void UnresolvedEntityNameIsTreatedAsNeeded()
    {
        var calculator = new NeededCalculator([], Reference);

        // No callsigns either, so the callsign-prefix fallback has nothing to
        // work with - this genuinely can't resolve by any means.
        var announcement = new DxpeditionAnnouncement
        {
            Source = AnnouncementSource.Ng3k,
            Callsigns = [],
            RawEntityName = "Definitely Not A Real DXCC Entity",
            SourceUrl = "http://example.com",
        };

        var result = calculator.Evaluate(announcement);

        Assert.True(result.IsNeeded);
        Assert.False(result.HasAnyQso);
        Assert.Null(result.DxccEntityName);
    }

    [Fact]
    public void FallsBackToIotaReferenceWhenEntityNameIsAnIotaTitle()
    {
        // "Giresun Island, AS-154" isn't a DXCC entity name at all - dx-world.net
        // titles some posts this way. AS-154 resolves to Turkey (390) per the live
        // IOTA fulllist.json, verified during implementation.
        var iota = IotaReference.FromMappings(new Dictionary<string, int> { ["AS-154"] = Turkey });
        var records = new List<AdifRecord> { MakeRecord(Turkey, "20m", "FT8", confirmed: true) };
        var calculator = new NeededCalculator(records, Reference, iota);

        var result = calculator.Evaluate(MakeAnnouncement("Giresun Island, AS-154"));

        Assert.True(result.HasAnyQso); // resolved via IOTA fallback, not left as a blind guess
        Assert.DoesNotContain("20m", result.NeededBands);

        // The user chases DXCC entities, not islands - display the resolved
        // country name, not the raw IOTA-titled text the source used.
        Assert.Equal("Turkey", result.DxccEntityName);
        Assert.Equal("Giresun Island, AS-154", result.Announcement.RawEntityName);
    }

    [Fact]
    public void FallsBackToCallsignPrefixWhenNameAndIotaBothFail()
    {
        // Regression for a real user-reported bug: dx-world.net titled a real
        // post "Guinea DXpedition 2026" (callsign 3X4U) - neither the raw name
        // nor IOTA resolve it, but the callsign's "3X" prefix unambiguously
        // means Guinea, and the user really had worked it before.
        const int guinea = 107;
        var records = new List<AdifRecord> { MakeRecord(guinea, "20m", "FT8", confirmed: true) };
        var calculator = new NeededCalculator(records, Reference);

        var announcement = new DxpeditionAnnouncement
        {
            Source = AnnouncementSource.DxWorld,
            Callsigns = ["3X4U"],
            RawEntityName = "Guinea DXpedition 2026",
            SourceUrl = "http://example.com",
        };

        var result = calculator.Evaluate(announcement);

        Assert.Equal("Guinea", result.DxccEntityName);
        Assert.True(result.HasAnyQso); // must NOT show as a false "never worked before".
    }

    [Fact]
    public void ResolvedDxccCodeIsUsedEvenWhenTheRawEntityNameWouldNeverMatch()
    {
        // va3rj gives the DXCC code directly per row, so resolution must succeed
        // via that code even when the raw text alongside it (here, deliberately
        // garbage) could never resolve by name.
        var records = new List<AdifRecord> { MakeRecord(Namibia, "20m", "FT8", confirmed: true) };
        var calculator = new NeededCalculator(records, Reference);

        var result = calculator.Evaluate(MakeAnnouncementWithResolvedCode(Namibia, "Definitely Not A Real DXCC Entity"));

        Assert.Equal("Namibia", result.DxccEntityName);
        Assert.True(result.HasAnyQso);
    }

    [Fact]
    public void WithoutIotaReferenceAnIotaTitleStaysUnresolved()
    {
        var calculator = new NeededCalculator([], Reference); // no IotaReference supplied

        var announcement = new DxpeditionAnnouncement
        {
            Source = AnnouncementSource.DxWorld,
            Callsigns = [], // no callsign either, so the prefix fallback can't kick in
            RawEntityName = "Giresun Island, AS-154",
            SourceUrl = "http://example.com",
        };

        var result = calculator.Evaluate(announcement);

        Assert.True(result.IsNeeded);
        Assert.False(result.HasAnyQso);
        Assert.Null(result.DxccEntityName);
    }
}
