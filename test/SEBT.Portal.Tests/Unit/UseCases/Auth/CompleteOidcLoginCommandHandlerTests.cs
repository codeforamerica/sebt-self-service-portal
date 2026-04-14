using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.Results;
using SEBT.Portal.UseCases.Auth;

namespace SEBT.Portal.Tests.Unit.UseCases.Auth;

public class CompleteOidcLoginCommandHandlerTests
{
    private readonly ICallbackTokenValidator _callbackTokenValidator;
    private readonly IUserRepository _userRepository;
    private readonly IJwtTokenService _jwtService;
    private readonly IOidcVerificationClaimTranslator _verificationTranslator;
    private readonly CompleteOidcLoginCommandHandler _handler;

    public CompleteOidcLoginCommandHandlerTests()
    {
        // Validator that always passes (DataAnnotations pass for non-null CallbackToken)
        var validator = Substitute.For<IValidator<CompleteOidcLoginCommand>>();
        validator.Validate(Arg.Any<CompleteOidcLoginCommand>(), Arg.Any<CancellationToken>())
            .Returns(ValidationResult.Passed());

        _callbackTokenValidator = Substitute.For<ICallbackTokenValidator>();
        _userRepository = Substitute.For<IUserRepository>();
        _jwtService = Substitute.For<IJwtTokenService>();
        _jwtService.GenerateToken(Arg.Any<User>(), Arg.Any<IReadOnlyDictionary<string, string>?>())
            .Returns("portal-jwt-token");

        var jwtSettings = Options.Create(new JwtSettings
        {
            SecretKey = new string('x', 32),
            Issuer = "test",
            Audience = "test",
            ExpirationMinutes = 60
        });

        _verificationTranslator = Substitute.For<IOidcVerificationClaimTranslator>();

        _handler = new CompleteOidcLoginCommandHandler(
            validator,
            _callbackTokenValidator,
            _userRepository,
            _jwtService,
            jwtSettings,
            NullLogger<CompleteOidcLoginCommandHandler>.Instance,
            _verificationTranslator);
    }

    private static CompleteOidcLoginCommand ValidCommand(bool isStepUp = false, string? returnUrl = null) =>
        new()
        {
            CallbackToken = "valid.callback.token",
            IsStepUp = isStepUp,
            ReturnUrl = returnUrl
        };

    private void SetupValidCallbackToken(string email = "user@example.com",
        Dictionary<string, string>? claims = null)
    {
        _callbackTokenValidator.Validate(Arg.Any<string>())
            .Returns(new CallbackTokenValidationResult(
                email,
                claims ?? new Dictionary<string, string>()));
    }

    #region Callback token validation

    [Fact]
    public async Task Handle_WhenCallbackTokenInvalid_ReturnsValidationFailed()
    {
        _callbackTokenValidator.Validate(Arg.Any<string>()).Returns((CallbackTokenValidationResult?)null);

        var result = await _handler.Handle(ValidCommand());

        Assert.False(result.IsSuccess);
        Assert.IsType<ValidationFailedResult<CompleteOidcLoginResult>>(result);
    }

    #endregion

    #region Normal login

