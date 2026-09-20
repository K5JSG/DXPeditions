using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using DXPeditions.Core.Scraping;

namespace DXPeditions.Core.Iota;

/// <summary>
/// Resolves an IOTA (Islands on the Air) reference code, e.g. "AS-154", to the
/// DXCC entity number it belongs to. Data comes from the RSGB's official IOTA
/// website (iota-world.org).
///
/// The live endpoint sends no ETag/Last-Modified (confirmed via curl - it
/// explicitly sends Cache-Control: no-cache, private), so there's no cheap way
/// to ask "has this changed" without downloading the ~1.3MB body. Per the
/// user's explicit call ("iota can be checked once a month. it doesnt change
/// often"), this is cached to a local file and only re-checked at most once
/// every 30 days; even then, the local file is only overwritten when the
/// downloaded content's hash actually differs from what's cached.
/// </summary>
public sealed partial class IotaReference
{
    private const string Url =
        "https://www.iota-world.org/islands-on-the-air/downloads/download-file.html?path=fulllist.json";

    private static readonly TimeSpan StalenessWindow = TimeSpan.FromDays(30);

    [GeneratedRegex(@"\b([A-Za-z]{2}-\d{3})\b")]
    private static partial Regex IotaRefPattern();

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly IReadOnlyDictionary<string, int> _refnoToDxccCode;

    private IotaReference(IReadOnlyDictionary<string, int> refnoToDxccCode)
    {
        _refnoToDxccCode = refnoToDxccCode;
    }

    /// <summary>
    /// Loads IOTA data, preferring a local cache file over the network per the
    /// staleness policy described on this class. <paramref name="cacheFilePath"/>
    /// is optional so callers (tests in particular) can opt out of on-disk
    /// persistence entirely and always fetch live.
    /// </summary>
    public static async Task<IotaReference> LoadAsync(
        HttpFetcher fetcher, string? cacheFilePath = null, CancellationToken cancellationToken = default)
    {
        var cache = cacheFilePath is not null ? TryLoadCacheFile(cacheFilePath) : null;

        if (cache is not null && DateTimeOffset.UtcNow - cache.CheckedUtc < StalenessWindow)
        {
            // Checked recently enough - trust the local file, skip the network entirely.
            return BuildFromGroups(cache.Groups);
        }

        try
        {
            var json = await fetcher.GetStringAsync(Url, cancellationToken).ConfigureAwait(false);
            var groups = JsonSerializer.Deserialize<List<IotaGroup>>(json, JsonOptions) ?? [];

            if (cacheFilePath is not null)
            {
                var hash = ComputeHash(json);
                var unchanged = cache is not null && cache.ContentHash == hash;
                var toSave = unchanged
                    ? cache! with { CheckedUtc = DateTimeOffset.UtcNow }
                    : new CacheFile(DateTimeOffset.UtcNow, hash, groups);
                TrySaveCacheFile(cacheFilePath, toSave);
            }

            return BuildFromGroups(groups);
        }
        catch
        {
            // Network hiccup or a site format change - fall back to whatever local
            // cache exists (even if stale) rather than degrading to empty.
            return cache is not null ? BuildFromGroups(cache.Groups) : new IotaReference(new Dictionary<string, int>());
        }
    }

    public static IotaReference FromMappings(IReadOnlyDictionary<string, int> refnoToDxccCode) => new(refnoToDxccCode);

    /// <summary>
    /// Number of IOTA references successfully loaded. Zero means neither a live
    /// fetch nor a local cache was available (callers should surface that to the
    /// user instead of letting it fail silently).
    /// </summary>
    public int Count => _refnoToDxccCode.Count;

    /// <summary>
    /// Extracts an IOTA reference code (e.g. "AS-154") from free text, if present,
    /// and resolves it to a DXCC entity number.
    /// </summary>
    public int? TryResolveFromText(string text)
    {
        var match = IotaRefPattern().Match(text);
        if (!match.Success)
        {
            return null;
        }

        return _refnoToDxccCode.TryGetValue(match.Groups[1].Value.ToUpperInvariant(), out var code) ? code : null;
    }

    private static IotaReference BuildFromGroups(List<IotaGroup> groups)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var group in groups)
        {
            if (string.IsNullOrWhiteSpace(group.Refno) || string.IsNullOrWhiteSpace(group.DxccNum))
            {
                continue;
            }

            // Groups straddling more than one DXCC entity (e.g. "223,294,279" for
            // Great Britain's constituent entities) are deliberately skipped -
            // there's no reliable way to know which one a specific announcement
            // means from the refno alone, so leaving it unresolved (safe default)
            // beats guessing.
            if (group.DxccNum.Contains(','))
            {
                continue;
            }

            if (int.TryParse(group.DxccNum, out var code))
            {
                map[group.Refno] = code;
            }
        }

        return new IotaReference(map);
    }

    private static string ComputeHash(string content) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)));

    private static CacheFile? TryLoadCacheFile(string path)
    {
        try
        {
            return File.Exists(path) ? JsonSerializer.Deserialize<CacheFile>(File.ReadAllText(path), JsonOptions) : null;
        }
        catch
        {
            // Corrupt or unreadable cache file - treat as "no cache" rather than crash.
            return null;
        }
    }

    private static void TrySaveCacheFile(string path, CacheFile cacheFile)
    {
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, JsonSerializer.Serialize(cacheFile));
        }
        catch
        {
            // Best-effort persistence - a failed save just means the next run re-checks, not a crash.
        }
    }

    private sealed record CacheFile(DateTimeOffset CheckedUtc, string ContentHash, List<IotaGroup> Groups);

    private sealed class IotaGroup
    {
        [JsonPropertyName("refno")]
        public string? Refno { get; set; }

        [JsonPropertyName("dxcc_num")]
        public string? DxccNum { get; set; }
    }
}
