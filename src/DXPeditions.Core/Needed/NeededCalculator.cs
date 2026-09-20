using DXPeditions.Core.Adif;
using DXPeditions.Core.Announcements;
using DXPeditions.Core.Dxcc;
using DXPeditions.Core.Iota;

namespace DXPeditions.Core.Needed;

/// <summary>
/// Computes, per the user's confirmed rule: a DXCC entity is "needed" if the user
/// has zero QSOs for it, zero *confirmed* QSOs for it, or is missing a confirmed
/// QSO on any of the fixed standard bands or any mode present anywhere in their
/// own log. This is deliberately independent of what a DXpedition itself claims
/// it will operate.
/// </summary>
public sealed class NeededCalculator
{
    private readonly ILookup<int, AdifRecord> _recordsByDxcc;
    private readonly DxccReference _dxccReference;
    private readonly IotaReference? _iotaReference;

    public NeededCalculator(IEnumerable<AdifRecord> records, DxccReference dxccReference, IotaReference? iotaReference = null)
    {
        _recordsByDxcc = records.Where(r => r.Dxcc.HasValue).ToLookup(r => r.Dxcc!.Value);
        _dxccReference = dxccReference;
        _iotaReference = iotaReference;
    }

    public NeededResult Evaluate(DxpeditionAnnouncement announcement)
    {
        var entity = (announcement.ResolvedDxccCode.HasValue ? _dxccReference.ByCode(announcement.ResolvedDxccCode.Value) : null)
            ?? _dxccReference.Resolve(announcement.RawEntityName)
            ?? ResolveViaIota(announcement.RawEntityName)
            ?? _dxccReference.ResolveViaCallsignPrefix(announcement.Callsigns);

        if (entity is null)
        {
            // Entity name didn't resolve against the known-current DXCC list. Treat
            // as needed (safer to over-show than silently drop), but this is a real
            // matching gap, not a genuine "never worked" - the UI shows RawEntityName
            // so the user can spot it and, if warranted, add an alias.
            return new NeededResult
            {
                Announcement = announcement,
                DxccEntityName = null,
                IsNeeded = true,
                NeededBands = StandardBands.All,
                NeededModes = StandardModes.All,
                HasAnyQso = false,
                HasAnyConfirmedQso = false,
            };
        }

        var records = _recordsByDxcc[entity.Code];
        var hasAnyQso = records.Any();

        var confirmedRecords = records.Where(r => r.IsConfirmed).ToList();
        var hasAnyConfirmedQso = confirmedRecords.Count > 0;

        var confirmedBands = confirmedRecords
            .Select(r => r.Band)
            .Where(b => !string.IsNullOrWhiteSpace(b))
            .Select(b => b!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var confirmedModes = confirmedRecords
            .Select(r => r.Mode)
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .Select(m => StandardModes.Classify(m!))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var neededBands = StandardBands.All
            .Where(b => !confirmedBands.Contains(b))
            .ToList();

        var neededModes = StandardModes.All
            .Where(m => !confirmedModes.Contains(m))
            .ToList();

        var isNeeded = !hasAnyQso || !hasAnyConfirmedQso || neededBands.Count > 0 || neededModes.Count > 0;

        return new NeededResult
        {
            Announcement = announcement,
            DxccEntityName = entity.Name,
            IsNeeded = isNeeded,
            NeededBands = neededBands,
            NeededModes = neededModes,
            HasAnyQso = hasAnyQso,
            HasAnyConfirmedQso = hasAnyConfirmedQso,
        };
    }

    /// <summary>
    /// Fallback for when the entity name is actually an IOTA-titled name (common on
    /// dx-world.net, e.g. "Giresun Island, AS-154") rather than a DXCC entity name:
    /// extract the IOTA reference code and resolve it to a DXCC entity that way.
    /// </summary>
    private DxccEntity? ResolveViaIota(string rawName)
    {
        var code = _iotaReference?.TryResolveFromText(rawName);
        return code.HasValue ? _dxccReference.ByCode(code.Value) : null;
    }
}
