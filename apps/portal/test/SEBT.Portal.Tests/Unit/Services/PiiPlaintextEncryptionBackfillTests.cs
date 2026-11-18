using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SEBT.Portal.Core.Exceptions;
using SEBT.Portal.Core.Models.UserActions;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Core.Utilities;
using SEBT.Portal.Infrastructure.Data;
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
public class PiiPlaintextEncryptionBackfillTests : IAsyncLifetime
{
    private readonly SqlServerTestFixture _fixture;
    private string _connectionString = null!;

    public PiiPlaintextEncryptionBackfillTests(SqlServerTestFixture fixture)
    {
        _fixture = fixture;
    }

    // Isolated database: the backfill rewrites every legacy row, so other classes' rows would change its decrypt order.
    public async Task InitializeAsync()
    {
        _connectionString = await _fixture.GetIsolatedDatabaseAsync(nameof(PiiPlaintextEncryptionBackfillTests));
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private PortalDbContext CreateContext() => SqlServerTestFixture.CreateContext(_connectionString);

    [Fact]
    public async Task ApplyAsync_invokedTwice_leaves_stable_envelopes_and_hashes()
    {
        using var db = CreateContext();
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
    public async Task ApplyAsync_encrypts_plaintext_user_case_identifiers_idempotently()
    {
        using var db = CreateContext();
        var user = UserFactory.CreateUserEntity();
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var lastSeenAt = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var applicationCase = new UserCaseEntity
        {
            UserId = user.Id,
            CaseId = "SEBT-CASE-001",
            CaseIdHash = "case-hash-001",
            ApplicationId = "APP-001",
            ApplicationIdHash = "app-hash-001",
            ApplicationStudentId = "STU-001",
            ApplicationStudentIdHash = "stu-hash-001",
            LastSeenAt = lastSeenAt
        };
        var autoIssuedCase = new UserCaseEntity
        {
            UserId = user.Id,
            CaseId = "SEBT-CASE-002",
            CaseIdHash = "case-hash-002",
            LastSeenAt = lastSeenAt
        };
        db.UserCases.AddRange(applicationCase, autoIssuedCase);
        await db.SaveChangesAsync();

        await ApplyBackfillOnFreshContextAsync();

        db.ChangeTracker.Clear();
        var crypto = TestPortalCryptography.PiiSymmetricEncryption;
        var storedApplicationCase = db.UserCases.Single(c => c.Id == applicationCase.Id);
        Assert.StartsWith(PiiAesGcmSymmetricEncryption.EnvelopePrefix, storedApplicationCase.CaseId, StringComparison.Ordinal);
        Assert.StartsWith(PiiAesGcmSymmetricEncryption.EnvelopePrefix, storedApplicationCase.ApplicationId!, StringComparison.Ordinal);
        Assert.StartsWith(PiiAesGcmSymmetricEncryption.EnvelopePrefix, storedApplicationCase.ApplicationStudentId!, StringComparison.Ordinal);
        Assert.Equal("SEBT-CASE-001", crypto.DecryptOrPassThroughLegacy(storedApplicationCase.CaseId));
        Assert.Equal("APP-001", crypto.DecryptOrPassThroughLegacy(storedApplicationCase.ApplicationId));
        Assert.Equal("STU-001", crypto.DecryptOrPassThroughLegacy(storedApplicationCase.ApplicationStudentId));
        Assert.Equal("case-hash-001", storedApplicationCase.CaseIdHash);
        Assert.Equal(lastSeenAt, storedApplicationCase.LastSeenAt);

        var storedAutoIssuedCase = db.UserCases.Single(c => c.Id == autoIssuedCase.Id);
        Assert.Equal("SEBT-CASE-002", crypto.DecryptOrPassThroughLegacy(storedAutoIssuedCase.CaseId));
        Assert.Null(storedAutoIssuedCase.ApplicationId);
        Assert.Null(storedAutoIssuedCase.ApplicationStudentId);

        await ApplyBackfillOnFreshContextAsync();

        db.ChangeTracker.Clear();
        var storedApplicationCaseAfter2 = db.UserCases.Single(c => c.Id == applicationCase.Id);
        Assert.Equal(storedApplicationCase.CaseId, storedApplicationCaseAfter2.CaseId);
        Assert.Equal(storedApplicationCase.ApplicationId, storedApplicationCaseAfter2.ApplicationId);
        Assert.Equal(storedApplicationCase.ApplicationStudentId, storedApplicationCaseAfter2.ApplicationStudentId);
    }

    [Fact]
    public async Task ApplyAsync_encrypts_plaintext_user_action_identifiers_idempotently()
    {
        using var db = CreateContext();
        var user = UserFactory.CreateUserEntity();
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var createdAt = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var applicationAction = new UserActionEntity
        {
            UserId = user.Id,
            Action = UserActionType.RequestCardReplacement,
            Outcome = UserActionOutcome.Succeeded,
            CaseId = "SEBT-CASE-001",
            CaseIdHash = "case-hash-001",
            ApplicationId = "APP-001",
            ApplicationIdHash = "app-hash-001",
            ApplicationStudentId = "STU-001",
            ApplicationStudentIdHash = "stu-hash-001",
            CreatedAt = createdAt
        };
        var autoIssuedAction = new UserActionEntity
        {
            UserId = user.Id,
            Action = UserActionType.UpdateAddress,
            Outcome = UserActionOutcome.Failed,
            CaseId = "SEBT-CASE-002",
            CaseIdHash = "case-hash-002",
            CreatedAt = createdAt
        };
        db.UserActions.AddRange(applicationAction, autoIssuedAction);
        await db.SaveChangesAsync();

        await ApplyBackfillOnFreshContextAsync();

        db.ChangeTracker.Clear();
        var crypto = TestPortalCryptography.PiiSymmetricEncryption;
        var storedApplicationAction = db.UserActions.Single(a => a.Id == applicationAction.Id);
        Assert.StartsWith(PiiAesGcmSymmetricEncryption.EnvelopePrefix, storedApplicationAction.CaseId, StringComparison.Ordinal);
        Assert.StartsWith(PiiAesGcmSymmetricEncryption.EnvelopePrefix, storedApplicationAction.ApplicationId!, StringComparison.Ordinal);
        Assert.StartsWith(PiiAesGcmSymmetricEncryption.EnvelopePrefix, storedApplicationAction.ApplicationStudentId!, StringComparison.Ordinal);
        Assert.Equal("SEBT-CASE-001", crypto.DecryptOrPassThroughLegacy(storedApplicationAction.CaseId));
        Assert.Equal("APP-001", crypto.DecryptOrPassThroughLegacy(storedApplicationAction.ApplicationId));
        Assert.Equal("STU-001", crypto.DecryptOrPassThroughLegacy(storedApplicationAction.ApplicationStudentId));
        Assert.Equal("case-hash-001", storedApplicationAction.CaseIdHash);
        Assert.Equal(createdAt, storedApplicationAction.CreatedAt);

        var storedAutoIssuedAction = db.UserActions.Single(a => a.Id == autoIssuedAction.Id);
        Assert.Equal("SEBT-CASE-002", crypto.DecryptOrPassThroughLegacy(storedAutoIssuedAction.CaseId));
        Assert.Null(storedAutoIssuedAction.ApplicationId);
        Assert.Null(storedAutoIssuedAction.ApplicationStudentId);

        await ApplyBackfillOnFreshContextAsync();

        db.ChangeTracker.Clear();
        var storedApplicationActionAfter2 = db.UserActions.Single(a => a.Id == applicationAction.Id);
        Assert.Equal(storedApplicationAction.CaseId, storedApplicationActionAfter2.CaseId);
        Assert.Equal(storedApplicationAction.ApplicationId, storedApplicationActionAfter2.ApplicationId);
        Assert.Equal(storedApplicationAction.ApplicationStudentId, storedApplicationActionAfter2.ApplicationStudentId);
    }

    private async Task ApplyBackfillOnFreshContextAsync()
    {
        using var fresh = CreateContext();
        var backfill = new PiiPlaintextEncryptionBackfill(
            fresh,
            TestPortalCryptography.PiiSymmetricEncryption,
            TestPortalCryptography.EmailLookupHasher,
            NullLogger<PiiPlaintextEncryptionBackfill>.Instance);
        await backfill.ApplyAsync();
    }

    [Fact]
    public async Task ApplyAsync_whenDecryptFails_doesNotPartiallyCommitBatch()
    {
        using var seedDb = CreateContext();
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

        using var runDb = CreateContext();
        var backfill = new PiiPlaintextEncryptionBackfill(
            runDb,
            throwingCrypto,
            TestPortalCryptography.EmailLookupHasher,
            NullLogger<PiiPlaintextEncryptionBackfill>.Instance);

        await Assert.ThrowsAsync<PiiDecryptException>(() => backfill.ApplyAsync());

        using var verifyDb = CreateContext();
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
