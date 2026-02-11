using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;
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
        var result = Hasher.Hash("123456789");

        Assert.NotNull(result);
        Assert.Equal(64, result!.Length);
        Assert.True(result.All(c => "0123456789ABCDEFabcdef".Contains(c)));
    }

    [Fact]
    public void Hash_WhenSameInput_ReturnsSameHash()
    {
        var hash1 = Hasher.Hash("123456789");
        var hash2 = Hasher.Hash("123456789");

        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void Hash_WhenDifferentInput_ReturnsDifferentHash()
    {
        var hash1 = Hasher.Hash("123456789");
        var hash2 = Hasher.Hash("123456788");

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void Hash_WhenNull_ReturnsNull()
    {
        var result = Hasher.Hash(null);

        Assert.Null(result);
    }

    [Fact]
    public void Hash_WhenWhitespace_ReturnsNull()
    {
        var result = Hasher.Hash("   ");

        Assert.Null(result);
    }

    [Fact]
    public void Hash_WhenSsn_NormalizesBeforeHashing()
    {
        var hash1 = Hasher.Hash("123-45-6789");
        var hash2 = Hasher.Hash("123456789");

        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void Matches_WhenPlaintextMatchesHash_ReturnsTrue()
    {
        var plaintext = "123456789";
        var hash = Hasher.Hash(plaintext);

        Assert.True(Hasher.Matches(plaintext, hash));
    }

    [Fact]
    public void Matches_WhenPlaintextDoesNotMatchHash_ReturnsFalse()
    {
        var hash = Hasher.Hash("123456789");

        Assert.False(Hasher.Matches("123456788", hash));
    }

    [Fact]
    public void Matches_WhenStoredHashIsNull_ReturnsFalse()
    {
        Assert.False(Hasher.Matches("value", null));
    }

    [Fact]
    public void HashForStorage_WhenPlaintext_ReturnsHash()
    {
        var result = Hasher.HashForStorage("123456789");

        Assert.NotNull(result);
        Assert.Equal(64, result!.Length);
    }

    [Fact]
    public void HashForStorage_WhenAlreadyHash_PassesThrough()
    {
        var hash = Hasher.Hash("123456789");
        var result = Hasher.HashForStorage(hash);

        Assert.Equal(hash, result);
    }

    [Fact]
    public void HashForStorage_WhenNull_ReturnsNull()
    {
        var result = Hasher.HashForStorage(null);

        Assert.Null(result);
    }
}
