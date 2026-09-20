namespace DXPeditions.Core.Adif;

/// <summary>
/// One QSO record from an ADIF log, exposed as raw field lookups plus a few typed
/// accessors for the fields this app actually cares about.
/// </summary>
public sealed class AdifRecord
{
    private readonly Dictionary<string, string> _fields;

    public AdifRecord(Dictionary<string, string> fields)
    {
        _fields = fields;
    }

    public string? this[string fieldName] =>
        _fields.TryGetValue(fieldName, out var value) ? value : null;

    public int? Dxcc =>
        int.TryParse(this["dxcc"], out var value) ? value : null;

    public string? Band => this["band"];

    public string? Mode => this["mode"];

    public string? Country => this["country"];

    /// <summary>
    /// True if either LOTW or a paper card confirms this QSO. ADIF's QSL-received
    /// enumeration includes "Y" (yes) and "V" (verified) as confirmed states;
    /// "N", "R" (requested), "I" (invalid) and anything else are not confirmed.
    /// </summary>
    public bool IsConfirmed => IsConfirmedStatus(this["lotw_qsl_rcvd"]) || IsConfirmedStatus(this["qsl_rcvd"]);

    private static bool IsConfirmedStatus(string? status) =>
        status is not null &&
        (status.Equals("Y", StringComparison.OrdinalIgnoreCase) ||
         status.Equals("V", StringComparison.OrdinalIgnoreCase));
}
