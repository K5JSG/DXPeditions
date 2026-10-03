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
    /// <paramref name="workableBands"/> limits the regex to entities still
    /// unconfirmed on at least one band the user's station can work (see
    /// <see cref="NeededResult.IsNeededOn"/>). Null (the default) means no
    /// station limitation - every needed entity is included.
    /// </summary>
    public static string Build(IEnumerable<NeededResult> results, IReadOnlySet<string>? workableBands = null)
    {
        var callsigns = results
            .Where(r => r.IsNeededOn(workableBands))
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
}
