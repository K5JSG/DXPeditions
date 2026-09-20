using System.Text;
using DXPeditions.Core.Needed;

namespace DXPeditions.Core.Output;

/// <summary>
/// Builds a plain-text checklist, grouped by DXCC entity, for manually entering
/// DX Cluster Alarms in Ham Radio Deluxe (no bulk-import mechanism exists there).
/// </summary>
public static class HrdChecklistBuilder
{
    public static string Build(IEnumerable<NeededResult> results)
    {
        var needed = results.Where(r => r.IsNeeded).ToList();
        if (needed.Count == 0)
        {
            return "(nothing needed for this month)";
        }

        var sb = new StringBuilder();

        foreach (var result in needed.OrderBy(r => r.DxccEntityName ?? r.Announcement.RawEntityName, StringComparer.OrdinalIgnoreCase))
        {
            var a = result.Announcement;
            var entityName = result.DxccEntityName ?? a.RawEntityName;
            sb.Append(entityName).Append(" - ").AppendLine(string.Join(", ", a.Callsigns));

            if (!result.HasAnyQso)
            {
                sb.AppendLine("  Never worked - needed on any band/mode");
            }
            else if (!result.HasAnyConfirmedQso)
            {
                sb.AppendLine("  Worked but not confirmed at all - needed on any band/mode");
            }
            else
            {
                if (result.NeededBands.Count > 0)
                {
                    sb.Append("  Needed bands: ").AppendLine(string.Join(", ", result.NeededBands));
                }

                if (result.NeededModes.Count > 0)
                {
                    sb.Append("  Needed modes: ").AppendLine(string.Join(", ", result.NeededModes));
                }
            }

            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }
}
