using DXPeditions.Core.Dxcc;

namespace DXPeditions.Core.Tests.Dxcc;

public class DxccReferenceTests
{
    private readonly DxccReference _reference = DxccReference.LoadEmbedded();

    [Fact]
    public void ResolvesExactCurrentEntityName()
    {
        var entity = _reference.Resolve("Namibia");

        Assert.NotNull(entity);
        Assert.Equal(464, entity!.Code);
    }

    [Theory]
    [InlineData("USA")]
    [InlineData("United States")]
    public void AliasesResolveToSameEntityAsOfficialName(string alias)
    {
        var official = _reference.Resolve("United States of America");
        var aliased = _reference.Resolve(alias);

        Assert.NotNull(official);
        Assert.NotNull(aliased);
        Assert.Equal(official!.Code, aliased!.Code);
    }

    [Fact]
    public void MatchingIsCaseAndWhitespaceInsensitive()
    {
        var lower = _reference.Resolve("namibia");
        var padded = _reference.Resolve("  Namibia  ");

        Assert.NotNull(lower);
        Assert.NotNull(padded);
        Assert.Equal(464, lower!.Code);
        Assert.Equal(464, padded!.Code);
    }

    [Theory]
    [InlineData("Solomon Is", "Solomon Islands")]
    [InlineData("Mariana Is", "Mariana Islands")]
    public void ResolvesNg3kTrailingIsAbbreviationToFullIslands(string abbreviated, string official)
    {
        var abbreviatedEntity = _reference.Resolve(abbreviated);
        var officialEntity = _reference.Resolve(official);

        Assert.NotNull(abbreviatedEntity);
        Assert.NotNull(officialEntity);
        Assert.Equal(officialEntity!.Code, abbreviatedEntity!.Code);
    }

    [Fact]
    public void UnknownNameReturnsNullRatherThanGuessing()
    {
        Assert.Null(_reference.Resolve("Definitely Not A Real DXCC Entity"));
        Assert.Null(_reference.Resolve(null));
        Assert.Null(_reference.Resolve(""));
    }

    [Fact]
    public void ByCodeReturnsMatchingEntity()
    {
        var entity = _reference.ByCode(291);

        Assert.NotNull(entity);
        Assert.Equal("United States of America", entity!.Name);
    }

    [Theory]
    [InlineData("Antigua", "Antigua and Barbuda")]
    [InlineData("Cape Verde Is", "Cape Verde")]
    [InlineData("Ceuta & Melilla", "Ceuta and Melilla")]
    [InlineData("Christmas I", "Christmas Island")]
    [InlineData("Dem Rep Congo", "Democratic Republic of the Congo")]
    [InlineData("Lord Howe I", "Lord Howe Island")]
    [InlineData("Marianas", "Mariana Islands")]
    [InlineData("Ogasawara", "Ogasawara Islands")]
    [InlineData("Peter I", "Peter I Island")]
    [InlineData("Sao Tome & Principe I", "Sao Tome and Principe")]
    [InlineData("Sint Maartin", "Sint Maarten")]
    [InlineData("South Georgia I", "South Georgia Island")]
    [InlineData("St Kitts & Nevis", "Saint Kitts and Nevis")]
    [InlineData("St Lucia", "Saint Lucia")]
    public void ResolvesNg3kCtyColumnAbbreviations(string ng3kSpelling, string official)
    {
        var abbreviated = _reference.Resolve(ng3kSpelling);
        var officialEntity = _reference.Resolve(official);

        Assert.NotNull(abbreviated);
        Assert.NotNull(officialEntity);
        Assert.Equal(officialEntity!.Code, abbreviated!.Code);
    }

    [Fact]
    public void DoesNotConflateEnglandWithUnitedKingdom()
    {
        // England, Scotland, Wales etc. are separate DXCC entities from any
        // "United Kingdom" grouping - regression guard against ever aliasing them.
        var england = _reference.Resolve("England");
        Assert.NotNull(england);
        Assert.Null(_reference.Resolve("United Kingdom"));
    }

