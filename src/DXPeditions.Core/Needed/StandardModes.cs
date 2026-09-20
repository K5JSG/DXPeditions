namespace DXPeditions.Core.Needed;

/// <summary>
/// The fixed mode universe checked for every DXCC entity, mirroring
/// <see cref="StandardBands"/> - independent of what any given DXpedition itself
/// plans to use. Every raw ADIF mode value (FT8, RTTY, JT65, USB, LSB, ...)
/// collapses into exactly one of these three buckets via <see cref="Classify"/>,
/// per the user's decision that finer-grained mode tracking isn't useful for
/// chase planning.
/// </summary>
public static class StandardModes
{
    public const string Cw = "CW";
    public const string Ssb = "SSB";
    public const string Digital = "Digital";

    public static readonly IReadOnlyList<string> All = [Cw, Ssb, Digital];

    private static readonly HashSet<string> SsbVariants = new(StringComparer.OrdinalIgnoreCase) { "SSB", "USB", "LSB" };

    /// <summary>
    /// Buckets a raw ADIF mode value into CW, SSB, or Digital. CW and voice
    /// (SSB/USB/LSB) are recognized explicitly; everything else (FT8, FT4, RTTY,
    /// PSK31, JT65, JT9, MSK144, WSPR, Olivia, ...) is Digital.
    /// </summary>
    public static string Classify(string rawMode)
    {
        if (rawMode.Equals(Cw, StringComparison.OrdinalIgnoreCase))
        {
            return Cw;
        }

        return SsbVariants.Contains(rawMode) ? Ssb : Digital;
    }
}
