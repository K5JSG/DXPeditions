using System.Globalization;

namespace DXPeditions.Core.Adif;

/// <summary>
/// Hand-rolled ADIF reader. This is deliberately NOT line-based: a field's declared
/// length can span embedded newlines (confirmed in real HRD-exported logs, e.g. a
/// multi-line "address" field), so every value is read by exact character length
/// from the position right after its tag's closing '>'.
/// </summary>
public static class AdifLogReader
{
    public static IEnumerable<AdifRecord> ReadRecords(string path)
    {
        var text = File.ReadAllText(path);
        return ReadRecordsFromText(text);
    }

    public static IEnumerable<AdifRecord> ReadRecordsFromText(string text)
    {
        int pos = SkipHeader(text);
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        while (TryReadNextTag(text, ref pos, out var name, out var value))
        {
            if (string.Equals(name, "eor", StringComparison.OrdinalIgnoreCase))
            {
                yield return new AdifRecord(fields);
                fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                continue;
            }

            if (string.Equals(name, "eoh", StringComparison.OrdinalIgnoreCase))
            {
                // Defensive: a second <eoh> shouldn't appear mid-file, but if it does,
                // just ignore it rather than treating it as a field.
                continue;
            }

            fields[name] = value;
        }
    }

    /// <summary>
    /// Finds the position right after the header's &lt;eoh&gt; tag. If no &lt;eoh&gt;
    /// is present (some minimal exporters omit the header entirely), starts from 0.
    /// </summary>
    private static int SkipHeader(string text)
    {
        var eohIndex = text.IndexOf("<eoh>", StringComparison.OrdinalIgnoreCase);
        if (eohIndex < 0)
        {
            return 0;
        }

        return eohIndex + "<eoh>".Length;
    }

    private static bool TryReadNextTag(string text, ref int pos, out string name, out string value)
    {
        int lt = text.IndexOf('<', pos);
        if (lt < 0)
        {
            name = string.Empty;
            value = string.Empty;
            return false;
        }

        int gt = text.IndexOf('>', lt);
        if (gt < 0)
        {
            // Unterminated tag at end of file - nothing more to read.
            name = string.Empty;
            value = string.Empty;
            return false;
        }

        var header = text[(lt + 1)..gt];
        var colon = header.IndexOf(':');

        if (colon < 0)
        {
            // Bare tag, e.g. <eor> or <eoh>.
            name = header.Trim();
            value = string.Empty;
            pos = gt + 1;
            return true;
        }

        name = header[..colon].Trim();
        var rest = header[(colon + 1)..];
        var secondColon = rest.IndexOf(':');
        var lengthText = secondColon >= 0 ? rest[..secondColon] : rest;

        if (!int.TryParse(lengthText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var length) || length < 0)
        {
            // Malformed length - skip past this tag and keep scanning rather than throwing,
            // so one bad tag doesn't take down the whole file.
            value = string.Empty;
            pos = gt + 1;
            return true;
        }

        int valueStart = gt + 1;
        int available = text.Length - valueStart;
        int actualLength = Math.Min(length, Math.Max(available, 0));
        value = actualLength > 0 ? text.Substring(valueStart, actualLength) : string.Empty;
        pos = valueStart + actualLength;
        return true;
    }
}
