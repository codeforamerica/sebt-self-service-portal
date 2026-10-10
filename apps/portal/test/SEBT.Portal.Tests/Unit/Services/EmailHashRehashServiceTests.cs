using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Infrastructure.Data;
using SEBT.Portal.Infrastructure.Services;
using SEBT.Portal.Tests.Unit.TestSupport;

namespace SEBT.Portal.Tests.Unit.Services;

public class EmailHashRehashServiceTests
{
    private static EmailHashRehashService CreateService(
        PortalDbContext? context = null,
        IPiiSymmetricEncryption? crypto = null,
        IEmailLookupHasher? hasher = null)
    {
        return new EmailHashRehashService(
            context!,
            crypto ?? TestPortalCryptography.PiiSymmetricEncryption,
            hasher ?? TestPortalCryptography.EmailLookupHasher,
            NullLogger<EmailHashRehashService>.Instance);
    }

    [Fact]
    public void PlanRow_WhenHashAlreadyMatches_ReturnsAlreadyCurrent()
    {
        var email = "user@example.com";
        var normalized = email.ToLowerInvariant();
        var hash = TestPortalCryptography.EmailLookupHasher.HashNormalized(normalized);
        var stored = TestPortalCryptography.PiiSymmetricEncryption.Encrypt(normalized)!;
        var service = CreateService();

        var plan = service.PlanRow(Guid.NewGuid(), stored, hash);

        Assert.Equal(EmailHashRehashService.RehashRowStatus.AlreadyCurrent, plan.Status);
        Assert.Equal(hash, plan.NewHash);
    }

    [Fact]
    public void PlanRow_WhenHashStale_ReturnsNeedsUpdate()
    {
        var email = "stale@example.com";
        var normalized = email.ToLowerInvariant();
        var currentHash = TestPortalCryptography.EmailLookupHasher.HashNormalized(normalized);
        var staleHasher = new EmailLookupHasher(Options.Create(new IdentifierHasherSettings
        {
            SecretKey = "BackfillSecretKeyMustBeAtLeast32Chars!!"
        }));
        var staleHash = staleHasher.HashNormalized(normalized);
        Assert.NotEqual(staleHash, currentHash);

        var stored = TestPortalCryptography.PiiSymmetricEncryption.Encrypt(normalized)!;
        var service = CreateService();

        var plan = service.PlanRow(Guid.NewGuid(), stored, staleHash);

        Assert.Equal(EmailHashRehashService.RehashRowStatus.NeedsUpdate, plan.Status);
        Assert.Equal(currentHash, plan.NewHash);
    }

    [Fact]
    public void PlanRow_WhenPlaintextEmail_WorksWithEncryptAtRestOff()
    {
        var normalized = "plain@example.com";
        var hash = TestPortalCryptography.EmailLookupHasher.HashNormalized(normalized);
        var plaintextCrypto = new ConditionalPiiSymmetricEncryption(
            new PiiAesGcmSymmetricEncryption(TestPortalCryptography.PiiOptions),
            Options.Create(new PiiEncryptionSettings
            {
                EncryptAtRest = false,
                ActiveKeyId = TestPortalCryptography.PiiOptions.Value.ActiveKeyId,
                Keys = TestPortalCryptography.PiiOptions.Value.Keys
            }));
        var service = CreateService(crypto: plaintextCrypto);

        var plan = service.PlanRow(Guid.NewGuid(), normalized, "wrong-hash");

        Assert.Equal(EmailHashRehashService.RehashRowStatus.NeedsUpdate, plan.Status);
        Assert.Equal(hash, plan.NewHash);
    }

    [Fact]
    public void FindCollisionUserIds_WhenTwoUsersShareNewHash_ReturnsBothIds()
    {
        var hash = "ABC123";
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var id3 = Guid.NewGuid();

        var collisions = EmailHashRehashService.FindCollisionUserIds(
        [
            new EmailHashRehashService.PlannedRehash(id1, hash, EmailHashRehashService.RehashRowStatus.NeedsUpdate),
            new EmailHashRehashService.PlannedRehash(id2, hash, EmailHashRehashService.RehashRowStatus.NeedsUpdate),
            new EmailHashRehashService.PlannedRehash(
                id3,
                "OTHER",
                EmailHashRehashService.RehashRowStatus.NeedsUpdate)
        ]);

        Assert.Contains(id1, collisions);
        Assert.Contains(id2, collisions);
        Assert.DoesNotContain(id3, collisions);
    }

    [Fact]
    public void PlanRow_WhenDecryptThrows_ReturnsDecryptFailed()
    {
        var crypto = Substitute.For<IPiiSymmetricEncryption>();
        crypto.DecryptOrPassThroughLegacy(Arg.Any<string?>())
            .Returns(_ => throw new InvalidOperationException("boom"));
        var service = CreateService(crypto: crypto);

        var plan = service.PlanRow(Guid.NewGuid(), "sep-pii:v1:garbage", "hash");

        Assert.Equal(EmailHashRehashService.RehashRowStatus.DecryptFailed, plan.Status);
        Assert.Null(plan.NewHash);
    }
}
