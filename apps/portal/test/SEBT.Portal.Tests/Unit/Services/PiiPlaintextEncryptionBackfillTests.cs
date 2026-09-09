using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SEBT.Portal.Core.Exceptions;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Core.Utilities;
using SEBT.Portal.Infrastructure.Data.Entities;
using SEBT.Portal.Infrastructure.Helpers;
using SEBT.Portal.Infrastructure.Services;
using SEBT.Portal.Tests.Unit.Repositories;
using SEBT.Portal.Tests.Unit.TestSupport;
using Xunit;

namespace SEBT.Portal.Tests.Unit.Services;

/// <seealso cref="SqlServerTestFixture"/>
[Collection("SqlServer")]
[Trait("Category", "SqlServer")]
public class PiiPlaintextEncryptionBackfillTests : IClassFixture<SqlServerTestFixture>
{
    private readonly SqlServerTestFixture _fixture;

    public PiiPlaintextEncryptionBackfillTests(SqlServerTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ApplyAsync_invokedTwice_leaves_stable_envelopes_and_hashes()
    {
        using var db = _fixture.CreateContext();
        var email = $"backfill-twice-{Guid.NewGuid()}@example.com";

        var user = UserFactory.CreateUserEntity(e =>
        {
            e.Email = email;
            e.EmailHash = null;
            e.Phone = "+15551234001";
            e.SnapId = "snap-plain";
            e.TanfId = "tanf-plain";
            e.DateOfBirth = "1988-06-01";
        });
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var challenge = new DocVerificationChallengeEntity
        {
            PublicId = Guid.CreateVersion7(),
            UserId = user.Id,
            ProofingDateOfBirth = "1990-01-15",
            ProofingIdType = "itin",
            ProofingIdValue = "111223333"
        };
        db.DocVerificationChallenges.Add(challenge);
        await db.SaveChangesAsync();

        async Task ApplyBackfillOnFreshContextAsync()
        {
            using var fresh = _fixture.CreateContext();
            var backfill = new PiiPlaintextEncryptionBackfill(
                fresh,
                TestPortalCryptography.PiiSymmetricEncryption,
                TestPortalCryptography.EmailLookupHasher,
                NullLogger<PiiPlaintextEncryptionBackfill>.Instance);
            await backfill.ApplyAsync();
        }

        await ApplyBackfillOnFreshContextAsync();

        db.ChangeTracker.Clear();
        var storedUserAfter1 = db.Users.Single(u => u.Id == user.Id);
        var fingerprint = TestPortalCryptography.FingerprintEmail(email);

        Assert.Equal(fingerprint, storedUserAfter1.EmailHash);
        Assert.StartsWith(PiiAesGcmSymmetricEncryption.EnvelopePrefix, storedUserAfter1.Email!, StringComparison.Ordinal);
        Assert.StartsWith(PiiAesGcmSymmetricEncryption.EnvelopePrefix, storedUserAfter1.Phone!, StringComparison.Ordinal);

        Assert.Equal(
            "1988-06-01",
            TestPortalCryptography.PiiSymmetricEncryption.DecryptOrPassThroughLegacy(storedUserAfter1.DateOfBirth));

        var storedChallengeAfter1 = db.DocVerificationChallenges.Single(c => c.Id == challenge.Id);
        Assert.StartsWith(
            PiiAesGcmSymmetricEncryption.EnvelopePrefix,
            storedChallengeAfter1.ProofingDateOfBirth!,
            StringComparison.Ordinal);
        Assert.Equal(
            "1990-01-15",
            TestPortalCryptography.PiiSymmetricEncryption.DecryptOrPassThroughLegacy(
                storedChallengeAfter1.ProofingDateOfBirth));

        // Second run: idempotent (no duplicate processing / exceptions).
        await ApplyBackfillOnFreshContextAsync();

        db.ChangeTracker.Clear();
        var storedUserAfter2 = db.Users.Single(u => u.Id == user.Id);
        Assert.Equal(storedUserAfter1.Email, storedUserAfter2.Email);
        Assert.Equal(storedUserAfter1.EmailHash, storedUserAfter2.EmailHash);

        var storedChallengeAfter2 = db.DocVerificationChallenges.Single(c => c.Id == challenge.Id);
        Assert.Equal(storedChallengeAfter1.ProofingDateOfBirth, storedChallengeAfter2.ProofingDateOfBirth);

        Assert.Equal(
            "snap-plain",
            TestPortalCryptography.PiiSymmetricEncryption.DecryptOrPassThroughLegacy(storedUserAfter2.SnapId));
        Assert.Equal(
            EmailNormalizer.Normalize(email),
            TestPortalCryptography.PiiSymmetricEncryption.DecryptOrPassThroughLegacy(storedUserAfter2.Email));
    }

    [Fact]
    public async Task ApplyAsync_whenDecryptFails_doesNotPartiallyCommitBatch()
    {
        using var seedDb = _fixture.CreateContext();
        var firstEmail = $"backfill-fail-first-{Guid.NewGuid()}@example.com";
        var secondEmail = $"backfill-fail-second-{Guid.NewGuid()}@example.com";

        seedDb.Users.Add(UserFactory.CreateUserEntity(e =>
        {
            e.Email = firstEmail;
            e.EmailHash = null;
            e.Phone = "+15555550101";
        }));
        seedDb.Users.Add(UserFactory.CreateUserEntity(e =>
        {
            e.Email = secondEmail;
            e.EmailHash = null;
            e.Phone = "+15555550102";
        }));
        await seedDb.SaveChangesAsync();

        // Throw on second row in the same batch to verify SaveChanges is not partially applied.
        var throwingCrypto = new ThrowOnNthDecryptCrypto(
            inner: TestPortalCryptography.PiiSymmetricEncryption,
            throwOnCall: 2);

        using var runDb = _fixture.CreateContext();
        var backfill = new PiiPlaintextEncryptionBackfill(
            runDb,
            throwingCrypto,
            TestPortalCryptography.EmailLookupHasher,
            NullLogger<PiiPlaintextEncryptionBackfill>.Instance);

        await Assert.ThrowsAsync<PiiDecryptException>(() => backfill.ApplyAsync());

        using var verifyDb = _fixture.CreateContext();
        var users = await verifyDb.Users
            .Where(u => u.Email == firstEmail || u.Email == secondEmail)
            .OrderBy(u => u.Email)
            .ToListAsync();

        Assert.Equal(2, users.Count);
        Assert.All(users, u =>
        {
            Assert.DoesNotContain(PiiAesGcmSymmetricEncryption.EnvelopePrefix, u.Email ?? string.Empty, StringComparison.Ordinal);
            Assert.Null(u.EmailHash);
            Assert.DoesNotContain(PiiAesGcmSymmetricEncryption.EnvelopePrefix, u.Phone ?? string.Empty, StringComparison.Ordinal);
        });
    }

    private sealed class ThrowOnNthDecryptCrypto : IPiiSymmetricEncryption
    {
        private readonly IPiiSymmetricEncryption _inner;
        private readonly int _throwOnCall;
        private int _callCount;

        public ThrowOnNthDecryptCrypto(IPiiSymmetricEncryption inner, int throwOnCall)
        {
            _inner = inner;
            _throwOnCall = throwOnCall;
        }

        public bool IsEnvelope(string? storedValue) => _inner.IsEnvelope(storedValue);

        public string? Encrypt(string? plaintext) => _inner.Encrypt(plaintext);

        public string Decrypt(string storedValue) => _inner.Decrypt(storedValue);

        public string? DecryptOrPassThroughLegacy(string? storedValue)
        {
            _callCount++;
            if (_callCount == _throwOnCall)
            {
                throw new PiiDecryptException("Synthetic decrypt failure for batch rollback test.");
            }

            return _inner.DecryptOrPassThroughLegacy(storedValue);
        }

        public string ReSealWithActiveEncryptor(string envelopeCiphertext) =>
            _inner.ReSealWithActiveEncryptor(envelopeCiphertext);
    }
}
