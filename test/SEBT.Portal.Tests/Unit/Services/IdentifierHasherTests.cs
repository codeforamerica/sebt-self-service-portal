using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Infrastructure.Services;

namespace SEBT.Portal.Tests.Unit.Services;

public class IdentifierHasherTests
{
    private static readonly IIdentifierHasher Hasher = new IdentifierHasher(
        Options.Create(new IdentifierHasherSettings { SecretKey = "TestKeyMustBeAtLeast32CharactersLong!!" }));

    [Fact]
    public void Hash_WhenPlaintextProvided_Returns64CharHexString()
    {
        var result = Hasher.Hash(PreferredHouseholdIdType.Phone, "5551234567");

        Assert.NotNull(result);
        Assert.Equal(64, result!.Length);
        Assert.True(result.All(c => "0123456789ABCDEFabcdef".Contains(c)));
    }

    [Fact]
    public void Hash_WhenSameInput_ReturnsSameHash()
    {
        var hash1 = Hasher.Hash(PreferredHouseholdIdType.Ssn, "123456789");
        var hash2 = Hasher.Hash(PreferredHouseholdIdType.Ssn, "123456789");

        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void Hash_WhenDifferentInput_ReturnsDifferentHash()
    {
        var hash1 = Hasher.Hash(PreferredHouseholdIdType.Phone, "5551234567");
        var hash2 = Hasher.Hash(PreferredHouseholdIdType.Phone, "5551234568");

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void Hash_WhenNull_ReturnsNull()
    {
        var result = Hasher.Hash(PreferredHouseholdIdType.Phone, null);

        Assert.Null(result);
    }

    [Fact]
    public void Hash_WhenWhitespace_ReturnsNull()
    {
        var result = Hasher.Hash(PreferredHouseholdIdType.SnapId, "   ");

        Assert.Null(result);
    }

    [Fact]
    public void Hash_WhenSsn_NormalizesBeforeHashing()
    {
        var hash1 = Hasher.Hash(PreferredHouseholdIdType.Ssn, "123-45-6789");
        var hash2 = Hasher.Hash(PreferredHouseholdIdType.Ssn, "123456789");

        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void Matches_WhenPlaintextMatchesHash_ReturnsTrue()
    {
        var plaintext = "5551234567";
        var hash = Hasher.Hash(PreferredHouseholdIdType.Phone, plaintext);

        Assert.True(Hasher.Matches(PreferredHouseholdIdType.Phone, plaintext, hash));
    }

    [Fact]
    public void Matches_WhenPlaintextDoesNotMatchHash_ReturnsFalse()
    {
        var hash = Hasher.Hash(PreferredHouseholdIdType.Phone, "5551234567");

        Assert.False(Hasher.Matches(PreferredHouseholdIdType.Phone, "5551234568", hash));
    }

    [Fact]
    public void Matches_WhenStoredHashIsNull_ReturnsFalse()
    {
        Assert.False(Hasher.Matches(PreferredHouseholdIdType.SnapId, "value", null));
    }

    [Fact]
    public void HashForStorage_WhenPlaintext_ReturnsHash()
    {
        var result = Hasher.HashForStorage(PreferredHouseholdIdType.Phone, "5551234567");

        Assert.NotNull(result);
        Assert.Equal(64, result!.Length);
    }

    [Fact]
    public void HashForStorage_WhenAlreadyHash_PassesThrough()
    {
        var hash = Hasher.Hash(PreferredHouseholdIdType.Phone, "5551234567");
        var result = Hasher.HashForStorage(PreferredHouseholdIdType.Phone, hash);

        Assert.Equal(hash, result);
    }

    [Fact]
    public void HashForStorage_WhenNull_ReturnsNull()
    {
        var result = Hasher.HashForStorage(PreferredHouseholdIdType.Ssn, null);

        Assert.Null(result);
    }
}
