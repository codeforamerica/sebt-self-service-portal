using System.Security.Claims;
using Microsoft.Extensions.Options;
using NSubstitute;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Core.Utilities;
using SEBT.Portal.Infrastructure.Services;
using SEBT.Portal.TestUtilities.Helpers;

namespace SEBT.Portal.Tests.Unit.Services;

public class HouseholdIdentifierResolverTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();

    private static HouseholdIdentifierResolver CreateResolver(StateHouseholdIdSettings settings)
    {
        return new HouseholdIdentifierResolver(
            Options.Create(settings),
            Substitute.For<IUserRepository>());
    }

    private static HouseholdIdentifierResolver CreateResolver(IUserRepository userRepository, StateHouseholdIdSettings settings)
    {
        return new HouseholdIdentifierResolver(Options.Create(settings), userRepository);
    }

    private static ClaimsPrincipal CreatePrincipal(string email, string claimType = ClaimTypes.Email)
    {
        var claims = new List<Claim> { new Claim(claimType, email) };
        var identity = new ClaimsIdentity(claims, "Test");
        return new ClaimsPrincipal(identity);
    }

    private static ClaimsPrincipal CreatePrincipalWithNoEmail()
    {
        var identity = new ClaimsIdentity();
        return new ClaimsPrincipal(identity);
    }

    /// <summary>
    /// Creates a principal where email is only available via Identity.Name (no Email or NameIdentifier claims).
    /// </summary>
    private static ClaimsPrincipal CreatePrincipalWithIdentityNameOnly(string email)
    {
        var claims = new List<Claim> { new Claim(ClaimTypes.Name, email) };
        var identity = new ClaimsIdentity(claims, "Test");
        return new ClaimsPrincipal(identity);
    }

    [Fact]
    public async Task ResolveAsync_WhenNoEmailInClaims_ReturnsNull()
    {
        var settings = new StateHouseholdIdSettings
        {
            PreferredHouseholdIdTypes = [PreferredHouseholdIdType.Email]
        };
        var resolver = CreateResolver(_userRepository, settings);

        var principal = CreatePrincipalWithNoEmail();

        var result = await resolver.ResolveAsync(principal);

        Assert.Null(result);
        await _userRepository.DidNotReceive().GetUserByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveAsync_WhenEmailClaimIsWhitespace_ReturnsNull()
    {
        var settings = new StateHouseholdIdSettings
        {
            PreferredHouseholdIdTypes = [PreferredHouseholdIdType.Email]
        };
        var resolver = CreateResolver(_userRepository, settings);

        var principal = CreatePrincipal("   ");

        var result = await resolver.ResolveAsync(principal);

        Assert.Null(result);
    }

    [Fact]
    public async Task ResolveAsync_WhenUserNotFound_ReturnsNull()
    {
        var settings = new StateHouseholdIdSettings
        {
            PreferredHouseholdIdTypes = [PreferredHouseholdIdType.Email]
        };
        _userRepository.GetUserByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((User?)null);
        var resolver = CreateResolver(_userRepository, settings);

        var principal = CreatePrincipal("user@example.com");

        var result = await resolver.ResolveAsync(principal);

        Assert.Null(result);
        await _userRepository.Received(1).GetUserByEmailAsync(EmailNormalizer.Normalize("user@example.com"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveAsync_WhenPrefersEmailAndUserHasEmail_ReturnsEmailIdentifier()
    {
        var email = "user@example.com";
        var user = UserFactory.CreateUserWithEmail(email);
        var settings = new StateHouseholdIdSettings
        {
            PreferredHouseholdIdTypes = [PreferredHouseholdIdType.Email]
        };
        _userRepository.GetUserByEmailAsync(EmailNormalizer.Normalize(email), Arg.Any<CancellationToken>())
            .Returns(user);
        var resolver = CreateResolver(_userRepository, settings);

        var principal = CreatePrincipal(email);

        var result = await resolver.ResolveAsync(principal);

        Assert.NotNull(result);
        Assert.Equal(PreferredHouseholdIdType.Email, result!.Type);
        Assert.Equal(EmailNormalizer.Normalize(email), result.Value);
    }

    [Fact]
    public async Task ResolveAsync_WhenPrefersPhoneAndUserHasPhone_ReturnsPhoneIdentifier()
    {
        var email = "user@example.com";
        var user = UserFactory.CreateUserWithEmail(email, u => u.Phone = "5551234567");
        var settings = new StateHouseholdIdSettings
        {
            PreferredHouseholdIdTypes = [PreferredHouseholdIdType.Phone, PreferredHouseholdIdType.Email]
        };
        _userRepository.GetUserByEmailAsync(EmailNormalizer.Normalize(email), Arg.Any<CancellationToken>())
            .Returns(user);
        var resolver = CreateResolver(_userRepository, settings);

        var principal = CreatePrincipal(email);

        var result = await resolver.ResolveAsync(principal);

        Assert.NotNull(result);
        Assert.Equal(PreferredHouseholdIdType.Phone, result!.Type);
        Assert.Equal("5551234567", result.Value);
    }

    [Fact]
    public async Task ResolveAsync_WhenPrefersSnapIdAndUserHasSnapId_ReturnsSnapIdIdentifier()
    {
        var email = "user@example.com";
        var user = UserFactory.CreateUserWithEmail(email, u => u.SnapId = "SNAP-001");
        var settings = new StateHouseholdIdSettings
        {
            PreferredHouseholdIdTypes = [PreferredHouseholdIdType.SnapId, PreferredHouseholdIdType.Email]
        };
        _userRepository.GetUserByEmailAsync(EmailNormalizer.Normalize(email), Arg.Any<CancellationToken>())
            .Returns(user);
        var resolver = CreateResolver(_userRepository, settings);

        var principal = CreatePrincipal(email);

        var result = await resolver.ResolveAsync(principal);

        Assert.NotNull(result);
        Assert.Equal(PreferredHouseholdIdType.SnapId, result!.Type);
        Assert.Equal("SNAP-001", result.Value);
    }

    [Fact]
    public async Task ResolveAsync_WhenPrefersTanfIdAndUserHasTanfId_ReturnsTanfIdIdentifier()
    {
        var email = "user@example.com";
        var user = UserFactory.CreateUserWithEmail(email, u => u.TanfId = "TANF-001");
        var settings = new StateHouseholdIdSettings
        {
            PreferredHouseholdIdTypes = [PreferredHouseholdIdType.TanfId, PreferredHouseholdIdType.Email]
        };
        _userRepository.GetUserByEmailAsync(EmailNormalizer.Normalize(email), Arg.Any<CancellationToken>())
            .Returns(user);
        var resolver = CreateResolver(_userRepository, settings);

        var principal = CreatePrincipal(email);

        var result = await resolver.ResolveAsync(principal);

        Assert.NotNull(result);
        Assert.Equal(PreferredHouseholdIdType.TanfId, result!.Type);
        Assert.Equal("TANF-001", result.Value);
    }

    [Fact]
    public async Task ResolveAsync_WhenPrefersSsnAndUserHasSsn_ReturnsSsnHashAsIs()
    {
        var hashedSsn = "A1B2C3D4E5F6789012345678901234567890ABCDEF1234567890ABCDEF123456";
        var email = "user@example.com";
        var user = UserFactory.CreateUserWithEmail(email, u => u.Ssn = hashedSsn);
        var settings = new StateHouseholdIdSettings
        {
            PreferredHouseholdIdTypes = [PreferredHouseholdIdType.Ssn, PreferredHouseholdIdType.Email]
        };
        _userRepository.GetUserByEmailAsync(EmailNormalizer.Normalize(email), Arg.Any<CancellationToken>())
            .Returns(user);
        var resolver = CreateResolver(_userRepository, settings);

        var principal = CreatePrincipal(email);

        var result = await resolver.ResolveAsync(principal);

        Assert.NotNull(result);
        Assert.Equal(PreferredHouseholdIdType.Ssn, result!.Type);
        Assert.Equal(hashedSsn, result.Value);
    }

    [Fact]
    public async Task ResolveAsync_WhenFirstPreferredTypeIsEmpty_FallsThroughToNext()
    {
        var email = "user@example.com";
        var user = UserFactory.CreateUserWithEmail(email, u =>
        {
            u.Phone = null;
            u.SnapId = "SNAP-002";
        });
        var settings = new StateHouseholdIdSettings
        {
            PreferredHouseholdIdTypes = [PreferredHouseholdIdType.Phone, PreferredHouseholdIdType.SnapId, PreferredHouseholdIdType.Email]
        };
        _userRepository.GetUserByEmailAsync(EmailNormalizer.Normalize(email), Arg.Any<CancellationToken>())
            .Returns(user);
        var resolver = CreateResolver(_userRepository, settings);

        var principal = CreatePrincipal(email);

        var result = await resolver.ResolveAsync(principal);

        Assert.NotNull(result);
        Assert.Equal(PreferredHouseholdIdType.SnapId, result!.Type);
        Assert.Equal("SNAP-002", result.Value);
    }

    [Fact]
    public async Task ResolveAsync_WhenPreferredHouseholdIdTypesNotSet_DefaultsToEmail()
    {
        var email = "user@example.com";
        var user = UserFactory.CreateUserWithEmail(email);
        var settings = new StateHouseholdIdSettings
        {
            PreferredHouseholdIdTypes = []
        };
        _userRepository.GetUserByEmailAsync(EmailNormalizer.Normalize(email), Arg.Any<CancellationToken>())
            .Returns(user);
        var resolver = CreateResolver(_userRepository, settings);

        var principal = CreatePrincipal(email);

        var result = await resolver.ResolveAsync(principal);

        Assert.NotNull(result);
        Assert.Equal(PreferredHouseholdIdType.Email, result!.Type);
        Assert.Equal(EmailNormalizer.Normalize(email), result.Value);
    }

    [Fact]
    public async Task ResolveAsync_WhenPreferredHouseholdIdTypesIsEmpty_DefaultsToEmail()
    {
        var email = "user@example.com";
        var user = UserFactory.CreateUserWithEmail(email);
        var settings = new StateHouseholdIdSettings
        {
            PreferredHouseholdIdTypes = []
        };
        _userRepository.GetUserByEmailAsync(EmailNormalizer.Normalize(email), Arg.Any<CancellationToken>())
            .Returns(user);
        var resolver = CreateResolver(_userRepository, settings);

        var principal = CreatePrincipal(email);

        var result = await resolver.ResolveAsync(principal);

        Assert.NotNull(result);
        Assert.Equal(PreferredHouseholdIdType.Email, result!.Type);
    }

    [Fact]
    public async Task ResolveAsync_WhenNoPreferredTypeHasValue_ReturnsNull()
    {
        var email = "user@example.com";
        var user = UserFactory.CreateUserWithEmail(email, u =>
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
        _userRepository.GetUserByEmailAsync(EmailNormalizer.Normalize(email), Arg.Any<CancellationToken>())
            .Returns(user);
        var resolver = CreateResolver(_userRepository, settings);

        var principal = CreatePrincipal(email);

        var result = await resolver.ResolveAsync(principal);

        Assert.Null(result);
    }

    [Fact]
    public async Task ResolveAsync_WhenEmailFromNameIdentifierClaim_ResolvesCorrectly()
    {
        var email = "user@example.com";
        var user = UserFactory.CreateUserWithEmail(email);
        var settings = new StateHouseholdIdSettings
        {
            PreferredHouseholdIdTypes = [PreferredHouseholdIdType.Email]
        };
        _userRepository.GetUserByEmailAsync(EmailNormalizer.Normalize(email), Arg.Any<CancellationToken>())
            .Returns(user);
        var resolver = CreateResolver(_userRepository, settings);

        var principal = CreatePrincipal(email, ClaimTypes.NameIdentifier);

        var result = await resolver.ResolveAsync(principal);

        Assert.NotNull(result);
        Assert.Equal(PreferredHouseholdIdType.Email, result!.Type);
        Assert.Equal(EmailNormalizer.Normalize(email), result.Value);
    }

    [Fact]
    public async Task ResolveAsync_WhenEmailFromIdentityName_ResolvesCorrectly()
    {
        var email = "user@example.com";
        var user = UserFactory.CreateUserWithEmail(email);
        var settings = new StateHouseholdIdSettings
        {
            PreferredHouseholdIdTypes = [PreferredHouseholdIdType.Email]
        };
        _userRepository.GetUserByEmailAsync(EmailNormalizer.Normalize(email), Arg.Any<CancellationToken>())
            .Returns(user);
        var resolver = CreateResolver(_userRepository, settings);

        var principal = CreatePrincipalWithIdentityNameOnly(email);

        var result = await resolver.ResolveAsync(principal);

        Assert.NotNull(result);
        Assert.Equal(PreferredHouseholdIdType.Email, result!.Type);
        Assert.Equal(EmailNormalizer.Normalize(email), result.Value);
    }

    [Fact]
    public async Task ResolveAsync_NormalizesEmailToLowercase()
    {
        var email = "User@Example.COM";
        var user = UserFactory.CreateUserWithEmail(EmailNormalizer.Normalize(email));
        var settings = new StateHouseholdIdSettings
        {
            PreferredHouseholdIdTypes = [PreferredHouseholdIdType.Email]
        };
        _userRepository.GetUserByEmailAsync(EmailNormalizer.Normalize(email), Arg.Any<CancellationToken>())
            .Returns(user);
        var resolver = CreateResolver(_userRepository, settings);

        var principal = CreatePrincipal(email);

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
        var principal = CreatePrincipal("user@example.com");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => resolver.ResolveAsync(principal, cts.Token));
    }
}
