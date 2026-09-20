using DXPeditions.Core.Announcements;

namespace DXPeditions.Core.Tests.Announcements;

public class CallsignParserTests
{
    [Fact]
    public void ExtractsMultipleAsMentionsFromRealNg3kStyleText()
    {
        var text = "By JA1UII as JD1BON, JI1LET as JD1BOK, JE1NVD as JE1NVD/JD1, " +
                   "JI1CRM as JI1CRM/JD1 fm Chichijima I (IOTA AS-031); 160-6m; CQ SSB FT8 FT4";

        var result = CallsignParser.ExtractMentionedAs(text);

        Assert.Equal(["JD1BON", "JD1BOK", "JE1NVD/JD1", "JI1CRM/JD1"], result);
    }

    [Fact]
    public void ExtractsSingleAsMentionAlongsideAnOperatorPrefix()
    {
        var text = "By OM0GA as 9N/OM0GA fm Kirtipur; 40-10m; QSL via Club Log OQRS";

        var result = CallsignParser.ExtractMentionedAs(text);

        Assert.Equal(["9N/OM0GA"], result);
    }

    [Fact]
    public void ExtractsTrailingAsMentionForASecondaryCallsign()
    {
        var text = "By DK2WH fm nr Omaruru; 160-6m, incl 60m; QRV as V55Y in CQWW RTTY Contest";

        var result = CallsignParser.ExtractMentionedAs(text);

        Assert.Equal(["V55Y"], result);
    }

    [Fact]
    public void ReturnsEmptyWhenTextHasNoAsMentions()
    {
        Assert.Empty(CallsignParser.ExtractMentionedAs("Just some prose with no attribution pattern."));
        Assert.Empty(CallsignParser.ExtractMentionedAs(null));
    }

    [Fact]
    public void DoesNotDuplicateACallsignMentionedTwice()
    {
        var text = "By K5SL as PJ2/K5SL; later PJ2/K5SL as PJ2/K5SL was active again";

        var result = CallsignParser.ExtractMentionedAs(text);

        Assert.Single(result);
        Assert.Equal("PJ2/K5SL", result[0]);
    }
}
