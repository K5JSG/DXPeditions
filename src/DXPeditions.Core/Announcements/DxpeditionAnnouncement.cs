namespace DXPeditions.Core.Announcements;

public enum AnnouncementSource
{
    Ng3k,
    DxWorld,
    Va3rj,
    Ham365,
    Dxn425
}

/// <summary>
/// One announced DXpedition, as scraped from either source. Bands/modes the
/// DXpedition itself plans to use are deliberately not modeled here - per the
/// user's decision, "needed" is computed purely from the DXCC entity against the
/// user's own log, independent of what a DXpedition claims it will transmit.
/// </summary>
public sealed class DxpeditionAnnouncement
{
    public required AnnouncementSource Source { get; init; }
    public required IReadOnlyList<string> Callsigns { get; init; }
    public required string RawEntityName { get; init; }

    /// <summary>
    /// A DXCC code already resolved by the source itself (e.g. va3rj publishes it
    /// directly per row), so callers can skip <c>DxccReference.Resolve</c>/IOTA
    /// name-matching entirely for this announcement. Null when the source only
    /// gives a free-text name and resolution must happen downstream as usual.
    /// </summary>
    public int? ResolvedDxccCode { get; init; }

    public DateOnly? StartDate { get; init; }
    public DateOnly? EndDate { get; init; }
    public string? QslVia { get; init; }
    public required string SourceUrl { get; init; }
    public string? RawInfoText { get; init; }

    /// <summary>
    /// True if this announcement's date range overlaps the given month. An open
    /// start/end (null) is treated as unbounded in that direction, so an
    /// announcement with no parseable date is included rather than silently
    /// dropped (over-inclusion is safer here than a silent miss).
    /// </summary>
    public bool OverlapsMonth(int year, int month)
    {
        var monthStart = new DateOnly(year, month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);

        var effectiveStart = StartDate ?? DateOnly.MinValue;
        var effectiveEnd = EndDate ?? DateOnly.MaxValue;

        return effectiveStart <= monthEnd && effectiveEnd >= monthStart;
    }
}
