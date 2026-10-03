namespace DXPeditions.Core.Output;

/// <summary>
/// Splits the GridTracker regex (e.g. "^V51WH$|^V55Y$|...") into blocks short
/// enough for Ham Radio Deluxe's DX Cluster Alarms, which won't take one long
/// expression. Splits only between '|' alternatives, so every block is a complete,
/// valid regex on its own. Blocks are separated by a blank line for copy/paste.
/// </summary>
public static class HrdRegexChunker
{
    public const int DefaultMaxLength = 300;

    public static string Build(string gridTrackerRegex, int maxLength = DefaultMaxLength) =>
        string.Join("\r\n\r\n", Chunk(gridTrackerRegex, maxLength));

    public static IReadOnlyList<string> Chunk(string gridTrackerRegex, int maxLength = DefaultMaxLength)
    {
        if (string.IsNullOrEmpty(gridTrackerRegex))
        {
            return [];
        }

        var chunks = new List<string>();
        var current = "";

        foreach (var alternative in gridTrackerRegex.Split('|'))
        {
            if (current.Length == 0)
            {
                // A single alternative longer than the limit can't be split
                // without breaking it, so it goes in a block of its own.
                current = alternative;
            }
            else if (current.Length + 1 + alternative.Length <= maxLength)
            {
                current += "|" + alternative;
            }
            else
            {
                chunks.Add(current);
                current = alternative;
            }
        }

        chunks.Add(current);
        return chunks;
    }
}
