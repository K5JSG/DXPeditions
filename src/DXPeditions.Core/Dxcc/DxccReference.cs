using System.Globalization;
using System.Text;
using System.Text.Json;

namespace DXPeditions.Core.Dxcc;

/// <summary>
/// Resolves a free-text DXCC entity name (as scraped from ng3k/dx-world.net, or as
/// stored in the user's ADIF "country" field) to a canonical <see cref="DxccEntity"/>.
///
/// Data provenance: Resources/dxcc_entities.json is a snapshot of the 340 current
/// DXCC entities from https://github.com/k0swe/dxcc-json (Apache-2.0 licensed),
/// itself derived from the ARRL's published DXCC list. That list changes only a
/// few times a decade; refreshing this snapshot is a matter of re-fetching
/// https://raw.githubusercontent.com/k0swe/dxcc-json/main/dxcc.json, filtering to
/// entries where "deleted" is false, and replacing this file - EXCEPT for three
/// entries the user explicitly asked to rename away from k0swe's naming (codes
/// 31, 48, 301: k0swe calls them "Phoenix Islands"/"Line Islands"/"Gilbert
/// Islands", but the ARRL's own current list
/// (arrl.org/files/file/DXCC/2022_Current_Deleted.txt) spells them out as
/// "Central/Eastern/Western Kiribati" - re-applying the raw upstream file would
/// silently revert this, so re-check those three names after any refresh.
/// Resources/aliases.json is a small, individually-verified list of alternate
/// spellings actually observed in DXpedition news text (e.g. "USA", "Burma")
/// mapped to the exact canonical name above; it's expected to grow as real gaps
/// are noticed (unresolved entity names are surfaced in the UI, not silently
/// guessed).
/// </summary>
public sealed class DxccReference
{
    private readonly Dictionary<string, DxccEntity> _byNormalizedName;
    private readonly Dictionary<int, DxccEntity> _byCode;

    private DxccReference(List<DxccEntity> entities, Dictionary<string, string> aliases)
    {
        _byNormalizedName = entities.ToDictionary(e => Normalize(e.Name), e => e);
        _byCode = entities.ToDictionary(e => e.Code, e => e);

        foreach (var (alias, canonicalName) in aliases)
        {
            var normalizedCanonical = Normalize(canonicalName);
            if (_byNormalizedName.TryGetValue(normalizedCanonical, out var entity))
            {
                _byNormalizedName[Normalize(alias)] = entity;
            }
        }
    }

    public static DxccReference LoadEmbedded()
    {
        var entities = LoadEmbeddedJson<List<DxccEntity>>("dxcc_entities.json") ?? [];
        var aliases = LoadEmbeddedJson<Dictionary<string, string>>("aliases.json") ?? [];
        return new DxccReference(entities, aliases);
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static T? LoadEmbeddedJson<T>(string fileName)
    {
        var assembly = typeof(DxccReference).Assembly;
        var resourceName = $"DXPeditions.Core.Dxcc.Resources.{fileName}";
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");
        return JsonSerializer.Deserialize<T>(stream, JsonOptions);
    }

    /// <summary>
    /// Resolves a free-text entity name to a known current DXCC entity, or null if
    /// it doesn't match any known entity or alias. A null result is a genuine gap -
    /// the caller should surface it rather than guess.
    /// </summary>
    public DxccEntity? Resolve(string? rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
        {
            return null;
        }

        return _byNormalizedName.GetValueOrDefault(Normalize(rawName));
    }

    public DxccEntity? ByCode(int code) => _byCode.GetValueOrDefault(code);

    /// <summary>
    /// Last-resort fallback: resolves via a callsign's prefix against each
    /// entity's known prefix list, for when free-text name and IOTA resolution
    /// have both already failed. A DXpedition source's entity text is
    /// sometimes garbled, a sub-national descriptor, or genuinely ambiguous
    /// ("Guinea DXpedition 2026", "Casey Station, Antarctica"), but the
    /// DXCC-relevant part of the callsign itself is unambiguous far more
    /// often - the callsign was already correctly extracted regardless of
    /// what the entity text says. Checks every callsign given (and every
    /// "/"-separated segment of a compound call, e.g. "HK0/PY8WW") and picks
    /// the entity with the LONGEST matching prefix (so "VK9X" for Christmas
    /// Island wins over bare "VK" for Australia). If two different entities
    /// tie on prefix length - a genuine data ambiguity, e.g. Malpelo Island
    /// and San Andrés and Providencia both list plain "HK0" - this returns
    /// null rather than guessing; that case needs an explicit alias instead.
    /// </summary>
    public DxccEntity? ResolveViaCallsignPrefix(IEnumerable<string> callsigns)
    {
        DxccEntity? best = null;
        var bestLength = -1;
        var ambiguous = false;

        foreach (var callsign in callsigns)
        {
            if (string.IsNullOrWhiteSpace(callsign))
            {
                continue;
            }

            foreach (var segment in callsign.Split('/', StringSplitOptions.RemoveEmptyEntries))
            {
                foreach (var entity in _byCode.Values)
                {
                    foreach (var prefix in entity.Prefix.Split(','))
                    {
                        if (prefix.Length == 0 || !segment.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        if (prefix.Length > bestLength)
                        {
                            best = entity;
                            bestLength = prefix.Length;
                            ambiguous = false;
                        }
                        else if (prefix.Length == bestLength && entity.Code != best?.Code)
                        {
                            ambiguous = true;
                        }
                    }
                }
            }
        }

        return ambiguous ? null : best;
    }

    /// <summary>
    /// Aggressive normalization: lowercase, strip diacritics, drop punctuation,
    /// collapse whitespace, and strip a handful of noise words so minor phrasing
    /// differences ("Rep. of the Congo" vs "Republic of Congo") have a chance of
    /// matching without needing an explicit alias entry.
    /// </summary>
    private static string Normalize(string name)
    {
        var decomposed = name.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);

        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            sb.Append(char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : ' ');
        }

        var words = sb.ToString()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w is not ("the" or "republic" or "rep" or "of"))
            .ToList();

        // ng3k systematically abbreviates a trailing "Islands" to "Is" (e.g. "Solomon
        // Is", "Mariana Is", "Spratly Is") while dx-world.net and the official ARRL/
        // k0swe names spell it out. Only the trailing word is rewritten, so this can't
        // collide with an unrelated entity the way stripping "island" everywhere could.
        if (words.Count > 0 && words[^1] == "is")
        {
            words[^1] = "islands";
        }

        return string.Join(' ', words);
    }
}
