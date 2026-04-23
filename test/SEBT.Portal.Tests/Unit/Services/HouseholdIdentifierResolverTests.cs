using System.Security.Claims;
using Microsoft.Extensions.Options;
using NSubstitute;
using SEBT.Portal.Api.Services;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Core.Utilities;
using SEBT.Portal.Infrastructure.Services;
using SEBT.Portal.TestUtilities.Helpers;

namespace SEBT.Portal.Tests.Unit.Services;

public class HouseholdIdentifierResolverTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private static readonly IPhoneOverrideProvider NoOverride = NullPhoneOverrideProvider.Instance;

    private static IOptionsSnapshot<StateHouseholdIdSettings> SnapshotFor(StateHouseholdIdSettings settings)
    {
        var snapshot = Substitute.For<IOptionsSnapshot<StateHouseholdIdSettings>>();
        snapshot.Value.Returns(settings);
        return snapshot;
    }

    private static HouseholdIdentifierResolver CreateResolver(StateHouseholdIdSettings settings)
    {
        return new HouseholdIdentifierResolver(
            SnapshotFor(settings),
            Substitute.For<IUserRepository>(),
            NoOverride);
    }

    private static HouseholdIdentifierResolver CreateResolver(IUserRepository userRepository, StateHouseholdIdSettings settings)
    {
        return new HouseholdIdentifierResolver(SnapshotFor(settings), userRepository, NoOverride);
    }

    private static HouseholdIdentifierResolver CreateResolver(
        IUserRepository userRepository,
        StateHouseholdIdSettings settings,
        IPhoneOverrideProvider phoneOverride)
    {
        return new HouseholdIdentifierResolver(SnapshotFor(settings), userRepository, phoneOverride);
    }

    /// <summary>
    /// Creates a principal with a sub claim matching the user's Id, plus any additional claims.
    /// </summary>
    private static ClaimsPrincipal CreatePrincipalForUser(User user, params Claim[] extraClaims)
    {
        var claims = new List<Claim> { new("sub", user.Id.ToString()) };
        claims.AddRange(extraClaims);
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }

    /// <summary>
    /// Creates a user with a positive Id using an object initializer, seeding any additional
    /// properties via the customize action. GetUserId() requires Id > 0 to be non-null.
    /// </summary>
    private static User CreateUser(int id, string email, Action<User>? customize = null)
    {
        var user = new User { Id = id, Email = EmailNormalizer.Normalize(email) };
        customize?.Invoke(user);
        return user;
    }

    [Fact]
    public async Task ResolveAsync_WhenNoSubClaim_ReturnsNull()
    {
        var settings = new StateHouseholdIdSettings
        {
            PreferredHouseholdIdTypes = [PreferredHouseholdIdType.Email]
        };
        var resolver = CreateResolver(_userRepository, settings);

        var principal = new ClaimsPrincipal(new ClaimsIdentity());

        var result = await resolver.ResolveAsync(principal);

        Assert.Null(result);
        await _userRepository.DidNotReceive().GetUserByIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveAsync_WhenUserNotFound_ReturnsNull()
    {
        var user = CreateUser(1, "user@example.com");
        var settings = new StateHouseholdIdSettings
        {
            PreferredHouseholdIdTypes = [PreferredHouseholdIdType.Email]
        };
        _userRepository.GetUserByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns((User?)null);
        var resolver = CreateResolver(_userRepository, settings);

        var principal = CreatePrincipalForUser(user);

        var result = await resolver.ResolveAsync(principal);

        Assert.Null(result);
        await _userRepository.Received(1).GetUserByIdAsync(user.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveAsync_WhenPrefersEmailAndUserHasEmail_ReturnsEmailIdentifier()
    {
        var email = "user@example.com";
        var user = CreateUser(2, email);
        var settings = new StateHouseholdIdSettings
        {
            PreferredHouseholdIdTypes = [PreferredHouseholdIdType.Email]
        };
        _userRepository.GetUserByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);
        var resolver = CreateResolver(_userRepository, settings);

        var principal = CreatePrincipalForUser(user);

        var result = await resolver.ResolveAsync(principal);

        Assert.NotNull(result);
        Assert.Equal(PreferredHouseholdIdType.Email, result!.Type);
        Assert.Equal(EmailNormalizer.Normalize(email), result.Value);
    }

    [Fact]
    public async Task ResolveAsync_WhenPrefersPhoneAndUserHasPhone_ReturnsPhoneIdentifier()
    {
        var user = CreateUser(3, "user@example.com", u => u.Phone = "5551234567");
        var settings = new StateHouseholdIdSettings
        {
            PreferredHouseholdIdTypes = [PreferredHouseholdIdType.Phone, PreferredHouseholdIdType.Email]
        };
        _userRepository.GetUserByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);
        var resolver = CreateResolver(_userRepository, settings);

        var principal = CreatePrincipalForUser(user);

        var result = await resolver.ResolveAsync(principal);

        Assert.NotNull(result);
        Assert.Equal(PreferredHouseholdIdType.Phone, result!.Type);
        Assert.Equal("5551234567", result.Value);
    }

    /// <summary>
    /// Ensures settings are respected when only Phone is configured (e.g. CO).
    /// Uses Phone from user, does not fall back to Email even though user has one.
    /// </summary>
    [Fact]
    public async Task ResolveAsync_WhenSettingsHavePhoneOnly_ReturnsPhoneNotEmail()
    {
        var user = CreateUser(4, "user@example.com", u => u.Phone = "8185558437");
        var settings = new StateHouseholdIdSettings
        {
            PreferredHouseholdIdTypes = [PreferredHouseholdIdType.Phone]
        };
        _userRepository.GetUserByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);
        var resolver = CreateResolver(_userRepository, settings);

        var principal = CreatePrincipalForUser(user);

        var result = await resolver.ResolveAsync(principal);

        Assert.NotNull(result);
        Assert.Equal(PreferredHouseholdIdType.Phone, result!.Type);
        Assert.Equal("8185558437", result.Value);
    }

    /// <summary>
    /// When settings have Phone only and user has no phone, returns null.
    /// Does not fall back to Email (Colorado does not support email lookup).
    /// </summary>
    [Fact]
    public async Task ResolveAsync_WhenSettingsHavePhoneOnlyAndUserHasNoPhone_ReturnsNull()
    {
        var user = CreateUser(5, "user@example.com", u => u.Phone = null);
        var settings = new StateHouseholdIdSettings
        {
            PreferredHouseholdIdTypes = [PreferredHouseholdIdType.Phone]
        };
        _userRepository.GetUserByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);
        var resolver = CreateResolver(_userRepository, settings);

        var principal = CreatePrincipalForUser(user);

        var result = await resolver.ResolveAsync(principal);

        Assert.Null(result);
    }

    [Fact]
    public async Task ResolveAsync_WhenPhoneOverrideProviderReturnsValue_UsesOverrideOverJwtAndUser()
    {
        var user = CreateUser(6, "user@example.com", u => u.Phone = "5551234567");
        var settings = new StateHouseholdIdSettings
        {
            PreferredHouseholdIdTypes = [PreferredHouseholdIdType.Phone, PreferredHouseholdIdType.Email]
        };
        _userRepository.GetUserByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);
        var overrideProvider = Substitute.For<IPhoneOverrideProvider>();
        overrideProvider.GetOverridePhone().Returns("8185558437");
        var resolver = CreateResolver(_userRepository, settings, overrideProvider);

        var principal = CreatePrincipalForUser(user);

        var result = await resolver.ResolveAsync(principal);

        Assert.NotNull(result);
        Assert.Equal(PreferredHouseholdIdType.Phone, result!.Type);
        Assert.Equal("8185558437", result.Value);
    }

    /// <summary>
    /// When the override provider returns null (setting not set), the resolver uses user or claims instead of the bypass.
    /// </summary>
    [Fact]
    public async Task ResolveAsync_WhenPhoneOverrideProviderReturnsNull_UsesUserOrClaimsInstead()
    {
        var user = CreateUser(7, "user@example.com", u => u.Phone = "5551234567");
        var settings = new StateHouseholdIdSettings
        {
            PreferredHouseholdIdTypes = [PreferredHouseholdIdType.Phone, PreferredHouseholdIdType.Email]
        };
        _userRepository.GetUserByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);
        var overrideProvider = Substitute.For<IPhoneOverrideProvider>();
        overrideProvider.GetOverridePhone().Returns((string?)null);
        var resolver = CreateResolver(_userRepository, settings, overrideProvider);

        var principal = CreatePrincipalForUser(user);

        var result = await resolver.ResolveAsync(principal);

        Assert.NotNull(result);
        Assert.Equal(PreferredHouseholdIdType.Phone, result!.Type);
        Assert.Equal("5551234567", result.Value);
    }

    [Fact]
    public async Task ResolveAsync_WhenPrefersPhoneAndUserHasNoPhoneButClaimsHavePhone_ReturnsPhoneFromClaims()
    {
        var user = CreateUser(8, "user@example.com", u => u.Phone = null);
        var settings = new StateHouseholdIdSettings
        {
            PreferredHouseholdIdTypes = [PreferredHouseholdIdType.Phone, PreferredHouseholdIdType.Email]
        };
        _userRepository.GetUserByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);
        var resolver = CreateResolver(_userRepository, settings);

        var principal = CreatePrincipalForUser(user, new Claim("phone", "5559876543"));

        var result = await resolver.ResolveAsync(principal);

        Assert.NotNull(result);
        Assert.Equal(PreferredHouseholdIdType.Phone, result!.Type);
        Assert.Equal("5559876543", result.Value);
    }

    [Fact]
    public async Task ResolveAsync_WhenPrefersSnapIdAndUserHasSnapId_ReturnsSnapIdIdentifier()
    {
        var user = CreateUser(9, "user@example.com", u => u.SnapId = "SNAP-001");
        var settings = new StateHouseholdIdSettings
        {
            PreferredHouseholdIdTypes = [PreferredHouseholdIdType.SnapId, PreferredHouseholdIdType.Email]
        };
        _userRepository.GetUserByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);
        var resolver = CreateResolver(_userRepository, settings);

        var principal = CreatePrincipalForUser(user);

        var result = await resolver.ResolveAsync(principal);

        Assert.NotNull(result);
        Assert.Equal(PreferredHouseholdIdType.SnapId, result!.Type);
        Assert.Equal("SNAP-001", result.Value);
    }

    [Fact]
    public async Task ResolveAsync_WhenPrefersTanfIdAndUserHasTanfId_ReturnsTanfIdIdentifier()
    {
        var user = CreateUser(10, "user@example.com", u => u.TanfId = "TANF-001");
        var settings = new StateHouseholdIdSettings
        {
            PreferredHouseholdIdTypes = [PreferredHouseholdIdType.TanfId, PreferredHouseholdIdType.Email]
        };
        _userRepository.GetUserByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);
        var resolver = CreateResolver(_userRepository, settings);

        var principal = CreatePrincipalForUser(user);

        var result = await resolver.ResolveAsync(principal);

        Assert.NotNull(result);
        Assert.Equal(PreferredHouseholdIdType.TanfId, result!.Type);
        Assert.Equal("TANF-001", result.Value);
    }

    [Fact]
    public async Task ResolveAsync_WhenPrefersSsnAndUserHasSsn_ReturnsSsnHashAsIs()
    {
        var hashedSsn = "A1B2C3D4E5F6789012345678901234567890ABCDEF1234567890ABCDEF123456";
        var user = CreateUser(11, "user@example.com", u => u.Ssn = hashedSsn);
        var settings = new StateHouseholdIdSettings
        {
            PreferredHouseholdIdTypes = [PreferredHouseholdIdType.Ssn, PreferredHouseholdIdType.Email]
        };
        _userRepository.GetUserByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);
        var resolver = CreateResolver(_userRepository, settings);

        var principal = CreatePrincipalForUser(user);

        var result = await resolver.ResolveAsync(principal);

        Assert.NotNull(result);
        Assert.Equal(PreferredHouseholdIdType.Ssn, result!.Type);
        Assert.Equal(hashedSsn, result.Value);
    }

    [Fact]
    public async Task ResolveAsync_WhenFirstPreferredTypeIsEmpty_FallsThroughToNext()
    {
        var user = CreateUser(12, "user@example.com", u =>
        {
            u.Phone = null;
            u.SnapId = "SNAP-002";
        });
        var settings = new StateHouseholdIdSettings
        {
            PreferredHouseholdIdTypes = [PreferredHouseholdIdType.Phone, PreferredHouseholdIdType.SnapId, PreferredHouseholdIdType.Email]
        };
        _userRepository.GetUserByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);
        var resolver = CreateResolver(_userRepository, settings);

        var principal = CreatePrincipalForUser(user);

        var result = await resolver.ResolveAsync(principal);

        Assert.NotNull(result);
        Assert.Equal(PreferredHouseholdIdType.SnapId, result!.Type);
        Assert.Equal("SNAP-002", result.Value);
    }

    [Fact]
    public async Task ResolveAsync_WhenPreferredHouseholdIdTypesIsNull_ThrowsInvalidOperationException()
    {
        var user = CreateUser(13, "user@example.com");
        var settings = new StateHouseholdIdSettings
        {
            PreferredHouseholdIdTypes = null!
        };
        _userRepository.GetUserByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);
        var resolver = CreateResolver(_userRepository, settings);

        var principal = CreatePrincipalForUser(user);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => resolver.ResolveAsync(principal));

        Assert.Contains("PreferredHouseholdIdTypes", ex.Message);
        Assert.Contains("null", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ResolveAsync_WhenPreferredHouseholdIdTypesIsEmpty_ThrowsInvalidOperationException()
    {
        var user = CreateUser(14, "user@example.com");
        var settings = new StateHouseholdIdSettings
        {
            PreferredHouseholdIdTypes = []
        };
        _userRepository.GetUserByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);
        var resolver = CreateResolver(_userRepository, settings);

        var principal = CreatePrincipalForUser(user);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => resolver.ResolveAsync(principal));

        Assert.Contains("PreferredHouseholdIdTypes", ex.Message);
        Assert.Contains("empty", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ResolveAsync_WhenNoPreferredTypeHasValue_ReturnsNull()
    {
        var user = CreateUser(15, "user@example.com", u =>
        {
            u.Phone = null;
            u.SnapId = null;
            u.TanfId = null;
            u.Ssn = null;
        });
        var settings = new StateHouseholdIdSettings
        {
            PreferredHouseholdIdTypes = [PreferredHouseholdIdType.Phone, PreferredHouseholdIdType.SnapId]
        };
        _userRepository.GetUserByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);
        var resolver = CreateResolver(_userRepository, settings);

        var principal = CreatePrincipalForUser(user);

        var result = await resolver.ResolveAsync(principal);

        Assert.Null(result);
    }

    [Fact]
    public async Task ResolveAsync_NormalizesEmailToLowercase()
    {
        var email = "User@Example.COM";
        var user = CreateUser(16, email);
        var settings = new StateHouseholdIdSettings
        {
            PreferredHouseholdIdTypes = [PreferredHouseholdIdType.Email]
        };
        _userRepository.GetUserByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);
        var resolver = CreateResolver(_userRepository, settings);

        var principal = CreatePrincipalForUser(user);

        var result = await resolver.ResolveAsync(principal);

        Assert.NotNull(result);
        Assert.Equal("user@example.com", result!.Value);
    }

    [Fact]
    public async Task ResolveAsync_WhenCancellationRequested_Throws()
    {
        var settings = new StateHouseholdIdSettings
        {
            PreferredHouseholdIdTypes = [PreferredHouseholdIdType.Email]
        };
        var resolver = CreateResolver(settings);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", "99") }, "Test"));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => resolver.ResolveAsync(principal, cts.Token));
    }
}
