namespace DXPeditions.Core.Needed;

public static class StandardBands
{
    /// <summary>
    /// The fixed band universe checked for every DXCC entity, per the user's
    /// decision - independent of what any given DXpedition itself plans to use.
    /// </summary>
    public static readonly IReadOnlyList<string> All =
        ["160m", "80m", "60m", "40m", "30m", "20m", "17m", "15m", "12m", "10m", "6m"];
}
