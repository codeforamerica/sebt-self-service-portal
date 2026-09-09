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

    [Fact]
    public void HashForAnalytics_WhenPlaintextProvided_Returns64CharLowercaseHex()
    {
        var result = Hasher.HashForAnalytics("APP-2024-0001");

        Assert.NotNull(result);
        Assert.Equal(64, result!.Length);
        Assert.True(result.All(c => "0123456789abcdef".Contains(c)),
            "HashForAnalytics must emit lowercase hex; external pipelines reproduce the digest from a published spec.");
    }

    [Fact]
    public void HashForAnalytics_WhenSameInputAndSecret_ReturnsSameHash()
    {
        var hash1 = Hasher.HashForAnalytics("APP-2024-0001");
        var hash2 = Hasher.HashForAnalytics("APP-2024-0001");

        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void HashForAnalytics_DoesNotNormalizeInput()
    {
        // Storage-side Hash() trims and strips dashes/spaces. Analytics digest
        // must NOT normalize so external pipelines hashing the raw SEBT App ID
        // get the same answer.
        var withDash = Hasher.HashForAnalytics("APP-2024-0001");
        var withoutDash = Hasher.HashForAnalytics("APP20240001");
        var withWhitespace = Hasher.HashForAnalytics(" APP-2024-0001 ");

        Assert.NotEqual(withDash, withoutDash);
        Assert.NotEqual(withDash, withWhitespace);
    }

    [Fact]
    public void HashForAnalytics_WhenNullOrWhitespace_ReturnsNull()
    {
        Assert.Null(Hasher.HashForAnalytics(null));
        Assert.Null(Hasher.HashForAnalytics(""));
        Assert.Null(Hasher.HashForAnalytics("   "));
    }

    [Fact]
    public void HashForAnalytics_WhenSecretChanges_DigestChanges()
    {
        var hasherA = new IdentifierHasher(
            Options.Create(new IdentifierHasherSettings { SecretKey = "TestKeyMustBeAtLeast32CharactersLong!!" }));
        var hasherB = new IdentifierHasher(
            Options.Create(new IdentifierHasherSettings { SecretKey = "DifferentKeyAlsoAtLeast32CharsLong!!!!" }));

        Assert.NotEqual(
            hasherA.HashForAnalytics("APP-2024-0001"),
            hasherB.HashForAnalytics("APP-2024-0001"));
    }

    [Fact]
    public void HashForAnalytics_UsesAnalyticsKey_WhenConfiguredSeparately()
    {
        // Storage key and analytics key are independent: rotating one must not
        // affect the other. With distinct keys, the same plaintext produces
        // different digests for storage vs. analytics.
        var hasher = new IdentifierHasher(Options.Create(new IdentifierHasherSettings
        {
            SecretKey = "StorageKeyMustBeAtLeast32CharsLong!!!!",
            AnalyticsSecretKey = "AnalyticsKeyMustBeAtLeast32CharsLong!!"
        }));

        var storage = hasher.Hash("APP20240001");
        var analytics = hasher.HashForAnalytics("APP20240001");

        Assert.NotNull(storage);
        Assert.NotNull(analytics);
        Assert.NotEqual(storage, analytics, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void HashForAnalytics_FallsBackToSecretKey_WhenAnalyticsKeyAbsent()
    {
        // Back-compat: deployments that haven't configured the new analytics
        // key still emit a stable digest using SecretKey.
        var fallbackHasher = new IdentifierHasher(Options.Create(new IdentifierHasherSettings
        {
            SecretKey = "SharedKeyMustBeAtLeast32CharsLong!!!!!"
        }));
        var explicitHasher = new IdentifierHasher(Options.Create(new IdentifierHasherSettings
        {
            SecretKey = "SharedKeyMustBeAtLeast32CharsLong!!!!!",
            AnalyticsSecretKey = "SharedKeyMustBeAtLeast32CharsLong!!!!!"
        }));

        Assert.Equal(
            fallbackHasher.HashForAnalytics("APP-2024-0001"),
            explicitHasher.HashForAnalytics("APP-2024-0001"));
    }

    [Fact]
    public void HashForAnalytics_TestVector_MatchesPublishedReference()
    {
        // This vector is the contract published in docs/analytics/hashed-sebt-app-id.md
        // and reproduced by docs/analytics/scripts/hash_sebt_app_id.py. If this test
        // fails after a hasher change, every external consumer's digest also broke.
        // Either revert the change or update the spec, script, and downstream
        // consumers in lockstep.
        const string testSecret = "TestVectorSecret_AtLeast32Bytes_!!!!";
        const string testInput = "APP-2024-0001";
        const string expected = "ca383d90647e371547d6e66297cda8089b81fc1c5cb30da6cfcbdf744d9e2861";

        // Set AnalyticsSecretKey explicitly so the vector is reproducible
        // regardless of the SecretKey fallback behavior changing later.
        var hasher = new IdentifierHasher(Options.Create(new IdentifierHasherSettings
        {
            SecretKey = testSecret,
            AnalyticsSecretKey = testSecret
        }));

        Assert.Equal(expected, hasher.HashForAnalytics(testInput));
    }
}
