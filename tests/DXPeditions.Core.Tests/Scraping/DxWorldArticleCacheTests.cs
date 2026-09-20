using DXPeditions.Core.Scraping;

namespace DXPeditions.Core.Tests.Scraping;

public class DxWorldArticleCacheTests
{
    private static string TempPath() => Path.Combine(Path.GetTempPath(), $"dxworld_cache_test_{Guid.NewGuid():N}.json");

    [Fact]
    public void StoredBodyIsReturnedByTryGetBody()
    {
        var cache = DxWorldArticleCache.Load(TempPath());

        cache.Store("http://example.com/a", "body text", 2026, 9);

        Assert.Equal("body text", cache.TryGetBody("http://example.com/a"));
    }

    [Fact]
    public void UnknownUrlReturnsNull()
    {
        var cache = DxWorldArticleCache.Load(TempPath());

        Assert.Null(cache.TryGetBody("http://example.com/unknown"));
    }

    [Fact]
    public void SavedCacheRoundTripsThroughANewLoad()
    {
        var path = TempPath();
        try
        {
            var cache = DxWorldArticleCache.Load(path);
            cache.Store("http://example.com/a", "body text", 2026, 9);
            cache.Save();

            var reloaded = DxWorldArticleCache.Load(path);

            Assert.Equal("body text", reloaded.TryGetBody("http://example.com/a"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void PruneMonthRemovesAUrlDiscoveredThatMonthWhichIsNoLongerInTheLiveListing()
    {
        var cache = DxWorldArticleCache.Load(TempPath());
        cache.Store("http://example.com/removed", "body", 2026, 9);

        cache.PruneMonth(2026, 9, new HashSet<string>());

        Assert.Null(cache.TryGetBody("http://example.com/removed"));
    }

    [Fact]
    public void PruneMonthNeverTouchesADifferentMonthsEntries()
    {
        var cache = DxWorldArticleCache.Load(TempPath());
        cache.Store("http://example.com/august", "body", 2026, 8);

        // Pruning September's listing must not remove August's cached entry,
        // even though August's URL isn't in September's "current" set.
        cache.PruneMonth(2026, 9, new HashSet<string>());

        Assert.Equal("body", cache.TryGetBody("http://example.com/august"));
    }

    [Fact]
    public void PruneMonthKeepsAUrlStillPresentInTheLiveListing()
    {
        var cache = DxWorldArticleCache.Load(TempPath());
        cache.Store("http://example.com/still-there", "body", 2026, 9);

        cache.PruneMonth(2026, 9, new HashSet<string> { "http://example.com/still-there" });

        Assert.Equal("body", cache.TryGetBody("http://example.com/still-there"));
    }

    [Fact]
    public void LoadingAMissingFileStartsEmptyRatherThanThrowing()
    {
        var cache = DxWorldArticleCache.Load(TempPath());

        Assert.Null(cache.TryGetBody("http://example.com/anything"));
    }
}
