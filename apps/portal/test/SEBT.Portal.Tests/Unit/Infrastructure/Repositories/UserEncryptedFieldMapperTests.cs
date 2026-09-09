using NSubstitute;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Infrastructure.Data.Entities;
using SEBT.Portal.Infrastructure.Repositories;
using SEBT.Portal.Tests.Unit.TestSupport;

namespace SEBT.Portal.Tests.Unit.Infrastructure.Repositories;

/// <summary>
/// Unit tests for <see cref="UserEncryptedFieldMapper"/>. No database required.
/// </summary>
public class UserEncryptedFieldMapperTests
{
    private static readonly IPiiSymmetricEncryption Crypto =
        TestPortalCryptography.PiiSymmetricEncryption;

    private static readonly IEmailLookupHasher Lookup =
        TestPortalCryptography.EmailLookupHasher;

    [Fact]
    public void ToDomain_roundTripsEncryptedFields()
    {
        var id = Guid.CreateVersion7();
        var createdAt = DateTime.UtcNow.AddDays(-2);
        var updatedAt = DateTime.UtcNow.AddDays(-1);
        var completedAt = DateTime.UtcNow.AddHours(-3);
        var coLoadedAt = DateTime.UtcNow.AddDays(-5);
        var dob = new DateOnly(1990, 4, 15);

        var entity = new UserEntity
        {
            Id = id,
            Email = Crypto.Encrypt("person@example.com"),
            EmailHash = Lookup.NormalizeAndHash("person@example.com"),
            ExternalProviderId = "oidc-sub",
            IdProofingStatus = (int)IdProofingStatus.Completed,
            IalLevel = (int)UserIalLevel.IAL1plus,
            IdProofingSessionId = "session-1",
            IdProofingCompletedAt = completedAt,
            DateOfBirth = Crypto.Encrypt("1990-04-15"),
            IsCoLoaded = true,
            CoLoadedLastUpdated = coLoadedAt,
            Phone = Crypto.Encrypt("phone-ext"),
            SnapId = Crypto.Encrypt("SNAP1"),
            TanfId = Crypto.Encrypt("TANF1"),
            Ssn = "stored-hash",
            IdProofingAttemptCount = 2,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };

        var user = UserEncryptedFieldMapper.ToDomain(entity, Crypto);

        Assert.Equal(id, user.Id);
        Assert.Equal("person@example.com", user.Email);
        Assert.Equal("oidc-sub", user.ExternalProviderId);
        Assert.Equal(IdProofingStatus.Completed, user.IdProofingStatus);
        Assert.Equal(UserIalLevel.IAL1plus, user.IalLevel);
        Assert.Equal("session-1", user.IdProofingSessionId);
        Assert.Equal(completedAt, user.IdProofingCompletedAt);
        Assert.Equal(dob, user.DateOfBirth);
        Assert.True(user.IsCoLoaded);
        Assert.Equal(coLoadedAt, user.CoLoadedLastUpdated);
        Assert.Equal("phone-ext", user.Phone);
        Assert.Equal("SNAP1", user.SnapId);
        Assert.Equal("TANF1", user.TanfId);
        Assert.Equal("stored-hash", user.Ssn);
        Assert.Equal(2, user.IdProofingAttemptCount);
        Assert.Equal(createdAt, user.CreatedAt);
        Assert.Equal(updatedAt, user.UpdatedAt);
    }

    [Fact]
    public void ToDomain_whenEmailIsEmpty_returnsNullEmail()
    {
        var entity = new UserEntity { Email = null };

        var user = UserEncryptedFieldMapper.ToDomain(entity, Crypto);

        Assert.Null(user.Email);
    }

    [Fact]
    public void ToDomain_normalizesDecryptedEmail()
    {
        var entity = new UserEntity { Email = Crypto.Encrypt("  Person@Example.COM  ") };

        var user = UserEncryptedFieldMapper.ToDomain(entity, Crypto);

        Assert.Equal("person@example.com", user.Email);
    }

    [Fact]
    public void ToDomain_whenDateOfBirthIsEmpty_returnsNull()
    {
        var entity = new UserEntity { DateOfBirth = null };

        var user = UserEncryptedFieldMapper.ToDomain(entity, Crypto);

        Assert.Null(user.DateOfBirth);
    }