    [Fact]
    public async Task Handle_NormalLogin_CreatesOrGetsUser()
    {
        SetupValidCallbackToken();
        var user = new User { Id = 1, Email = "user@example.com", IalLevel = UserIalLevel.IAL1 };
        _userRepository.GetOrCreateUserAsync("user@example.com", Arg.Any<CancellationToken>())
            .Returns((user, false));

        var result = await _handler.Handle(ValidCommand());

        Assert.True(result.IsSuccess);
        Assert.Equal("portal-jwt-token", result.Value.Token);
        await _userRepository.Received(1).GetOrCreateUserAsync("user@example.com", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NormalLogin_BumpsIalToIal1_WhenBelow()
    {
        SetupValidCallbackToken();
        var user = new User { Id = 1, Email = "user@example.com", IalLevel = UserIalLevel.None };
        _userRepository.GetOrCreateUserAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((user, true));

        await _handler.Handle(ValidCommand());

        Assert.Equal(UserIalLevel.IAL1, user.IalLevel);
        await _userRepository.Received().UpdateUserAsync(
            Arg.Is<User>(u => u.IalLevel == UserIalLevel.IAL1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NormalLogin_DoesNotDowngradeIal()
    {
        SetupValidCallbackToken();
        var user = new User { Id = 1, Email = "user@example.com", IalLevel = UserIalLevel.IAL1plus };
        _userRepository.GetOrCreateUserAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((user, false));
        // No verification claims
        _verificationTranslator.Translate(Arg.Any<IReadOnlyDictionary<string, string>>())
            .Returns((OidcVerificationResult?)null);

        await _handler.Handle(ValidCommand());

        Assert.Equal(UserIalLevel.IAL1plus, user.IalLevel);
    }

    [Fact]
    public async Task Handle_NormalLogin_ReturnsReturnUrlNull()
    {
        SetupValidCallbackToken();
        var user = new User { Id = 1, Email = "user@example.com", IalLevel = UserIalLevel.IAL1 };
        _userRepository.GetOrCreateUserAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((user, false));

        var result = await _handler.Handle(ValidCommand());

        Assert.Null(result.Value.ReturnUrl);
    }

    #endregion

    #region Step-up login

    [Fact]
    public async Task Handle_StepUp_SetsIalToIAL1plus()
    {
        SetupValidCallbackToken();
        var user = new User { Id = 1, Email = "user@example.com", IalLevel = UserIalLevel.IAL1 };
        _userRepository.GetUserByEmailAsync("user@example.com", Arg.Any<CancellationToken>())
            .Returns(user);

        var result = await _handler.Handle(ValidCommand(isStepUp: true));

        Assert.True(result.IsSuccess);
        await _userRepository.Received().UpdateUserAsync(
            Arg.Is<User>(u => u.IalLevel == UserIalLevel.IAL1plus
                           && u.IdProofingStatus == IdProofingStatus.Completed),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_StepUp_WhenNoExistingUser_ReturnsValidationFailed()
    {
        SetupValidCallbackToken("new-user@example.com");
        _userRepository.GetUserByEmailAsync("new-user@example.com", Arg.Any<CancellationToken>())
            .Returns((User?)null);

        var result = await _handler.Handle(ValidCommand(isStepUp: true));

        Assert.False(result.IsSuccess);
        await _userRepository.DidNotReceive().UpdateUserAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_StepUp_WithSafeReturnUrl_ReturnsIt()
    {
        SetupValidCallbackToken();
        var user = new User { Id = 1, Email = "user@example.com", IalLevel = UserIalLevel.IAL1 };
        _userRepository.GetUserByEmailAsync("user@example.com", Arg.Any<CancellationToken>())
            .Returns(user);

        var result = await _handler.Handle(ValidCommand(isStepUp: true, returnUrl: "/profile/address?q=1"));

        Assert.Equal("/profile/address?q=1", result.Value.ReturnUrl);
    }

    [Fact]
    public async Task Handle_StepUp_WithExternalReturnUrl_ReturnsNull()
    {
        SetupValidCallbackToken();
        var user = new User { Id = 1, Email = "user@example.com", IalLevel = UserIalLevel.IAL1 };
        _userRepository.GetUserByEmailAsync("user@example.com", Arg.Any<CancellationToken>())
            .Returns(user);

        var result = await _handler.Handle(ValidCommand(isStepUp: true, returnUrl: "https://evil.example/phish"));

        Assert.Null(result.Value.ReturnUrl);
    }

    #endregion

    #region OIDC verification claim reconciliation

    [Fact]
    public async Task Handle_NormalLogin_WithFreshVerification_UpdatesUserToIAL1plus()
    {
        var claims = new Dictionary<string, string>
        {
            ["socureIdVerificationLevel"] = "1.5",
            ["socureIdVerificationDate"] = DateTime.UtcNow.AddDays(-30).ToString("o")
        };
        SetupValidCallbackToken(claims: claims);

        var user = new User { Id = 1, Email = "user@example.com", IalLevel = UserIalLevel.IAL1 };
        _userRepository.GetOrCreateUserAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((user, false));

        _verificationTranslator.Translate(Arg.Any<IReadOnlyDictionary<string, string>>())
            .Returns(new OidcVerificationResult(UserIalLevel.IAL1plus, DateTime.UtcNow.AddDays(-30), false));

        var result = await _handler.Handle(ValidCommand());

        Assert.True(result.IsSuccess);
        await _userRepository.Received().UpdateUserAsync(
            Arg.Is<User>(u => u.IalLevel == UserIalLevel.IAL1plus
                           && u.IdProofingStatus == IdProofingStatus.Completed),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NormalLogin_WithExpiredVerification_ResetsToIAL1()
    {
        SetupValidCallbackToken();
        var user = new User { Id = 1, Email = "user@example.com", IalLevel = UserIalLevel.IAL1plus };
        _userRepository.GetOrCreateUserAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((user, false));

        _verificationTranslator.Translate(Arg.Any<IReadOnlyDictionary<string, string>>())
            .Returns(new OidcVerificationResult(UserIalLevel.IAL1plus, DateTime.UtcNow.AddYears(-6), true));

        var result = await _handler.Handle(ValidCommand());

        Assert.True(result.IsSuccess);
        await _userRepository.Received().UpdateUserAsync(
            Arg.Is<User>(u => u.IalLevel == UserIalLevel.IAL1
                           && u.IdProofingStatus == IdProofingStatus.Expired),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NormalLogin_WithNoVerificationClaims_DoesNotReconcile()
    {
        SetupValidCallbackToken();
        var user = new User { Id = 1, Email = "user@example.com", IalLevel = UserIalLevel.IAL1 };
        _userRepository.GetOrCreateUserAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((user, false));

        _verificationTranslator.Translate(Arg.Any<IReadOnlyDictionary<string, string>>())
            .Returns((OidcVerificationResult?)null);

        await _handler.Handle(ValidCommand());

        // Only the IAL1 bump, no reconciliation update
        Assert.Equal(UserIalLevel.IAL1, user.IalLevel);
    }

    #endregion

    #region Handler without translator (DC scenario)

    [Fact]
    public async Task Handle_WithoutTranslator_SkipsOidcReconciliation()
    {
        // Create handler without translator (simulates DC deployment)
        var validator = Substitute.For<IValidator<CompleteOidcLoginCommand>>();
        validator.Validate(Arg.Any<CompleteOidcLoginCommand>(), Arg.Any<CancellationToken>())
            .Returns(ValidationResult.Passed());

        var handlerWithoutTranslator = new CompleteOidcLoginCommandHandler(
            validator,
            _callbackTokenValidator,
            _userRepository,
            _jwtService,
            Options.Create(new JwtSettings
            {
                SecretKey = new string('x', 32),
                Issuer = "test",
                Audience = "test",
                ExpirationMinutes = 60
            }),
            NullLogger<CompleteOidcLoginCommandHandler>.Instance);

        SetupValidCallbackToken();
        var user = new User { Id = 1, Email = "user@example.com", IalLevel = UserIalLevel.IAL1 };
        _userRepository.GetOrCreateUserAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((user, false));

        var result = await handlerWithoutTranslator.Handle(ValidCommand());

        Assert.True(result.IsSuccess);
        // No translator means no reconciliation — user stays at IAL1
        Assert.Equal(UserIalLevel.IAL1, user.IalLevel);
    }

    #endregion
}
