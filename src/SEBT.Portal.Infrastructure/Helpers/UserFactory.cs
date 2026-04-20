using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Utilities;
using SEBT.Portal.Infrastructure.Data.Entities;
using TestUtilitiesUserFactory = SEBT.Portal.TestUtilities.Helpers.UserFactory;

namespace SEBT.Portal.Infrastructure.Helpers;

/// <summary>
/// Factory for creating UserEntity instances for testing.
/// Uses the TestUtilities project's UserFactory to create domain models, then maps them to entities.
/// </summary>
public static class UserFactory
{
    /// <summary>
    /// Creates a new User instance with generated fake data.
    /// Delegates to the TestUtilities project's UserFactory.
    /// </summary>
    /// <param name="customize">Optional action to customize the generated user.</param>
    /// <returns>A new User instance.</returns>
    public static User CreateUser(Action<User>? customize = null)
    {
        return TestUtilitiesUserFactory.CreateUser(customize);
    }

    /// <summary>
    /// Creates a new User instance with a specific email address.
    /// Delegates to the TestUtilities project's UserFactory.
    /// </summary>
    /// <param name="email">The email address to use (may be empty/null for testing).</param>
    /// <param name="customize">Optional action to further customize the user.</param>
    /// <returns>A new User instance with the specified email.</returns>
    public static User CreateUserWithEmail(string email, Action<User>? customize = null)
    {
        return TestUtilitiesUserFactory.CreateUserWithEmail(email, customize);
    }

    /// <summary>
    /// Creates a new UserEntity instance with realistic fake data.
    /// </summary>
    /// <param name="customize">Optional action to customize the generated entity.</param>
    /// <returns>A new UserEntity instance.</returns>
    public static UserEntity CreateUserEntity(Action<UserEntity>? customize = null)
    {
        var user = TestUtilitiesUserFactory.CreateUser();
        var entity = MapToEntity(user);
        customize?.Invoke(entity);
        return entity;
    }

    /// <summary>
    /// Creates a User with co-loaded status set to true. Delegates to the TestUtilities project's UserFactory.
    /// </summary>
    public static User CreateCoLoadedUser(Action<User>? customize = null) =>
        TestUtilitiesUserFactory.CreateCoLoadedUser(customize);

    /// <summary>
    /// Creates a User with non-co-loaded status (IsCoLoaded = false). Delegates to the TestUtilities project's UserFactory.
    /// </summary>
    public static User CreateNonCoLoadedUser(Action<User>? customize = null) =>
        TestUtilitiesUserFactory.CreateNonCoLoadedUser(customize);

    /// <summary>
    /// Creates a UserEntity with co-loaded status set to true.
    /// </summary>
    /// <param name="customize">Optional action to further customize the entity.</param>
    /// <returns>A UserEntity instance with IsCoLoaded = true.</returns>
    public static UserEntity CreateCoLoadedUserEntity(Action<UserEntity>? customize = null)
    {
        var user = TestUtilitiesUserFactory.CreateCoLoadedUser();
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
        var user = TestUtilitiesUserFactory.CreateNonCoLoadedUser();
        var entity = MapToEntity(user);
        customize?.Invoke(entity);
        return entity;
    }

    private static UserEntity MapToEntity(User user)
    {
        return new UserEntity
        {
            Id = user.Id,
            Email = user.Email != null ? EmailNormalizer.Normalize(user.Email) : null,
            IdProofingStatus = (int)user.IdProofingStatus,
            IalLevel = (int)user.IalLevel,
            IdProofingSessionId = user.IdProofingSessionId,
            IdProofingCompletedAt = user.IdProofingCompletedAt,
            IdProofingExpiresAt = user.IdProofingExpiresAt,
            IsCoLoaded = user.IsCoLoaded,
            CoLoadedLastUpdated = user.CoLoadedLastUpdated,
            IdProofingAttemptCount = user.IdProofingAttemptCount,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };
    }
}
