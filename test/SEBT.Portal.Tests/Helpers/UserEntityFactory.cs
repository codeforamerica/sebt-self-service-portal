using Bogus;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Infrastructure.Data.Entities;

namespace SEBT.Portal.Tests.Helpers;

/// <summary>
/// Factory for creating UserEntity instances for testing.
/// Located in test project to avoid including Bogus in production builds.
/// </summary>
public static class UserEntityFactory
{
    private static readonly Faker<User> UserFaker = new Faker<User>()
        .RuleFor(u => u.Id, f => 0)
        .RuleFor(u => u.Email, f => f.Internet.Email().ToLowerInvariant())
        .RuleFor(u => u.IdProofingStatus, f => f.PickRandom<IdProofingStatus>())
        .RuleFor(u => u.IdProofingSessionId, f => f.Random.Guid().ToString())
        .RuleFor(u => u.IdProofingCompletedAt, (f, u) =>
            u.IdProofingStatus == IdProofingStatus.Completed || u.IdProofingStatus == IdProofingStatus.Expired
                ? f.Date.Recent(30)
                : null)
        .RuleFor(u => u.IdProofingExpiresAt, (f, u) =>
            u.IdProofingCompletedAt?.AddYears(1))
        .RuleFor(u => u.IsCoLoaded, f => f.Random.Bool(0.3f))
        .RuleFor(u => u.CoLoadedLastUpdated, (f, u) =>
            u.IsCoLoaded ? f.Date.Recent(60) : null)
        .RuleFor(u => u.CreatedAt, f => f.Date.Past(1))
        .RuleFor(u => u.UpdatedAt, (f, u) => f.Date.Between(u.CreatedAt, DateTime.UtcNow));

    /// <summary>
    /// Creates a new UserEntity instance with realistic fake data.
    /// </summary>
    /// <param name="customize">Optional action to customize the generated entity.</param>
    /// <returns>A new UserEntity instance.</returns>
    public static UserEntity CreateUserEntity(Action<UserEntity>? customize = null)
    {
        var user = UserFaker.Generate();
        var entity = MapToEntity(user);
        customize?.Invoke(entity);
        return entity;
    }

    /// <summary>
    /// Creates a UserEntity with co-loaded status set to true.
    /// </summary>
    /// <param name="customize">Optional action to further customize the entity.</param>
    /// <returns>A UserEntity instance with IsCoLoaded = true.</returns>
    public static UserEntity CreateCoLoadedUserEntity(Action<UserEntity>? customize = null)
    {
        var user = UserFaker.Generate();
        user.IsCoLoaded = true;
        user.CoLoadedLastUpdated = new Faker().Date.Recent(60);
        var entity = MapToEntity(user);
        customize?.Invoke(entity);
        return entity;
    }

    /// <summary>
    /// Creates a UserEntity with co-loaded status set to false.
    /// </summary>
    /// <param name="customize">Optional action to further customize the entity.</param>
    /// <returns>A UserEntity instance with IsCoLoaded = false.</returns>
    public static UserEntity CreateNonCoLoadedUserEntity(Action<UserEntity>? customize = null)
    {
        var user = UserFaker.Generate();
        user.IsCoLoaded = false;
        user.CoLoadedLastUpdated = null;
        var entity = MapToEntity(user);
        customize?.Invoke(entity);
        return entity;
    }

    private static UserEntity MapToEntity(User user)
    {
        return new UserEntity
        {
            Id = user.Id,
            Email = user.Email.ToLowerInvariant().Trim(),
            IdProofingStatus = (int)user.IdProofingStatus,
            IdProofingSessionId = user.IdProofingSessionId,
            IdProofingCompletedAt = user.IdProofingCompletedAt,
            IdProofingExpiresAt = user.IdProofingExpiresAt,
            IsCoLoaded = user.IsCoLoaded,
            CoLoadedLastUpdated = user.CoLoadedLastUpdated,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };
    }
}
