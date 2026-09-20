using DXPeditions.Core.Announcements;

namespace DXPeditions.Core.Scraping;

/// <summary>One callsign's best-known dates/DXCC code, merged across the cheap (single-fetch) sources.</summary>
public sealed record CrossReferenceEntry(DateOnly? StartDate, DateOnly? EndDate, int? DxccCode)
{
    /// <summary>True if this entry actually has something dx-world's per-article fetch would otherwise supply.</summary>
    public bool HasUsableDates => StartDate.HasValue || EndDate.HasValue;
}

/// <summary>
/// Merges the already-fetched ng3k/va3rj/ham365/425dxn results into a
/// callsign -> dates/DXCC-code lookup, so DxWorldScraper can skip its
/// per-article fetch whenever a teaser's callsign is already covered by one
/// of the cheap, single-fetch sources - dx-world is the last resort.
/// Priority when the same callsign appears in more than one source: va3rj
/// (explicit dates + a source-resolved DXCC code) > ng3k (explicit dates) >
/// 425dxn (explicit but year-inferred dates) > ham365 (dates inferred from
/// colored calendar cells).
/// </summary>
public static class CrossReferenceIndex
{
    public static IReadOnlyDictionary<string, CrossReferenceEntry> Build(
        IReadOnlyList<DxpeditionAnnouncement> va3rj,
        IReadOnlyList<DxpeditionAnnouncement> ng3k,
        IReadOnlyList<DxpeditionAnnouncement> dxn425,
        IReadOnlyList<DxpeditionAnnouncement> ham365)
    {
        var map = new Dictionary<string, CrossReferenceEntry>(StringComparer.OrdinalIgnoreCase);

        // Lowest priority first - a later loop overwrites an earlier one, so the
        // last source applied for a given callsign is the one that "wins".
        foreach (var source in new[] { ham365, dxn425, ng3k, va3rj })
        {
            foreach (var announcement in source)
            {
                var entry = new CrossReferenceEntry(announcement.StartDate, announcement.EndDate, announcement.ResolvedDxccCode);
                foreach (var callsign in announcement.Callsigns)
                {
                    map[callsign] = entry;
                }
            }
        }

        return map;
    }
}