    [Theory]
    [InlineData(48, "Eastern Kiribati")]
    [InlineData(301, "Western Kiribati")]
    [InlineData(31, "Central Kiribati")]
    public void KiribatiSubEntitiesUseTheArrlSpelledOutNameNotK0swesParenthetical(int code, string expectedName)
    {
        // Per explicit user request: the ARRL's own current DXCC list
        // (arrl.org/files/file/DXCC/2022_Current_Deleted.txt) spells these
        // "W./C./E. Kiribati", not the k0swe-derived snapshot's parenthetical
        // alternate names ("Gilbert Is."/"British Phoenix Is."/"Line Is.") -
        // the canonical Name shown to the user must match ARRL's primary name.
        var entity = _reference.ByCode(code);

        Assert.NotNull(entity);
        Assert.Equal(expectedName, entity!.Name);
    }

    [Theory]
    [InlineData("Line Islands", "Eastern Kiribati")]
    [InlineData("Gilbert Islands", "Western Kiribati")]
    [InlineData("Phoenix Islands", "Central Kiribati")]
    public void OldK0sweNamesStillResolveAsAliasesOfTheRenamedEntities(string oldName, string newName)
    {
        // Backward-compat: any source still using the old k0swe-style name must
        // still resolve, to the (now-renamed) same entity.
        var resolved = _reference.Resolve(oldName);
        var officialEntity = _reference.Resolve(newName);

        Assert.NotNull(resolved);
        Assert.NotNull(officialEntity);
        Assert.Equal(officialEntity!.Code, resolved!.Code);
    }

    [Theory]
    [InlineData("Casey Station, Antarctica", "Antarctica")]
    [InlineData("San Andres Island", "San Andrés and Providencia")]
    public void ResolvesHam365And425dxnNamingQuirks(string rawSpelling, string official)
    {
        // Verified against real user QSOs where relevant (VY0IRC/VY0AA worked =
        // Canada, handled separately via the callsign-prefix fallback, not an
        // alias here).
        var resolved = _reference.Resolve(rawSpelling);
        var officialEntity = _reference.Resolve(official);

        Assert.NotNull(resolved);
        Assert.NotNull(officialEntity);
        Assert.Equal(officialEntity!.Code, resolved!.Code);
    }

    [Fact]
    public void ResolveViaCallsignPrefixFindsTheLongestMatchingPrefix()
    {
        // "VK9X" (Christmas Island) must win over the bare "VK" (Australia)
        // that every VK9-prefixed callsign would also technically start with.
        var entity = _reference.ResolveViaCallsignPrefix(["VK9XY"]);

        Assert.NotNull(entity);
        Assert.Equal("Christmas Island", entity!.Name);
    }

    [Theory]
    [InlineData("3X4U", "Guinea")]
    [InlineData("C8K", "Mozambique")]
    [InlineData("VY0ZOO", "Canada")]
    [InlineData("6O6X", "Somalia")]
    public void ResolveViaCallsignPrefixHandlesRealDxpeditionCallsigns(string callsign, string expectedEntity)
    {
        var entity = _reference.ResolveViaCallsignPrefix([callsign]);

        Assert.NotNull(entity);
        Assert.Equal(expectedEntity, entity!.Name);
    }

    [Fact]
    public void ResolveViaCallsignPrefixChecksEachSlashSeparatedSegment()
    {
        // "KH0/JR1FRK" - KH0 is the DXCC-relevant segment (Mariana Islands),
        // JR1FRK is the operator's home call and must not confuse the match.
        var entity = _reference.ResolveViaCallsignPrefix(["KH0/JR1FRK"]);

        Assert.NotNull(entity);
        Assert.Equal("Mariana Islands", entity!.Name);
    }

    [Theory]
    [InlineData("HK0ABC")] // Malpelo Island and San Andrés and Providencia both list plain "HK0".
    [InlineData("JD1BON")] // Ogasawara Islands and Minami-Tori-shima both list plain "JD1".
    public void ResolveViaCallsignPrefixReturnsNullOnAGenuinePrefixCollisionRatherThanGuessing(string callsign)
    {
        Assert.Null(_reference.ResolveViaCallsignPrefix([callsign]));
    }

    [Fact]
    public void ResolveViaCallsignPrefixReturnsNullWhenNothingMatches()
    {
        // No real DXCC prefix is purely numeric.
        Assert.Null(_reference.ResolveViaCallsignPrefix(["0000000"]));
        Assert.Null(_reference.ResolveViaCallsignPrefix([]));
    }
}
