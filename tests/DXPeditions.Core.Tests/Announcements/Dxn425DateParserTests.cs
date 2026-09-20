using DXPeditions.Core.Announcements;

namespace DXPeditions.Core.Tests.Announcements;

public class Dxn425DateParserTests
{
    private static readonly DateOnly Today = new(2026, 9, 20);

    [Fact]
    public void ParsesADdMmRangeInTheCurrentYear()
    {
        var (start, end) = Dxn425DateParser.TryParse("28/09-04/10", Today);

        Assert.Equal(new DateOnly(2026, 9, 28), start);
        Assert.Equal(new DateOnly(2026, 10, 4), end);
    }

    [Fact]
    public void ParsesAnOpenStartTillDate()
    {
        var (start, end) = Dxn425DateParser.TryParse("till  19/09", Today);

        Assert.Null(start);
        Assert.Equal(new DateOnly(2026, 9, 19), end);
    }

    [Fact]
    public void ParsesABareSingleDayAsStartAndEnd()
    {
        var (start, end) = Dxn425DateParser.TryParse("26/09", Today);

        Assert.Equal(new DateOnly(2026, 9, 26), start);
        Assert.Equal(new DateOnly(2026, 9, 26), end);
    }

    [Fact]
    public void UsesAnExplicitTrailingYearWhenGiven()
    {
        var (start, end) = Dxn425DateParser.TryParse("13/11-23/11 2027", Today);

        Assert.Equal(new DateOnly(2027, 11, 13), start);
        Assert.Equal(new DateOnly(2027, 11, 23), end);
    }

    [Theory]
    [InlineData("till  ??/??")]
    [InlineData("till  December")]
    [InlineData("March       2027")]
    public void ReturnsNullForUnparseablePlaceholdersRatherThanGuessing(string text)
    {
        var (start, end) = Dxn425DateParser.TryParse(text, Today);

        Assert.Null(start);
        Assert.Null(end);
    }

    [Fact]
    public void InfersNextYearWhenADateWithoutAYearWouldOtherwiseBeFarInThePast()
    {
        // "05/01" (Jan 5) with no year, evaluated against a "today" of Sept 20 -
        // Jan 5 of the current year is more than 60 days in the past, so this
        // must be next January, not last January.
        var (start, _) = Dxn425DateParser.TryParse("05/01", Today);

        Assert.Equal(new DateOnly(2027, 1, 5), start);
    }
}
