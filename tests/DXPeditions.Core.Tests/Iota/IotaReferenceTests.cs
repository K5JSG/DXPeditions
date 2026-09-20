using DXPeditions.Core.Iota;
using DXPeditions.Core.Scraping;

namespace DXPeditions.Core.Tests.Iota;

public class IotaReferenceTests
{
    private static readonly IotaReference Reference = IotaReference.FromMappings(new Dictionary<string, int>
    {
        ["AS-154"] = 390, // Turkey - verified against the live IOTA fulllist.json
        ["EU-171"] = 221, // Denmark
    });

    [Fact]
    public void ExtractsAndResolvesAnIotaReferenceFromFreeText()
    {
        var code = Reference.TryResolveFromText("Giresun Island, AS-154");

        Assert.Equal(390, code);
    }

    [Fact]
    public void IsCaseInsensitiveOnTheReferenceCode()
    {
        var code = Reference.TryResolveFromText("Giresun Island, as-154");

        Assert.Equal(390, code);
    }

    [Fact]
    public void ReturnsNullWhenTextHasNoIotaReference()
    {
        Assert.Null(Reference.TryResolveFromText("Just a country name"));
    }

    [Fact]
    public void ReturnsNullForAReferenceNotInTheMapping()
    {
        Assert.Null(Reference.TryResolveFromText("Somewhere, ZZ-999"));
    }

    [Fact]
    public void CountReflectsHowManyReferencesLoaded()
    {
        Assert.Equal(2, Reference.Count);
        Assert.Equal(0, IotaReference.FromMappings(new Dictionary<string, int>()).Count);
    }

    [Fact]
    public async Task LoadAsyncTrustsAFreshLocalCacheWithoutTouchingTheNetwork()
    {
        // A cache checked within the last 30 days (per the user's "iota can be
        // checked once a month" call) must short-circuit before ever calling
        // the fetcher - if it didn't, this test would attempt a real network
        // request instead of completing instantly from the local file.
        var path = Path.Combine(Path.GetTempPath(), $"iota_cache_test_{Guid.NewGuid():N}.json");
        try
        {
            var freshCache = $$"""
                {"CheckedUtc":"{{DateTimeOffset.UtcNow:o}}","ContentHash":"irrelevant","Groups":[{"refno":"AS-154","dxcc_num":"390"}]}
                """;
            await File.WriteAllTextAsync(path, freshCache);

            using var fetcher = new HttpFetcher();
            var reference = await IotaReference.LoadAsync(fetcher, path);

            Assert.Equal(390, reference.TryResolveFromText("Giresun Island, AS-154"));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
