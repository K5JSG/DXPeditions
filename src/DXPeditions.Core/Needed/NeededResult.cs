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
}
