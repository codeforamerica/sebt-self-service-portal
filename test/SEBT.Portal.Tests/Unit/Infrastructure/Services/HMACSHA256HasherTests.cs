using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Infrastructure.Services;
using SEBT.Portal.StatesPlugins.Interfaces.Services;

namespace SEBT.Portal.Tests.Unit.Infrastructure.Services;

public class HMACSHA256HasherTests
{
    private const string TestKey = "test-key-of-at-least-32-characters-long-aaaaa";

    private static IHMACSHA256Hasher CreateSut(string? key = null)
    {
        var settings = new IdentifierHasherSettings { SecretKey = key ?? TestKey };
        return new HMACSHA256Hasher(Options.Create(settings));
    }

    [Fact]
    public void Hash_returns_64_character_lowercase_hex()
    {
        var sut = CreateSut();

        var result = sut.Hash("hello");

        Assert.Equal(64, result.Length);
        Assert.Matches("^[0-9a-f]{64}$", result);
    }

    [Fact]
    public void Hash_is_deterministic_for_same_input_and_key()
    {
        var sut = CreateSut();

        Assert.Equal(sut.Hash("phone-1234567890"), sut.Hash("phone-1234567890"));
    }

    [Fact]
    public void Hash_differs_for_different_inputs()
    {
        var sut = CreateSut();

        Assert.NotEqual(sut.Hash("alpha"), sut.Hash("beta"));
    }

    [Fact]
    public void Hash_differs_for_different_keys_on_same_input()
    {
        var sutA = CreateSut("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
        var sutB = CreateSut("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");

        Assert.NotEqual(sutA.Hash("same-input"), sutB.Hash("same-input"));
    }

    [Fact]
    public void Hash_matches_reference_HMACSHA256_computation()
    {
        var sut = CreateSut();
        var expected = Convert.ToHexString(
            HMACSHA256.HashData(Encoding.UTF8.GetBytes(TestKey), Encoding.UTF8.GetBytes("known-input"))
        ).ToLowerInvariant();

        Assert.Equal(expected, sut.Hash("known-input"));
    }

    [Fact]
    public void Hash_throws_when_input_is_null()
    {
        var sut = CreateSut();

        Assert.Throws<ArgumentNullException>(() => sut.Hash(null!));
    }

    [Fact]
    public void Constructor_throws_when_key_missing()
    {
        var settings = new IdentifierHasherSettings { SecretKey = string.Empty };

        Assert.Throws<InvalidOperationException>(
            () => new HMACSHA256Hasher(Options.Create(settings)));
    }

    [Fact]
    public void Constructor_throws_when_key_too_short()
    {
        var settings = new IdentifierHasherSettings { SecretKey = "too-short" };

        Assert.Throws<InvalidOperationException>(
            () => new HMACSHA256Hasher(Options.Create(settings)));
    }
}
