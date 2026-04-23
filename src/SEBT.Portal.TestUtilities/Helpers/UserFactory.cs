using Bogus;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Utilities;

namespace SEBT.Portal.TestUtilities.Helpers;

/// <summary>
/// Factory for creating User instances using Bogus for generating fake data.
/// Used for testing. For UserEntity and database helpers, use Infrastructure.Helpers.UserFactory.
/// See https://github.com/bchavez/Bogus for more information
/// </summary>
public static class UserFactory
{
    private static readonly Faker<User> UserFaker = new Faker<User>()
        // Id defaults to Guid.CreateVersion7() at User construction; no rule needed.
        .RuleFor(u => u.Email, f => f.Internet.Email().ToLowerInvariant())
        .RuleFor(u => u.IdProofingStatus, f => f.PickRandom<IdProofingStatus>())
        .RuleFor(u => u.IalLevel, f => f.PickRandom<UserIalLevel>())
        .RuleFor(u => u.IdProofingSessionId, f => f.Random.Guid().ToString())
        .RuleFor(u => u.IdProofingCompletedAt, (f, u) =>
            u.IalLevel is UserIalLevel.IAL1plus or UserIalLevel.IAL2
                ? f.Date.Recent(30)
                : null)
        .RuleFor(u => u.IdProofingExpiresAt, (f, u) =>
            u.IdProofingCompletedAt?.AddYears(1))
        .RuleFor(u => u.DateOfBirth, f =>
            DateOnly.FromDateTime(f.Date.Between(DateTime.UtcNow.AddYears(-65), DateTime.UtcNow.AddYears(-21))))
        .RuleFor(u => u.IsCoLoaded, f => f.Random.Bool(0.3f))
        .RuleFor(u => u.CoLoadedLastUpdated, (f, u) =>
            u.IsCoLoaded ? f.Date.Recent(60) : null)
        .RuleFor(u => u.CreatedAt, f => f.Date.Past(1))
        .RuleFor(u => u.UpdatedAt, (f, u) => f.Date.Between(u.CreatedAt, DateTime.UtcNow));

    /// <summary>
    /// Creates a new User instance with generated fake data.
    /// </summary>
    /// <param name="customize">Optional action to customize the generated user.</param>
    /// <returns>A new User instance.</returns>
    public static User CreateUser(Action<User>? customize = null)
    {
        var user = UserFaker.Generate();
        customize?.Invoke(user);
        return user;
    }

    /// <summary>
    /// Creates a new User instance with a specific email address.
    /// Note: For testing purposes, this allows empty/null emails to test repository validation.
    /// In production code, emails should be validated before calling this method.
    /// </summary>
    /// <param name="email">The email address to use.</param>
    /// <param name="customize">Optional action to further customize the user.</param>
    /// <returns>A new User instance with the specified email.</returns>
    public static User CreateUserWithEmail(string email, Action<User>? customize = null)
    {
        var user = UserFaker.Generate();
        user.Email = string.IsNullOrWhiteSpace(email) ? email : EmailNormalizer.Normalize(email);
        customize?.Invoke(user);
        return user;
    }

    /// <summary>
    /// Creates a User with co-loaded status set to true.
    /// </summary>
    /// <param name="customize">Optional action to further customize the user.</param>
    /// <returns>A User instance with IsCoLoaded = true.</returns>
    public static User CreateCoLoadedUser(Action<User>? customize = null)
    {
        return CreateUser(u =>
        {
            u.IsCoLoaded = true;
            var faker = new Faker();
            u.CoLoadedLastUpdated = faker.Date.Recent(60);
            customize?.Invoke(u);
        });
    }

    /// <summary>
    /// Creates a User with co-loaded status set to false.
    /// </summary>
    /// <param name="customize">Optional action to further customize the user.</param>
    /// <returns>A User instance with IsCoLoaded = false.</returns>
    public static User CreateNonCoLoadedUser(Action<User>? customize = null)
    {
        return CreateUser(u =>
        {
            u.IsCoLoaded = false;
            u.CoLoadedLastUpdated = null;
            customize?.Invoke(u);
        });
    }

    /// <summary>
    /// Creates a User with a specific IAL level.
    /// </summary>
    /// <param name="ialLevel">The IAL level to set.</param>
    /// <param name="customize">Optional action to further customize the user.</param>
    /// <returns>A User instance with the specified IAL level.</returns>
    public static User CreateUserWithStatus(UserIalLevel ialLevel, Action<User>? customize = null)
    {
        return CreateUser(u =>
        {
            u.IdProofingStatus = ialLevel is UserIalLevel.IAL1 or UserIalLevel.IAL1plus or UserIalLevel.IAL2
                ? IdProofingStatus.Completed
                : IdProofingStatus.NotStarted;
            u.IalLevel = ialLevel;

            // IAL1+ / IAL2 store a proofing completion timestamp (matches DatabaseSeeder mock seeding).
            if (ialLevel is UserIalLevel.IAL1plus or UserIalLevel.IAL2)
            {
                var faker = new Faker();
                u.IdProofingCompletedAt = faker.Date.Recent(30);
            }
            else
            {
                u.IdProofingCompletedAt = null;
            }

            customize?.Invoke(u);
        });
    }

    /// <summary>
    /// Sets a seed for the random number generator to ensure deterministic test data.
    /// </summary>
    /// <param name="seed">The seed value to use.</param>
    public static void SetSeed(int seed)
    {
        Randomizer.Seed = new Random(seed);
    }
}
