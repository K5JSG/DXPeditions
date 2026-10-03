using System.Text.Json;

namespace DXPeditions.App;

/// <summary>
/// UI state remembered between runs: which workable-band checkboxes were checked
/// and where the splitters sat. Splitter positions are stored as a fraction of
/// the container so they still look right if the window opens at another size.
/// </summary>
public sealed class AppSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>Null means never saved - every band starts checked.</summary>
    public List<string>? WorkableBands { get; set; }

    /// <summary>Grid vs output panes (top/bottom).</summary>
    public double? MainSplitRatio { get; set; }

    /// <summary>GridTracker vs HRD output panes (left/right).</summary>
    public double? OutputsSplitRatio { get; set; }

    /// <summary>
    /// A missing or unreadable file just means defaults - a corrupt settings file
    /// must never stop the app from starting.
    /// </summary>
    public static AppSettings Load(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path)) ?? new AppSettings();
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
        }

        return new AppSettings();
    }

    public void Save(string path)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOptions));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
