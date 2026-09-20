using DXPeditions.Core.Adif;

namespace DXPeditions.Core.Tests.Adif;

public class AdifLogReaderTests
{
    [Fact]
    public void ParsesNormalRecord()
    {
        var text = "<call:5>K5JSG<band:3>40m<mode:3>FT8<eor>";

        var record = Assert.Single(AdifLogReader.ReadRecordsFromText(text));

        Assert.Equal("K5JSG", record["call"]);
        Assert.Equal("40m", record.Band);
        Assert.Equal("FT8", record.Mode);
    }

    [Fact]
    public void HandlesEmbeddedNewlinesWithinDeclaredFieldLength()
    {
        // Regression test modeled directly on a real HRD-exported "address" field,
        // which spans literal newlines within its declared byte length. A
        // line-oriented parser would corrupt this and misalign every field after it.
        var addressValue = "Robert Brian Morton, VE3WY\nP.O. BOX 1471\nEVERETT, ON L0M 1J0\nCanada";
        var text = $"<address:{addressValue.Length}>{addressValue}<call:5>VE3WY<eor>";

        var record = Assert.Single(AdifLogReader.ReadRecordsFromText(text));

        Assert.Equal(addressValue, record["address"]);
        Assert.Equal("VE3WY", record["call"]);
    }

    [Fact]
    public void TagNamesAreCaseInsensitive()
    {
        var text = "<CALL:5>K5JSG<BAND:3>40m<EOR>";

        var record = Assert.Single(AdifLogReader.ReadRecordsFromText(text));

        Assert.Equal("K5JSG", record["call"]);
        Assert.Equal("40m", record["Band"]);
    }

    [Fact]
    public void HandlesOptionalDataTypeSuffix()
    {
        var text = "<qso_date:8:d>20230821<call:4>ABCD<eor>";

        var record = Assert.Single(AdifLogReader.ReadRecordsFromText(text));

        Assert.Equal("20230821", record["qso_date"]);
        Assert.Equal("ABCD", record["call"]);
    }

    [Fact]
    public void FallsBackToStartWhenEohIsMissing()
    {
        var text = "<call:5>K5JSG<eor><call:4>ABCD<eor>";

        var records = AdifLogReader.ReadRecordsFromText(text).ToList();

        Assert.Equal(2, records.Count);
        Assert.Equal("K5JSG", records[0]["call"]);
        Assert.Equal("ABCD", records[1]["call"]);
    }

    [Fact]
    public void SkipsHeaderTextAndCommentBannerBeforeEoh()
    {
        var text = "#++\n# HRD Logbook\n#--\n<adif_ver:5>3.1.5<eoh>\n<call:5>K5JSG<eor>";

        var record = Assert.Single(AdifLogReader.ReadRecordsFromText(text));

        Assert.Equal("K5JSG", record["call"]);
    }

    [Fact]
    public void ParsesMultipleRecordsSeparately()
    {
        var text = "<call:5>K5JSG<eor><call:4>ABCD<eor><call:4>WXYZ<eor>";

        var records = AdifLogReader.ReadRecordsFromText(text).ToList();

        Assert.Equal(3, records.Count);
        Assert.Equal(["K5JSG", "ABCD", "WXYZ"], records.Select(r => r["call"]));
    }

    [Theory]
    [InlineData("Y", null, true)]
    [InlineData(null, "Y", true)]
    [InlineData("V", null, true)] // ADIF "Verified" status - confirmed real value in HRD-exported logs.
    [InlineData(null, "V", true)]
    [InlineData("N", "N", false)]
    [InlineData(null, null, false)]
    [InlineData("R", null, false)] // Requested, not yet confirmed.
    [InlineData("I", null, false)] // Invalid.
    public void IsConfirmedTruthTable(string? lotw, string? card, bool expected)
    {
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (lotw is not null) fields["lotw_qsl_rcvd"] = lotw;
        if (card is not null) fields["qsl_rcvd"] = card;

        var record = new AdifRecord(fields);

        Assert.Equal(expected, record.IsConfirmed);
    }
}
