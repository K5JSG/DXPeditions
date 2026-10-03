using DXPeditions.Core.Announcements;

namespace DXPeditions.Core.Needed;

public sealed class NeededResult
{
    public required DxpeditionAnnouncement Announcement { get; init; }

    /// <summary>
    /// The resolved DXCC entity's official name (e.g. "Turkey"), or null if
    /// <see cref="Announcement"/>'s raw name/IOTA reference didn't resolve. Callers
    /// that display an entity to the user should prefer this over
    /// <see cref="DxpeditionAnnouncement.RawEntityName"/> - the user chases DXCC
    /// entities, not the island/IOTA name a source happened to title the post with.
    /// Falling back to RawEntityName only applies in the unresolved case, so a real
    /// matching gap is still visible rather than silently hidden.
    /// </summary>
    public string? DxccEntityName { get; init; }

    public required bool IsNeeded { get; init; }
    public required IReadOnlyList<string> NeededBands { get; init; }
    public required IReadOnlyList<string> NeededModes { get; init; }
    public required bool HasAnyQso { get; init; }
    public required bool HasAnyConfirmedQso { get; init; }

    /// <summary>
    /// Whether this entity is needed on something the user's station can actually
    /// work. <see cref="IsNeeded"/> counts all 11 standard bands, so a station with
    /// no 160m/80m/60m/6m capability sees nearly everything as needed. With a band
    /// set, an entity counts only if it's unconfirmed on at least one of those bands
    /// - nothing else (per the user: "what ever is checked should be what is listed,
    /// nothing more"), so a mode-only need doesn't count. Null means no limitation.
    /// </summary>
    public bool IsNeededOn(IReadOnlySet<string>? workableBands) =>
        IsNeeded && (workableBands is null || NeededBands.Any(workableBands.Contains));
}
