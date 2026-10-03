using DXPeditions.Core.Output;

namespace DXPeditions.Core.Tests.Output;

public class HrdRegexChunkerTests
{
    [Fact]
    public void ReturnsEmptyForEmptyRegex()
    {
        Assert.Empty(HrdRegexChunker.Chunk(""));
        Assert.Equal("", HrdRegexChunker.Build(""));
    }

    [Fact]
    public void KeepsShortRegexAsOneBlock()
    {
        Assert.Equal(["^V51WH$|^V55Y$"], HrdRegexChunker.Chunk("^V51WH$|^V55Y$"));
    }

    [Fact]
    public void SplitsOnlyBetweenAlternativesAndRespectsLimit()
    {
        var callsigns = Enumerable.Range(0, 100).Select(i => $"^K{i}ABC$").ToList();
        var regex = string.Join('|', callsigns);

        var chunks = HrdRegexChunker.Chunk(regex);

        Assert.True(chunks.Count > 1);
        Assert.All(chunks, c => Assert.True(c.Length <= HrdRegexChunker.DefaultMaxLength));
        Assert.All(chunks, c => Assert.False(c.StartsWith('|') || c.EndsWith('|')));
        Assert.Equal(callsigns, chunks.SelectMany(c => c.Split('|')));
    }

    [Fact]
    public void SeparatesBlocksWithBlankLine()
    {
        var output = HrdRegexChunker.Build("^AAAA$|^BBBB$|^CCCC$", maxLength: 13);

        Assert.Equal("^AAAA$|^BBBB$\r\n\r\n^CCCC$", output);
    }
}