    [Fact]
    public void ToDomain_parsesNonExactDateOfBirth()
    {
        var entity = new UserEntity { DateOfBirth = Crypto.Encrypt("1990/04/15") };

        var user = UserEncryptedFieldMapper.ToDomain(entity, Crypto);

        Assert.Equal(new DateOnly(1990, 4, 15), user.DateOfBirth);
    }

    [Fact]
    public void ToDomain_whenDateOfBirthIsUnparseable_returnsNull()
    {
        var entity = new UserEntity { DateOfBirth = Crypto.Encrypt("not-a-date") };

        var user = UserEncryptedFieldMapper.ToDomain(entity, Crypto);

        Assert.Null(user.DateOfBirth);
    }

    [Fact]
    public void ToDomain_whenDecryptedDateOfBirthIsWhitespace_returnsNull()
    {
        var crypto = Substitute.For<IPiiSymmetricEncryption>();
        crypto.DecryptOrPassThroughLegacy("stored").Returns("   ");

        var entity = new UserEntity { DateOfBirth = "stored" };

        var user = UserEncryptedFieldMapper.ToDomain(entity, crypto);

        Assert.Null(user.DateOfBirth);
    }

    [Fact]
    public void EncryptIdentifiers_whenIncludeEmailColumns_writesEmailAndHash()
    {
        var entity = new UserEntity();
        var user = new User
        {
            Email = "Person@Example.COM",
            Phone = "phone-ext",
            SnapId = "SNAP1",
            TanfId = "TANF1",
            DateOfBirth = new DateOnly(1990, 4, 15)
        };

        UserEncryptedFieldMapper.EncryptIdentifiers(
            entity, user, Crypto, Lookup, includeEmailColumns: true);

        Assert.Equal("person@example.com", Crypto.DecryptOrPassThroughLegacy(entity.Email));
        Assert.Equal(Lookup.NormalizeAndHash("Person@Example.COM"), entity.EmailHash);
        Assert.Equal("phone-ext", Crypto.DecryptOrPassThroughLegacy(entity.Phone));
        Assert.Equal("SNAP1", Crypto.DecryptOrPassThroughLegacy(entity.SnapId));
        Assert.Equal("TANF1", Crypto.DecryptOrPassThroughLegacy(entity.TanfId));
        Assert.Equal("1990-04-15", Crypto.DecryptOrPassThroughLegacy(entity.DateOfBirth));
    }

    [Fact]
    public void EncryptIdentifiers_whenEmailMissing_clearsEmailColumns()
    {
        var entity = new UserEntity
        {
            Email = "leftover",
            EmailHash = "leftover-hash"
        };
        var user = new User { Email = null };

        UserEncryptedFieldMapper.EncryptIdentifiers(
            entity, user, Crypto, Lookup, includeEmailColumns: true);

        Assert.Null(entity.Email);
        Assert.Null(entity.EmailHash);
    }

    [Fact]
    public void EncryptIdentifiers_whenIncludeEmailColumnsIsFalse_leavesEmailColumns()
    {
        var entity = new UserEntity
        {
            Email = "keep-me",
            EmailHash = "keep-hash"
        };
        var user = new User
        {
            Email = "ignored@example.com",
            Phone = "phone-ext"
        };

        UserEncryptedFieldMapper.EncryptIdentifiers(
            entity, user, Crypto, Lookup, includeEmailColumns: false);

        Assert.Equal("keep-me", entity.Email);
        Assert.Equal("keep-hash", entity.EmailHash);
        Assert.Equal("phone-ext", Crypto.DecryptOrPassThroughLegacy(entity.Phone));
    }

    [Fact]
    public void ClearEmailColumns_nullsEmailAndHash()
    {
        var entity = new UserEntity
        {
            Email = "keep-me",
            EmailHash = "keep-hash",
            Phone = "unchanged"
        };

        UserEncryptedFieldMapper.ClearEmailColumns(entity);

        Assert.Null(entity.Email);
        Assert.Null(entity.EmailHash);
        Assert.Equal("unchanged", entity.Phone);
    }
}
