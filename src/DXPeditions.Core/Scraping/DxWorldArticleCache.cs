using System.Text.Json;

namespace DXPeditions.Core.Scraping;

/// <summary>
/// A local on-disk cache of dx-world.net article bodies, keyed by article URL,
/// for the residual articles no cheap cross-referenced source already covers.
/// Amended and pruned each run: a URL still present in a freshly-fetched
/// archive month is added if new and reused (no re-fetch) if already cached;
/// a previously-cached URL discovered under that *same* month that's no
/// longer in the live listing is dropped, scoped correctly so pruning one
/// month never touches another month's still-valid entries.
/// </summary>
public sealed class DxWorldArticleCache
{
    private sealed record Entry(string BodyText, int DiscoveredYear, int DiscoveredMonth);

    private readonly string? _filePath;
    private readonly Dictionary<string, Entry> _entries;

    private DxWorldArticleCache(string? filePath, Dictionary<string, Entry> entries)
    {
        _filePath = filePath;
        _entries = entries;
    }

    /// <summary>A no-op cache (nothing loaded, nothing saved) - lets callers opt out of on-disk persistence, e.g. in tests.</summary>
    public static DxWorldArticleCache Disabled() => new(null, []);

    public static DxWorldArticleCache Load(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                var json = File.ReadAllText(filePath);
                var entries = JsonSerializer.Deserialize<Dictionary<string, Entry>>(json);
                if (entries is not null)
                {
                    return new DxWorldArticleCache(filePath, entries);
                }
            }
        }
        catch
        {
            // Corrupt or unreadable cache file - start fresh rather than crash.
        }

        return new DxWorldArticleCache(filePath, []);
    }

    public string? TryGetBody(string url) => _entries.TryGetValue(url, out var entry) ? entry.BodyText : null;

    public void Store(string url, string bodyText, int discoveredYear, int discoveredMonth) =>
        _entries[url] = new Entry(bodyText, discoveredYear, discoveredMonth);

    /// <summary>
    /// Drops cached entries discovered under (year, month) whose URL is no
    /// longer in that month's freshly-fetched archive listing. Never touches
    /// an entry discovered under a different month.
    /// </summary>
    public void PruneMonth(int year, int month, IReadOnlySet<string> currentUrls)
    {
        var stale = _entries
            .Where(kv => kv.Value.DiscoveredYear == year && kv.Value.DiscoveredMonth == month && !currentUrls.Contains(kv.Key))
            .Select(kv => kv.Key)
            .ToList();

        foreach (var url in stale)
        {
            _entries.Remove(url);
        }
    }

    public void Save()
    {
        if (_filePath is null)
        {
            return;
        }

        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(_filePath, JsonSerializer.Serialize(_entries));
        }
        catch
        {
            // Best-effort persistence - a failed save just means the next run refetches, not a crash.
        }
    }
}
