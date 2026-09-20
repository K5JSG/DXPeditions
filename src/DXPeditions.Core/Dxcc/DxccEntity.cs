namespace DXPeditions.Core.Dxcc;

/// <summary>
/// One current DXCC entity. Data comes from the embedded snapshot in
/// Resources/dxcc_entities.json - see DxccReference for provenance/refresh notes.
/// </summary>
public sealed record DxccEntity(int Code, string Name, string Prefix, string Continent);
