using System.Text.RegularExpressions;
using DXPeditions.Core.Needed;

namespace DXPeditions.Core.Output;

/// <summary>
/// Builds a chain of individually-anchored callsign alternatives, for GridTracker
/// 2's Call Roster "Regex Limiter" field, e.g. "^V51WH$|^V55Y$". Each alternative
/// is anchored on its own so no other callsign can be matched.
/// </summary>
public static class GridTrackerRegexBuilder
{
    /// <summary>
    /// <paramref name="workableBands"/> limits the regex to entities actually
    /// needed on a band the user's station can work - one whose only band-level
    /// need is on a band the user can't work (e.g. no 160m antenna) is left out,
    /// since there'd be no point alerting for it. Null (the default) means no
    /// station limitation - every needed entity is included, as before. An entity
    /// needed purely for a *mode* reason (already confirmed on every band) is
    /// never excluded by this filter, since it isn't a band-capability issue.
    /// </summary>
    public static string Build(IEnumerable<NeededResult> results, IReadOnlySet<string>? workableBands = null)
    {
        var callsigns = results
            .Where(r => r.IsNeeded && IsWorkable(r, workableBands))
            .SelectMany(r => r.Announcement.Callsigns)
            .Select(c => c.ToUpperInvariant())
            .Distinct()
            .OrderBy(c => c, StringComparer.Ordinal)
            .ToList();

        if (callsigns.Count == 0)
        {
            return string.Empty;
        }

        return string.Join('|', callsigns.Select(c => $"^{Regex.Escape(c)}$"));
    }

    private static bool IsWorkable(NeededResult result, IReadOnlySet<string>? workableBands) =>
        workableBands is null || result.NeededBands.Count == 0 || result.NeededBands.Any(workableBands.Contains);
}
