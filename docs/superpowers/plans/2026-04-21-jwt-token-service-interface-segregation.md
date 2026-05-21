# JWT Token Service Interface Segregation

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the single `IJwtTokenService.GenerateToken()` with three focused interfaces — one per caller — so each entry point owns its claim resolution, the token builder is mechanical, and the step-up loop bug class becomes structurally impossible.

**Architecture:** `JwtTokenService` implements `ILocalLoginTokenService`, `IOidcTokenService`, and `ISessionRefreshTokenService`. Each public method resolves claims from its specific source (user entity, IdP principal, existing JWT), then delegates to a shared private `BuildAndSignToken` helper that does mechanical JWT construction and invariant checking. `OidcVerificationClaimTranslator` moves from being a controller dependency to a service dependency.

**Tech Stack:** C# / .NET 10, xUnit, NSubstitute, ASP.NET Core DI

**Pre-existing condition:** The `fix/step-up-loop` branch already contains:
- A bug-fix test (`GenerateToken_WhenAdditionalClaimsHaveCompletedAtButNoExpiresAt_ComputesExpiresAt`)
- Two invariant guard tests
- The `ExpiresAt` computation fix and invariant guard in `JwtTokenService.GenerateToken`

These were written as the minimal fix. This plan replaces the patched `GenerateToken` with a cleaner decomposition that makes the bug class structurally impossible.

---

### Task 1: Define three token service interfaces in Core

**Files:**
- Create: `src/SEBT.Portal.Core/Services/ILocalLoginTokenService.cs`
- Create: `src/SEBT.Portal.Core/Services/IOidcTokenService.cs`
- Create: `src/SEBT.Portal.Core/Services/ISessionRefreshTokenService.cs`

- [ ] **Step 1: Create ILocalLoginTokenService**

```csharp
// src/SEBT.Portal.Core/Services/ILocalLoginTokenService.cs
using SEBT.Portal.Core.Models.Auth;

namespace SEBT.Portal.Core.Services;

/// <summary>
/// Generates a portal JWT for OTP-authenticated (local login) users.
/// All claims are sourced from the <see cref="User"/> entity.
/// </summary>
public interface ILocalLoginTokenService
{
    string GenerateForLocalLogin(User user);
}
```

- [ ] **Step 2: Create IOidcTokenService**

```csharp
// src/SEBT.Portal.Core/Services/IOidcTokenService.cs
using System.Security.Claims;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Kernel;

namespace SEBT.Portal.Core.Services;

/// <summary>
/// Generates a portal JWT for OIDC-authenticated users. Accepts the validated
/// IdP callback token as a <see cref="ClaimsPrincipal"/>, handles verification
/// claim translation, IAL derivation, and ID proofing timestamp computation.
/// Returns <see cref="Result{T}"/> because step-up flows fail when the IdP
/// returns no verification claims.
/// </summary>
public interface IOidcTokenService
{
    Result<string> GenerateForOidcLogin(User user, ClaimsPrincipal idpPrincipal, bool isStepUp);
}
```

- [ ] **Step 3: Create ISessionRefreshTokenService**

```csharp
// src/SEBT.Portal.Core/Services/ISessionRefreshTokenService.cs
using System.Security.Claims;
using SEBT.Portal.Core.Models.Auth;

namespace SEBT.Portal.Core.Services;

/// <summary>
/// Generates a refreshed portal JWT by copying claims from the current session's
/// <see cref="ClaimsPrincipal"/>. The user entity is used only for the internal
/// user ID (JWT sub claim).
/// </summary>
public interface ISessionRefreshTokenService
{
    string GenerateForSessionRefresh(User user, ClaimsPrincipal currentPrincipal);
}
```

- [ ] **Step 4: Verify solution builds**

Run: `dotnet build src/SEBT.Portal.Core/SEBT.Portal.Core.csproj`
Expected: Build succeeded.

- [ ] **Step 5: Commit**

```
feat: define ILocalLoginTokenService, IOidcTokenService, ISessionRefreshTokenService interfaces
```

---

### Task 2: Write tests for GenerateForOidcLogin (red)

This is the most complex path — it absorbs verification claim translation, infrastructure claim filtering, and the step-up error case. Write all tests first, expect compilation failures until Task 4.

**Files:**
- Create: `test/SEBT.Portal.Tests/Unit/Services/OidcTokenServiceTests.cs`

- [ ] **Step 1: Create OidcTokenServiceTests with test setup**

The test class uses a real `OidcVerificationClaimTranslator` (not mocked) so we test the full pipeline. The claim names use the defaults from `OidcVerificationClaimSettings`.

```csharp
// test/SEBT.Portal.Tests/Unit/Services/OidcTokenServiceTests.cs
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Infrastructure.Services;
using SEBT.Portal.Kernel.Results;

namespace SEBT.Portal.Tests.Unit.Services;

public class OidcTokenServiceTests
{
    private readonly IOidcTokenService _service;

    public OidcTokenServiceTests()
    {
        var jwtOptions = Substitute.For<IOptions<JwtSettings>>();
        jwtOptions.Value.Returns(new JwtSettings
        {
            SecretKey = "TestSecretKeyMustBeAtLeast32CharactersLongForSecurity",
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            ExpirationMinutes = 60
        });

        var validityOptions = Substitute.For<IOptions<IdProofingValiditySettings>>();
        validityOptions.Value.Returns(new IdProofingValiditySettings { ValidityDays = 1826 });

        var translator = new OidcVerificationClaimTranslator(
            new OidcVerificationClaimSettings(),
            new IdProofingValiditySettings { ValidityDays = 1826 },
            NullLogger<OidcVerificationClaimTranslator>.Instance);

        _service = new JwtTokenService(jwtOptions, validityOptions, translator);
    }

    private static ClaimsPrincipal MakePrincipal(params (string type, string value)[] claims)
    {
        var identity = new ClaimsIdentity(
            claims.Select(c => new Claim(c.type, c.value)), "test");
        return new ClaimsPrincipal(identity);
    }

    private static JwtSecurityToken ReadJwt(string token) =>
        new JwtSecurityTokenHandler().ReadJwtToken(token);

    // --- Normal login (non-step-up) ---

    [Fact]
    public void NormalLogin_WithVerificationClaims_SetsIal1Plus()
    {
        var user = new User { Id = 1 };
        var principal = MakePrincipal(
            ("sub", "idp-sub-123"),
            ("email", "user@example.com"),
            ("socureIdVerificationLevel", "1.5"),
            ("socureIdVerificationDate", DateTime.UtcNow.ToString("o")));

        var result = _service.GenerateForOidcLogin(user, principal, isStepUp: false);

        Assert.True(result.IsSuccess);
        var jwt = ReadJwt(result.Value);
        Assert.Equal("1plus", jwt.Claims.First(c => c.Type == JwtClaimTypes.Ial).Value);
        Assert.Equal(
            ((int)IdProofingStatus.Completed).ToString(),
            jwt.Claims.First(c => c.Type == JwtClaimTypes.IdProofingStatus).Value);
    }

    [Fact]
    public void NormalLogin_WithVerificationClaims_SetsCompletedAtAndExpiresAt()
    {
        var verifiedAt = DateTime.UtcNow.AddDays(-5);
        var user = new User { Id = 1 };
        var principal = MakePrincipal(
            ("sub", "idp-sub-123"),
            ("email", "user@example.com"),
            ("socureIdVerificationLevel", "1.5"),
            ("socureIdVerificationDate", verifiedAt.ToString("o")));

        var result = _service.GenerateForOidcLogin(user, principal, isStepUp: false);

        Assert.True(result.IsSuccess);
        var jwt = ReadJwt(result.Value);

        var completedAtClaim = jwt.Claims.First(c => c.Type == JwtClaimTypes.IdProofingCompletedAt);
        Assert.True(long.TryParse(completedAtClaim.Value, out var completedAtUnix));
        var completedAt = DateTimeOffset.FromUnixTimeSeconds(completedAtUnix).UtcDateTime;
        Assert.True(Math.Abs((completedAt - verifiedAt).TotalSeconds) < 1);

        var expiresAtClaim = jwt.Claims.First(c => c.Type == JwtClaimTypes.IdProofingExpiresAt);
        Assert.True(long.TryParse(expiresAtClaim.Value, out var expiresAtUnix));
        var expiresAt = DateTimeOffset.FromUnixTimeSeconds(expiresAtUnix).UtcDateTime;
        var expectedExpiresAt = verifiedAt.AddDays(1826);
        Assert.True(Math.Abs((expiresAt - expectedExpiresAt).TotalSeconds) < 1);
    }

    [Fact]
    public void NormalLogin_WithoutVerificationClaims_SetsIal1()
    {
        var user = new User { Id = 1 };
        var principal = MakePrincipal(
            ("sub", "idp-sub-123"),
            ("email", "user@example.com"));

        var result = _service.GenerateForOidcLogin(user, principal, isStepUp: false);

        Assert.True(result.IsSuccess);
        var jwt = ReadJwt(result.Value);
        Assert.Equal("1", jwt.Claims.First(c => c.Type == JwtClaimTypes.Ial).Value);
        Assert.Equal(
            ((int)IdProofingStatus.NotStarted).ToString(),
            jwt.Claims.First(c => c.Type == JwtClaimTypes.IdProofingStatus).Value);
    }

    [Fact]
    public void NormalLogin_WithoutVerificationClaims_HasNoCompletedAtOrExpiresAt()
    {
        var user = new User { Id = 1 };
        var principal = MakePrincipal(
            ("sub", "idp-sub-123"),
            ("email", "user@example.com"));

        var result = _service.GenerateForOidcLogin(user, principal, isStepUp: false);

        var jwt = ReadJwt(result.Value);
        Assert.Null(jwt.Claims.FirstOrDefault(c => c.Type == JwtClaimTypes.IdProofingCompletedAt));
        Assert.Null(jwt.Claims.FirstOrDefault(c => c.Type == JwtClaimTypes.IdProofingExpiresAt));
    }

    [Fact]
    public void NormalLogin_WithExpiredVerification_SetsIal1()
    {
        var user = new User { Id = 1 };
        var principal = MakePrincipal(
            ("sub", "idp-sub-123"),
            ("email", "user@example.com"),
            ("socureIdVerificationLevel", "1.5"),
            ("socureIdVerificationDate", DateTime.UtcNow.AddDays(-2000).ToString("o")));

        var result = _service.GenerateForOidcLogin(user, principal, isStepUp: false);

        Assert.True(result.IsSuccess);
        var jwt = ReadJwt(result.Value);
        Assert.Equal("1", jwt.Claims.First(c => c.Type == JwtClaimTypes.Ial).Value);
        Assert.Equal(
            ((int)IdProofingStatus.Expired).ToString(),
            jwt.Claims.First(c => c.Type == JwtClaimTypes.IdProofingStatus).Value);
    }

    // --- Step-up ---

    [Fact]
    public void StepUp_WithVerificationClaims_ReturnsSuccessWithIal1Plus()
    {
        var user = new User { Id = 1 };
        var principal = MakePrincipal(
            ("sub", "idp-sub-123"),
            ("email", "user@example.com"),
            ("socureIdVerificationLevel", "1.5"),
            ("socureIdVerificationDate", DateTime.UtcNow.ToString("o")));

        var result = _service.GenerateForOidcLogin(user, principal, isStepUp: true);

        Assert.True(result.IsSuccess);
        var jwt = ReadJwt(result.Value);
        Assert.Equal("1plus", jwt.Claims.First(c => c.Type == JwtClaimTypes.Ial).Value);
    }

    [Fact]
    public void StepUp_WithIal2VerificationClaims_SetsIal2()
    {
        var user = new User { Id = 1 };
        var principal = MakePrincipal(
            ("sub", "idp-sub-123"),
            ("email", "user@example.com"),
            ("socureIdVerificationLevel", "2"),
            ("socureIdVerificationDate", DateTime.UtcNow.ToString("o")));

        var result = _service.GenerateForOidcLogin(user, principal, isStepUp: true);

        Assert.True(result.IsSuccess);
        var jwt = ReadJwt(result.Value);
        Assert.Equal("2", jwt.Claims.First(c => c.Type == JwtClaimTypes.Ial).Value);
    }

    [Fact]
    public void StepUp_WithoutVerificationClaims_ReturnsDependencyFailed()
    {
        var user = new User { Id = 1 };
        var principal = MakePrincipal(
            ("sub", "idp-sub-123"),
            ("email", "user@example.com"));

        var result = _service.GenerateForOidcLogin(user, principal, isStepUp: true);

        Assert.False(result.IsSuccess);
        Assert.IsType<DependencyFailedResult<string>>(result);
    }

    // --- Claim handling ---

    [Fact]
    public void SubClaimIsAlwaysUserId_NotIdpSub()
    {
        var user = new User { Id = 42 };
        var principal = MakePrincipal(
            ("sub", "idp-sub-999"),
            ("email", "user@example.com"));

        var result = _service.GenerateForOidcLogin(user, principal, isStepUp: false);

        var jwt = ReadJwt(result.Value);
        var subClaim = jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub);
        Assert.Equal("42", subClaim.Value);
    }

    [Fact]
    public void EmailComesFromIdpClaims()
    {
        var user = new User { Id = 1, Email = "old@example.com" };
        var principal = MakePrincipal(
            ("sub", "idp-sub-123"),
            ("email", "oidc@example.com"));

        var result = _service.GenerateForOidcLogin(user, principal, isStepUp: false);

        var jwt = ReadJwt(result.Value);
        var emailClaim = jwt.Claims.First(c => c.Type == ClaimTypes.Email);
        Assert.Equal("oidc@example.com", emailClaim.Value);
    }

    [Fact]
    public void ApplicationClaimsPassThrough()
    {
        var user = new User { Id = 1 };
        var principal = MakePrincipal(
            ("sub", "idp-sub-123"),
            ("email", "user@example.com"),
            ("phone", "+13035551234"),
            ("givenName", "Jane"),
            ("familyName", "Doe"));

        var result = _service.GenerateForOidcLogin(user, principal, isStepUp: false);

        var jwt = ReadJwt(result.Value);
        Assert.Equal("+13035551234", jwt.Claims.First(c => c.Type == "phone").Value);
        Assert.Equal("Jane", jwt.Claims.First(c => c.Type == "givenName").Value);
        Assert.Equal("Doe", jwt.Claims.First(c => c.Type == "familyName").Value);
    }

    [Fact]
    public void InfrastructureClaimsAreFilteredOut()
    {
        var user = new User { Id = 1 };
        var principal = MakePrincipal(
            ("sub", "idp-sub-123"),
            ("email", "user@example.com"),
            ("iss", "https://idp.example.com"),
            ("aud", "client-id"),
            ("iat", "1700000000"),
            ("exp", "1700003600"),
            ("nonce", "abc123"),
            ("at_hash", "xyz789"));

        var result = _service.GenerateForOidcLogin(user, principal, isStepUp: false);

        var jwt = ReadJwt(result.Value);
        // iss and aud should be the portal's values, not the IdP's
        Assert.Equal("TestIssuer", jwt.Issuer);
        Assert.Contains("TestAudience", jwt.Audiences);
        // nonce and at_hash should not appear in the portal JWT
        Assert.Null(jwt.Claims.FirstOrDefault(c => c.Type == "nonce"));
        Assert.Null(jwt.Claims.FirstOrDefault(c => c.Type == "at_hash"));
    }
}
```

- [ ] **Step 2: Verify tests don't compile yet**

Run: `dotnet build test/SEBT.Portal.Tests/SEBT.Portal.Tests.csproj`
Expected: Compilation error — `JwtTokenService` doesn't have a constructor that takes `OidcVerificationClaimTranslator`, and doesn't implement `IOidcTokenService`.

- [ ] **Step 3: Commit**

```
test: add OidcTokenServiceTests (red — implementation pending)
```

---

### Task 3: Write tests for GenerateForLocalLogin (red)

**Files:**
- Create: `test/SEBT.Portal.Tests/Unit/Services/LocalLoginTokenServiceTests.cs`

- [ ] **Step 1: Create LocalLoginTokenServiceTests**

```csharp
// test/SEBT.Portal.Tests/Unit/Services/LocalLoginTokenServiceTests.cs
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Infrastructure.Services;

namespace SEBT.Portal.Tests.Unit.Services;

public class LocalLoginTokenServiceTests
{
    private readonly ILocalLoginTokenService _service;

    public LocalLoginTokenServiceTests()
    {
        var jwtOptions = Substitute.For<IOptions<JwtSettings>>();
        jwtOptions.Value.Returns(new JwtSettings
        {
            SecretKey = "TestSecretKeyMustBeAtLeast32CharactersLongForSecurity",
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            ExpirationMinutes = 60
        });

        var validityOptions = Substitute.For<IOptions<IdProofingValiditySettings>>();
        validityOptions.Value.Returns(new IdProofingValiditySettings { ValidityDays = 1826 });

        var translator = new OidcVerificationClaimTranslator(
            new OidcVerificationClaimSettings(),
            new IdProofingValiditySettings { ValidityDays = 1826 },
            NullLogger<OidcVerificationClaimTranslator>.Instance);

        _service = new JwtTokenService(jwtOptions, validityOptions, translator);
    }

    private static JwtSecurityToken ReadJwt(string token) =>
        new JwtSecurityTokenHandler().ReadJwtToken(token);

    [Fact]
    public void SubClaimIsUserId()
    {
        var user = new User { Id = 42, Email = "user@example.com" };

        var token = _service.GenerateForLocalLogin(user);

        var jwt = ReadJwt(token);
        Assert.Equal("42", jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value);
    }

    [Fact]
    public void EmailComesFromUserEntity()
    {
        var user = new User { Id = 1, Email = "otp-user@example.com" };

        var token = _service.GenerateForLocalLogin(user);

        var jwt = ReadJwt(token);
        Assert.Equal("otp-user@example.com", jwt.Claims.First(c => c.Type == ClaimTypes.Email).Value);
    }

    [Fact]
    public void IalComesFromUserIalLevel()
    {
        var user = new User { Id = 1, Email = "user@example.com", IalLevel = UserIalLevel.IAL1plus };

        var token = _service.GenerateForLocalLogin(user);

        var jwt = ReadJwt(token);
        Assert.Equal("1plus", jwt.Claims.First(c => c.Type == JwtClaimTypes.Ial).Value);
    }

    [Fact]
    public void IdProofingStatusComesFromUser()
    {
        var user = new User
        {
            Id = 1,
            Email = "user@example.com",
            IdProofingStatus = IdProofingStatus.Completed,
            IalLevel = UserIalLevel.IAL1plus,
            IdProofingCompletedAt = DateTime.UtcNow.AddDays(-5)
        };

        var token = _service.GenerateForLocalLogin(user);

        var jwt = ReadJwt(token);
        Assert.Equal(
            ((int)IdProofingStatus.Completed).ToString(),
            jwt.Claims.First(c => c.Type == JwtClaimTypes.IdProofingStatus).Value);
    }

    [Fact]
    public void CompletedUser_HasCompletedAtAndExpiresAt()
    {
        var completedAt = DateTime.UtcNow.AddDays(-10);
        var user = new User
        {
            Id = 1,
            Email = "user@example.com",
            IalLevel = UserIalLevel.IAL1plus,
            IdProofingStatus = IdProofingStatus.Completed,
            IdProofingCompletedAt = completedAt
        };

        var token = _service.GenerateForLocalLogin(user);

        var jwt = ReadJwt(token);
        var completedAtClaim = jwt.Claims.First(c => c.Type == JwtClaimTypes.IdProofingCompletedAt);
        Assert.True(long.TryParse(completedAtClaim.Value, out _));

        var expiresAtClaim = jwt.Claims.First(c => c.Type == JwtClaimTypes.IdProofingExpiresAt);
        Assert.True(long.TryParse(expiresAtClaim.Value, out var expiresAtUnix));
        var expectedExpiresAt = completedAt.AddDays(1826);
        var actualExpiresAt = DateTimeOffset.FromUnixTimeSeconds(expiresAtUnix).UtcDateTime;
        Assert.True(Math.Abs((actualExpiresAt - expectedExpiresAt).TotalSeconds) < 1);
    }

    [Fact]
    public void UserWithNoProofing_HasNoCompletedAtOrExpiresAt()
    {
        var user = new User { Id = 1, Email = "user@example.com", IalLevel = UserIalLevel.None };

        var token = _service.GenerateForLocalLogin(user);

        var jwt = ReadJwt(token);
        Assert.Null(jwt.Claims.FirstOrDefault(c => c.Type == JwtClaimTypes.IdProofingCompletedAt));
        Assert.Null(jwt.Claims.FirstOrDefault(c => c.Type == JwtClaimTypes.IdProofingExpiresAt));
    }

    [Fact]
    public void IncludesIdProofingSessionId_WhenPresent()
    {
        var user = new User
        {
            Id = 1,
            Email = "user@example.com",
            IdProofingSessionId = "session-abc-123"
        };

        var token = _service.GenerateForLocalLogin(user);

        var jwt = ReadJwt(token);
        Assert.Equal("session-abc-123",
            jwt.Claims.First(c => c.Type == JwtClaimTypes.IdProofingSessionId).Value);
    }

    [Fact]
    public void OmitsIdProofingSessionId_WhenNull()
    {
        var user = new User { Id = 1, Email = "user@example.com", IdProofingSessionId = null };

        var token = _service.GenerateForLocalLogin(user);

        var jwt = ReadJwt(token);
        Assert.Null(jwt.Claims.FirstOrDefault(c => c.Type == JwtClaimTypes.IdProofingSessionId));
    }

    [Fact]
    public void ProducesValidJwtWithStandardClaims()
    {
        var user = new User { Id = 1, Email = "user@example.com" };

        var token = _service.GenerateForLocalLogin(user);

        var jwt = ReadJwt(token);
        Assert.Equal("TestIssuer", jwt.Issuer);
        Assert.Contains("TestAudience", jwt.Audiences);
        Assert.NotNull(jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti));
        Assert.NotNull(jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Iat));
        Assert.True(jwt.ValidTo > DateTime.UtcNow);
    }
}
```

- [ ] **Step 2: Commit**

```
test: add LocalLoginTokenServiceTests (red — implementation pending)
```

---

### Task 4: Write tests for GenerateForSessionRefresh (red)

**Files:**
- Create: `test/SEBT.Portal.Tests/Unit/Services/SessionRefreshTokenServiceTests.cs`

- [ ] **Step 1: Create SessionRefreshTokenServiceTests**

```csharp
// test/SEBT.Portal.Tests/Unit/Services/SessionRefreshTokenServiceTests.cs
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Infrastructure.Services;

namespace SEBT.Portal.Tests.Unit.Services;

public class SessionRefreshTokenServiceTests
{
    private readonly ISessionRefreshTokenService _service;

    public SessionRefreshTokenServiceTests()
    {
        var jwtOptions = Substitute.For<IOptions<JwtSettings>>();
        jwtOptions.Value.Returns(new JwtSettings
        {
            SecretKey = "TestSecretKeyMustBeAtLeast32CharactersLongForSecurity",
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            ExpirationMinutes = 60
        });

        var validityOptions = Substitute.For<IOptions<IdProofingValiditySettings>>();
        validityOptions.Value.Returns(new IdProofingValiditySettings { ValidityDays = 1826 });

        var translator = new OidcVerificationClaimTranslator(
            new OidcVerificationClaimSettings(),
            new IdProofingValiditySettings { ValidityDays = 1826 },
            NullLogger<OidcVerificationClaimTranslator>.Instance);

        _service = new JwtTokenService(jwtOptions, validityOptions, translator);
    }

    private static ClaimsPrincipal MakePrincipal(params (string type, string value)[] claims)
    {
        var identity = new ClaimsIdentity(
            claims.Select(c => new Claim(c.type, c.value)), "test");
        return new ClaimsPrincipal(identity);
    }

    private static JwtSecurityToken ReadJwt(string token) =>
        new JwtSecurityTokenHandler().ReadJwtToken(token);

    [Fact]
    public void CopiesIalFromExistingJwt()
    {
        var user = new User { Id = 1, IalLevel = UserIalLevel.None };
        var principal = MakePrincipal(
            (JwtClaimTypes.Ial, "1plus"),
            (JwtClaimTypes.IdProofingStatus, ((int)IdProofingStatus.Completed).ToString()),
            (JwtClaimTypes.IdProofingCompletedAt, "1700000000"),
            (JwtClaimTypes.IdProofingExpiresAt, "1857676800"),
            ("email", "user@example.com"));

        var token = _service.GenerateForSessionRefresh(user, principal);

        var jwt = ReadJwt(token);
        Assert.Equal("1plus", jwt.Claims.First(c => c.Type == JwtClaimTypes.Ial).Value);
    }

    [Fact]
    public void CopiesIdProofingTimestamps()
    {
        var user = new User { Id = 1 };
        var principal = MakePrincipal(
            (JwtClaimTypes.Ial, "1plus"),
            (JwtClaimTypes.IdProofingStatus, "2"),
            (JwtClaimTypes.IdProofingCompletedAt, "1700000000"),
            (JwtClaimTypes.IdProofingExpiresAt, "1857676800"),
            ("email", "user@example.com"));

        var token = _service.GenerateForSessionRefresh(user, principal);

        var jwt = ReadJwt(token);
        Assert.Equal("1700000000", jwt.Claims.First(c => c.Type == JwtClaimTypes.IdProofingCompletedAt).Value);
        Assert.Equal("1857676800", jwt.Claims.First(c => c.Type == JwtClaimTypes.IdProofingExpiresAt).Value);
    }

    [Fact]
    public void CopiesApplicationClaims()
    {
        var user = new User { Id = 1 };
        var principal = MakePrincipal(
            (JwtClaimTypes.Ial, "1"),
            (JwtClaimTypes.IdProofingStatus, "0"),
            ("email", "user@example.com"),
            ("phone", "+13035551234"),
            ("givenName", "Jane"));

        var token = _service.GenerateForSessionRefresh(user, principal);

        var jwt = ReadJwt(token);
        Assert.Equal("+13035551234", jwt.Claims.First(c => c.Type == "phone").Value);
        Assert.Equal("Jane", jwt.Claims.First(c => c.Type == "givenName").Value);
    }

    [Fact]
    public void SubIsAlwaysUserId_NotFromExistingJwt()
    {
        var user = new User { Id = 42 };
        var principal = MakePrincipal(
            (JwtRegisteredClaimNames.Sub, "999"),
            (JwtClaimTypes.Ial, "1"),
            (JwtClaimTypes.IdProofingStatus, "0"),
            ("email", "user@example.com"));

        var token = _service.GenerateForSessionRefresh(user, principal);

        var jwt = ReadJwt(token);
        var subClaims = jwt.Claims.Where(c => c.Type == JwtRegisteredClaimNames.Sub).ToList();
        Assert.Single(subClaims);
        Assert.Equal("42", subClaims[0].Value);
    }

    [Fact]
    public void FallsBackToUserEntity_WhenPrincipalLacksIal()
    {
        var user = new User { Id = 1, IalLevel = UserIalLevel.IAL1plus, Email = "user@example.com" };
        var principal = MakePrincipal(("email", "user@example.com"));

        var token = _service.GenerateForSessionRefresh(user, principal);

        var jwt = ReadJwt(token);
        Assert.Equal("1plus", jwt.Claims.First(c => c.Type == JwtClaimTypes.Ial).Value);
    }
}
```

- [ ] **Step 2: Commit**

```
test: add SessionRefreshTokenServiceTests (red — implementation pending)
```

---

### Task 5: Implement the three methods on JwtTokenService (green)

**Files:**
- Modify: `src/SEBT.Portal.Infrastructure/Services/JwtTokenService.cs` — major rewrite

The key structural change: each public method resolves claims from its source, then delegates to `BuildAndSignToken`. The `ExpiresAt` computation happens right next to `CompletedAt` in each method — the original bug class is structurally impossible.

- [ ] **Step 1: Rewrite JwtTokenService**

Replace the entire contents of `src/SEBT.Portal.Infrastructure/Services/JwtTokenService.cs` with:

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Services;

namespace SEBT.Portal.Infrastructure.Services;

/// <summary>
/// Generates portal JWTs for all authentication paths. Implements three focused
/// interfaces — one per caller — so each entry point owns its claim resolution.
/// A shared <see cref="BuildAndSignToken"/> handles mechanical JWT construction.
/// </summary>
public class JwtTokenService : ILocalLoginTokenService, IOidcTokenService, ISessionRefreshTokenService
{
    private readonly JwtSettings _settings;
    private readonly IdProofingValiditySettings _validitySettings;
    private readonly OidcVerificationClaimTranslator _verificationClaimTranslator;

    /// <summary>
    /// Standard OIDC/JWT infrastructure claim names excluded when copying IdP claims.
    /// Parallel to OidcExchangeService.CommonOidcInfrastructureClaims (in Api layer).
    /// </summary>
    private static readonly HashSet<string> OidcInfrastructureClaims =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "iss", "aud", "iat", "exp", "nbf", "nonce", "at_hash", "c_hash",
            "auth_time", "acr", "amr", "azp", "sid", "jti",
            "env", "org", "p1.region"
        };

    /// <summary>
    /// Claim names that BuildAndSignToken sets directly — excluded from the passthrough loop
    /// to avoid duplicates (which .NET's JWT reader rejects).
    /// </summary>
    private static readonly HashSet<string> ReservedClaimNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            JwtRegisteredClaimNames.Sub,
            ClaimTypes.Email,
            JwtRegisteredClaimNames.Email,
            "email",
            JwtRegisteredClaimNames.Jti,
            JwtRegisteredClaimNames.Iat,
            JwtRegisteredClaimNames.Nbf,
            JwtRegisteredClaimNames.Aud,
            JwtRegisteredClaimNames.Iss,
            JwtClaimTypes.Ial,
            JwtClaimTypes.IdProofingStatus,
            JwtClaimTypes.IdProofingSessionId,
            JwtClaimTypes.IdProofingCompletedAt,
            JwtClaimTypes.IdProofingExpiresAt,
        };

    public JwtTokenService(
        IOptions<JwtSettings> settings,
        IOptions<IdProofingValiditySettings> validitySettings,
        OidcVerificationClaimTranslator verificationClaimTranslator)
    {
        _settings = settings.Value;
        _validitySettings = validitySettings.Value;
        _verificationClaimTranslator = verificationClaimTranslator;
    }

    // ──────────────────────────────────────────────
    //  ILocalLoginTokenService
    // ──────────────────────────────────────────────

    /// <inheritdoc />
    public string GenerateForLocalLogin(User user)
    {
        var resolved = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        resolved[JwtClaimTypes.Ial] = user.IalLevel switch
        {
            UserIalLevel.IAL1plus => "1plus",
            UserIalLevel.IAL2 => "2",
            _ => "1"
        };
        resolved[JwtClaimTypes.IdProofingStatus] = ((int)user.IdProofingStatus).ToString();

        if (!string.IsNullOrWhiteSpace(user.IdProofingSessionId))
        {
            resolved[JwtClaimTypes.IdProofingSessionId] = user.IdProofingSessionId;
        }

        if (user.IdProofingCompletedAt.HasValue)
        {
            var completedAtOffset = new DateTimeOffset(user.IdProofingCompletedAt.Value, TimeSpan.Zero);
            resolved[JwtClaimTypes.IdProofingCompletedAt] = completedAtOffset.ToUnixTimeSeconds().ToString();

            var expiresAt = user.IdProofingCompletedAt.Value.AddDays(_validitySettings.ValidityDays);
            resolved[JwtClaimTypes.IdProofingExpiresAt] =
                new DateTimeOffset(expiresAt, TimeSpan.Zero).ToUnixTimeSeconds().ToString();
        }

        var email = user.Email ?? "";
        return BuildAndSignToken(user.Id, email, resolved);
    }

    // ──────────────────────────────────────────────
    //  IOidcTokenService
    // ──────────────────────────────────────────────

    /// <inheritdoc />
    public Kernel.Result<string> GenerateForOidcLogin(
        User user, ClaimsPrincipal idpPrincipal, bool isStepUp)
    {
        // Filter infrastructure claims from the IdP principal
        var idpClaims = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var claim in idpPrincipal.Claims)
        {
            if (!OidcInfrastructureClaims.Contains(claim.Type) && !string.IsNullOrEmpty(claim.Value))
            {
                idpClaims[claim.Type] = claim.Value;
            }
        }

        // Translate verification claims (e.g., socureIdVerificationLevel → IAL)
        var verification = _verificationClaimTranslator.Translate(idpClaims);

        if (isStepUp && verification == null)
        {
            return Kernel.Result<string>.DependencyFailed(
                Kernel.Results.DependencyFailedReason.BadRequest,
                "Step-up verification failed: IdP returned no verification claims.");
        }

        // Resolve IAL and ID proofing state
        if (verification != null)
        {
            idpClaims[JwtClaimTypes.Ial] = verification.IsExpired
                ? "1"
                : verification.IalLevel switch
                {
                    UserIalLevel.IAL1plus => "1plus",
                    UserIalLevel.IAL2 => "2",
                    _ => "1"
                };

            idpClaims[JwtClaimTypes.IdProofingStatus] =
                (verification.IsExpired, verification.IalLevel) switch
                {
                    (true, _) => ((int)IdProofingStatus.Expired).ToString(),
                    (_, UserIalLevel.IAL1plus) => ((int)IdProofingStatus.Completed).ToString(),
                    (_, UserIalLevel.IAL2) => ((int)IdProofingStatus.Completed).ToString(),
                    _ => ((int)IdProofingStatus.NotStarted).ToString()
                };

            // CompletedAt and ExpiresAt computed together — the gap that caused
            // the step-up loop bug is structurally impossible here.
            if (verification.VerifiedAt != default)
            {
                var verifiedAtOffset = new DateTimeOffset(verification.VerifiedAt, TimeSpan.Zero);
                idpClaims[JwtClaimTypes.IdProofingCompletedAt] =
                    verifiedAtOffset.ToUnixTimeSeconds().ToString();

                var expiresAt = verification.VerifiedAt.AddDays(_validitySettings.ValidityDays);
                idpClaims[JwtClaimTypes.IdProofingExpiresAt] =
                    new DateTimeOffset(expiresAt, TimeSpan.Zero).ToUnixTimeSeconds().ToString();
            }
        }
        else
        {
            // No verification claims — user is IAL1 (authenticated but not verified)
            idpClaims[JwtClaimTypes.Ial] = "1";
            idpClaims[JwtClaimTypes.IdProofingStatus] = ((int)IdProofingStatus.NotStarted).ToString();
        }

        var email = idpClaims.GetValueOrDefault("email") ?? user.Email ?? "";
        return Kernel.Result<string>.Success(BuildAndSignToken(user.Id, email, idpClaims));
    }

    // ──────────────────────────────────────────────
    //  ISessionRefreshTokenService
    // ──────────────────────────────────────────────

    /// <inheritdoc />
    public string GenerateForSessionRefresh(User user, ClaimsPrincipal currentPrincipal)
    {
        var existingClaims = currentPrincipal.Claims
            .DistinctBy(c => c.Type)
            .Where(c => !string.IsNullOrEmpty(c.Value))
            .ToDictionary(c => c.Type, c => c.Value, StringComparer.OrdinalIgnoreCase);

        // Resolve IAL: prefer existing JWT claim, fall back to user entity
        if (!existingClaims.ContainsKey(JwtClaimTypes.Ial))
        {
            existingClaims[JwtClaimTypes.Ial] = user.IalLevel switch
            {
                UserIalLevel.IAL1plus => "1plus",
                UserIalLevel.IAL2 => "2",
                _ => "1"
            };
        }

        if (!existingClaims.ContainsKey(JwtClaimTypes.IdProofingStatus))
        {
            existingClaims[JwtClaimTypes.IdProofingStatus] = ((int)user.IdProofingStatus).ToString();
        }

        var email = existingClaims.GetValueOrDefault("email")
            ?? existingClaims.GetValueOrDefault(ClaimTypes.Email)
            ?? user.Email ?? "";

        return BuildAndSignToken(user.Id, email, existingClaims);
    }

    // ──────────────────────────────────────────────
    //  Shared JWT construction
    // ──────────────────────────────────────────────

    /// <summary>
    /// Mechanical JWT construction from pre-resolved claims. Each public method
    /// fully resolves its claims before calling this — no fallback logic here.
    /// </summary>
    private string BuildAndSignToken(
        int userId,
        string email,
        IReadOnlyDictionary<string, string> resolvedClaims)
    {
        var ialValue = resolvedClaims.GetValueOrDefault(JwtClaimTypes.Ial) ?? "1";
        var idProofingStatusValue = resolvedClaims.GetValueOrDefault(JwtClaimTypes.IdProofingStatus)
            ?? ((int)IdProofingStatus.NotStarted).ToString();

        // Invariant: Completed ID proofing must have IAL > 1 and a completion timestamp.
        if (idProofingStatusValue == ((int)IdProofingStatus.Completed).ToString())
        {
            if (ialValue == "1")
            {
                throw new InvalidOperationException(
                    "Cannot mint JWT with IdProofingStatus=Completed and IAL=1. " +
                    "Completed identity proofing must elevate IAL above 1.");
            }

            if (!resolvedClaims.ContainsKey(JwtClaimTypes.IdProofingCompletedAt))
            {
                throw new InvalidOperationException(
                    "Cannot mint JWT with IdProofingStatus=Completed without a completion timestamp. " +
                    "IdProofingCompletedAt is required to compute expiration.");
            }
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var now = DateTimeOffset.UtcNow;
        var unixTimeSeconds = now.ToUnixTimeSeconds();

        var claims = new List<Claim>
        {
            new(ClaimTypes.Email, email),
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat, unixTimeSeconds.ToString(), ClaimValueTypes.Integer64),
            new(JwtRegisteredClaimNames.Nbf, unixTimeSeconds.ToString(), ClaimValueTypes.Integer64),
            new(JwtRegisteredClaimNames.Aud, "SEBT.Portal.Web"),
            new(JwtRegisteredClaimNames.Iss, "SEBT.Portal.Api"),
            new(JwtClaimTypes.IdProofingStatus, idProofingStatusValue, ClaimValueTypes.Integer32),
            new(JwtClaimTypes.Ial, ialValue)
        };

        // Add optional ID proofing claims if present in resolved set
        if (resolvedClaims.TryGetValue(JwtClaimTypes.IdProofingSessionId, out var sessionId)
            && !string.IsNullOrWhiteSpace(sessionId))
        {
            claims.Add(new Claim(JwtClaimTypes.IdProofingSessionId, sessionId));
        }
        if (resolvedClaims.TryGetValue(JwtClaimTypes.IdProofingCompletedAt, out var completedAt))
        {
            claims.Add(new Claim(JwtClaimTypes.IdProofingCompletedAt, completedAt, ClaimValueTypes.Integer64));
        }
        if (resolvedClaims.TryGetValue(JwtClaimTypes.IdProofingExpiresAt, out var expiresAt))
        {
            claims.Add(new Claim(JwtClaimTypes.IdProofingExpiresAt, expiresAt, ClaimValueTypes.Integer64));
        }

        // Passthrough remaining application claims (phone, givenName, etc.)
        foreach (var (name, value) in resolvedClaims)
        {
            if (!string.IsNullOrEmpty(name)
                && value != null
                && !ReservedClaimNames.Contains(name)
                && !claims.Any(c => c.Type == name))
            {
                claims.Add(new Claim(name, value));
            }
        }

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_settings.ExpirationMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
```

- [ ] **Step 2: Run new tests**

Run: `dotnet test test/SEBT.Portal.Tests/SEBT.Portal.Tests.csproj --filter "FullyQualifiedName~OidcTokenServiceTests|FullyQualifiedName~LocalLoginTokenServiceTests|FullyQualifiedName~SessionRefreshTokenServiceTests" --verbosity normal`
Expected: All new tests pass. Note: old `JwtTokenServiceTests` will fail (constructor change) — that's expected.

- [ ] **Step 3: Commit**

```
feat: implement GenerateForLocalLogin, GenerateForOidcLogin, GenerateForSessionRefresh

JwtTokenService now implements three focused interfaces. Each method
owns its claim resolution; BuildAndSignToken is mechanical. The ExpiresAt
computation is co-located with CompletedAt in each path, making the
step-up loop bug class structurally impossible.
```

---

### Task 6: Update DI registration and OidcController

**Files:**
- Modify: `src/SEBT.Portal.Infrastructure/Dependencies.cs:28` — register three interfaces
- Modify: `src/SEBT.Portal.Api/Controllers/Auth/OidcController.cs` — use `IOidcTokenService`, simplify CompleteLogin

- [ ] **Step 1: Update DI registration**

In `src/SEBT.Portal.Infrastructure/Dependencies.cs`, replace the JWT service registration (line 28):

```csharp
// Before:
services.AddTransient<IJwtTokenService, JwtTokenService>();

// After:
services.AddTransient<JwtTokenService>();
services.AddTransient<ILocalLoginTokenService>(sp => sp.GetRequiredService<JwtTokenService>());
services.AddTransient<IOidcTokenService>(sp => sp.GetRequiredService<JwtTokenService>());
services.AddTransient<ISessionRefreshTokenService>(sp => sp.GetRequiredService<JwtTokenService>());
```

Also update the `OidcVerificationClaimTranslator` registration (lines 31–35) since `JwtTokenService` now takes it as a constructor parameter — the DI container resolves it automatically. No change needed to the translator registration itself, but verify it's registered before `JwtTokenService`.

- [ ] **Step 2: Update OidcController constructor and CompleteLogin**

In `src/SEBT.Portal.Api/Controllers/Auth/OidcController.cs`, make these changes:

**Constructor** (line 27–36): Replace `IJwtTokenService jwtService` with `IOidcTokenService oidcTokenService` and remove `OidcVerificationClaimTranslator verificationClaimTranslator`:

```csharp
public class OidcController(
    IConfiguration config,
    ILogger<OidcController> logger,
    IUserRepository userRepository,
    IOidcTokenService oidcTokenService,
    IOptions<JwtSettings> jwtSettingsOptions,
    IStateAllowlist stateAllowlist,
    IPreAuthSessionStore sessionStore,
    IWebHostEnvironment environment) : ControllerBase
```

**CompleteLogin** (lines 411–534): Replace the entire claim-processing block (lines 411–520) with a streamlined version. The service now owns claim extraction, verification translation, and IAL derivation. The controller keeps user lookup, HTTP responses, and diagnostic logging.

Replace lines 411–534 (from `// Copy non-common IdP claims` through the end of the method) with:

```csharp
        // Extract sub + email from principal for user lookup (not claim processing —
        // the service handles that). The callback token's claims were filtered by
        // OidcExchangeService; the principal also includes JWT infrastructure claims
        // from validation, but those are the service's concern.
        var subClaim = principal.FindFirst("sub")?.Value;
        var email = GetEmailFromClaims(principal);
        var phoneClaim = principal.FindFirst("phone")?.Value;
        var maskedPhone = MaskPhone(phoneClaim);

        if (phoneClaim == null)
        {
            logger.LogWarning("OIDC incoming claims missing 'phone' (SessionId={SessionId})", sessionId);
        }

        if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(subClaim))
        {
            logger.LogWarning("Callback token had no email or sub claim (SessionId={SessionId})", sessionId);
            return BadRequest(new ErrorResponse("Callback token must contain an email or sub claim."));
        }

        User user;

        if (session.IsStepUp)
        {
            if (string.IsNullOrWhiteSpace(subClaim))
            {
                logger.LogWarning("Step-up complete-login: missing sub claim (SessionId={SessionId})", sessionId);
                return BadRequest(new ErrorResponse("Callback token must contain a sub claim."));
            }

            var existingEntity = await userRepository.GetUserByExternalIdAsync(subClaim, cancellationToken);
            if (existingEntity == null)
            {
                logger.LogWarning(
                    "Step-up complete-login: no existing portal user for sub claim; sign-in required first (SessionId={SessionId}).",
                    sessionId);
                return BadRequest(new { error = "Step-up requires an existing session. Please sign in again." });
            }

            user = existingEntity;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(subClaim))
            {
                logger.LogWarning(
                    "OIDC CompleteLogin: callback token missing sub claim (SessionId={SessionId})",
                    sessionId);
                return BadRequest(new ErrorResponse("Callback token must contain a sub claim."));
            }

            var emailHint = principal.FindFirst("email")?.Value;
            var (createdUser, _) = await userRepository.GetOrCreateUserByExternalIdAsync(
                subClaim, emailHint, cancellationToken);
            user = createdUser;
        }

        // The service handles claim filtering, verification translation,
        // IAL derivation, and timestamp computation.
        var tokenResult = oidcTokenService.GenerateForOidcLogin(user, principal, session.IsStepUp);

        if (!tokenResult.IsSuccess)
        {
            logger.LogWarning(
                "OIDC token generation failed: {Message} (SessionId={SessionId})",
                tokenResult.Message, sessionId);
            return BadRequest(new { error = "Step-up verification failed. Please try again." });
        }

        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(jwtSettingsOptions.Value.ExpirationMinutes);
        AuthCookies.SetAuthCookie(Response, tokenResult.Value, expiresAt);

        logger.LogInformation(
            "OIDC {FlowType} complete: UserId {UserId}, Phone={MaskedPhone}, SessionId={SessionId}",
            session.IsStepUp ? "step-up" : "login", user.Id, maskedPhone, sessionId);

        var safeReturnUrl = session.IsStepUp ? session.ReturnUrl : null;
        return Ok(new CompleteLoginResponse(ReturnUrl: safeReturnUrl));
```

**Remove** the `ApplyVerificationToClaims` static method (lines 596–628) — this logic now lives inside `GenerateForOidcLogin`.

Also remove the `using SEBT.Portal.Infrastructure.Services;` import (line 14) if the controller no longer references `OidcVerificationClaimTranslator` directly.

- [ ] **Step 3: Verify the solution builds**

Run: `dotnet build src/SEBT.Portal.Api/SEBT.Portal.Api.csproj`
Expected: Build succeeded. (The old `IJwtTokenService` is still referenced by handler files — addressed in next task.)

- [ ] **Step 4: Commit**

```
refactor: wire OidcController to IOidcTokenService, remove claim processing from controller

CompleteLogin shrinks from ~130 lines to ~70 lines. Claim filtering,
verification translation, IAL derivation, and timestamp computation
all move into JwtTokenService.GenerateForOidcLogin.
```

---

### Task 7: Update ValidateOtpCommandHandler and RefreshTokenCommandHandler

**Files:**
- Modify: `src/SEBT.Portal.UseCases/Auth/ValidateOtp/ValidateOtpCommandHandler.cs` — `IJwtTokenService` → `ILocalLoginTokenService`
- Modify: `src/SEBT.Portal.UseCases/Auth/RefreshToken/RefreshTokenCommandHandler.cs` — `IJwtTokenService` → `ISessionRefreshTokenService`

- [ ] **Step 1: Update ValidateOtpCommandHandler**

In `src/SEBT.Portal.UseCases/Auth/ValidateOtp/ValidateOtpCommandHandler.cs`:

Replace constructor parameter (line 30):
```csharp
// Before:
IJwtTokenService jwtTokenService,

// After:
ILocalLoginTokenService jwtTokenService,
```

Replace call site (line 80):
```csharp
// Before:
var token = jwtTokenService.GenerateToken(user);

// After:
var token = jwtTokenService.GenerateForLocalLogin(user);
```

- [ ] **Step 2: Update RefreshTokenCommandHandler**

In `src/SEBT.Portal.UseCases/Auth/RefreshToken/RefreshTokenCommandHandler.cs`:

Replace constructor parameter:
```csharp
// Before:
IJwtTokenService jwtTokenService,

// After:
ISessionRefreshTokenService jwtTokenService,
```

Replace the call site (lines 66–69):
```csharp
// Before:
var additionalClaims = command.CurrentPrincipal.Claims
    .DistinctBy(c => c.Type)
    .ToDictionary(c => c.Type, c => c.Value);
var token = jwtTokenService.GenerateToken(user, additionalClaims);

// After:
var token = jwtTokenService.GenerateForSessionRefresh(user, command.CurrentPrincipal);
```

- [ ] **Step 3: Verify the solution builds**

Run: `dotnet build`
Expected: Build succeeded (ignoring test projects that still reference old interfaces).

- [ ] **Step 4: Commit**

```
refactor: update OTP and refresh handlers to use focused token interfaces
```

---

### Task 8: Update caller tests

**Files:**
- Modify: `test/SEBT.Portal.Tests/Unit/Controllers/OidcControllerTests.cs`
- Modify: `test/SEBT.Portal.Tests/Unit/UseCases/Auth/ValidateOtpCommandHandlerTests.cs`
- Modify: `test/SEBT.Portal.Tests/Unit/UseCases/Auth/RefreshTokenCommandHandlerTests.cs`

- [ ] **Step 1: Update OidcControllerTests**

In `test/SEBT.Portal.Tests/Unit/Controllers/OidcControllerTests.cs`:

Replace mock field (line 30):
```csharp
// Before:
private readonly IJwtTokenService _jwtService = Substitute.For<IJwtTokenService>();

// After:
private readonly IOidcTokenService _oidcTokenService = Substitute.For<IOidcTokenService>();
```

Update constructor — replace `_jwtService` with `_oidcTokenService` in the controller construction (line 78), and remove `translator` (lines 69–72) since the controller no longer takes it:

```csharp
_controller = new OidcController(
    _config,
    NullLogger<OidcController>.Instance,
    _userRepository,
    _oidcTokenService,
    jwtSettings,
    allowlist,
    _sessionStore,
    env)
```

Update all test methods that configure/assert against `_jwtService`:

The mock pattern changes from:
```csharp
// Before:
_jwtService.GenerateToken(Arg.Any<User>(), Arg.Any<IReadOnlyDictionary<string, string>?>())
    .Returns("portal-jwt");

// After:
_oidcTokenService.GenerateForOidcLogin(Arg.Any<User>(), Arg.Any<ClaimsPrincipal>(), Arg.Any<bool>())
    .Returns(Kernel.Result<string>.Success("portal-jwt"));
```

For tests that capture claims (e.g., `CompleteLogin_WhenOidcClaimsContainFreshVerification_SetsIalInClaimsNotDb`), the claim assertions move to `OidcTokenServiceTests` — the controller tests should now only verify that the service was called with the correct user and `isStepUp` flag, and that the result is handled correctly.

For the step-up verification failure test, configure the mock to return a failure:
```csharp
_oidcTokenService.GenerateForOidcLogin(Arg.Any<User>(), Arg.Any<ClaimsPrincipal>(), true)
    .Returns(Kernel.Result<string>.DependencyFailed(
        Kernel.Results.DependencyFailedReason.BadRequest,
        "Step-up verification failed"));
```

- [ ] **Step 2: Update ValidateOtpCommandHandlerTests**

In `test/SEBT.Portal.Tests/Unit/UseCases/Auth/ValidateOtpCommandHandlerTests.cs`:

Replace mock field (line 19):
```csharp
// Before:
private readonly IJwtTokenService jwtTokenService = Substitute.For<IJwtTokenService>();

// After:
private readonly ILocalLoginTokenService jwtTokenService = Substitute.For<ILocalLoginTokenService>();
```

Replace all `.GenerateToken(` calls with `.GenerateForLocalLogin(` throughout the file. The argument patterns stay the same (they all pass `Arg.Is<User>(...)` or `Arg.Any<User>()`). Note: these calls used the single-argument form `GenerateToken(user)`, so the change is just the method name.

Search and replace throughout the file:
- `.GenerateToken(Arg.Is<User>` → `.GenerateForLocalLogin(Arg.Is<User>`
- `.GenerateToken(Arg.Any<User>()` → `.GenerateForLocalLogin(Arg.Any<User>()`
- `.When(x => x.GenerateToken(` → `.When(x => x.GenerateForLocalLogin(`

- [ ] **Step 3: Update RefreshTokenCommandHandlerTests**

In `test/SEBT.Portal.Tests/Unit/UseCases/Auth/RefreshTokenCommandHandlerTests.cs`:

Replace mock field:
```csharp
// Before:
private readonly IJwtTokenService jwtTokenService = Substitute.For<IJwtTokenService>();

// After:
private readonly ISessionRefreshTokenService jwtTokenService = Substitute.For<ISessionRefreshTokenService>();
```

Replace all `.GenerateToken(` calls with `.GenerateForSessionRefresh(`. The argument pattern changes — the method now takes `(User, ClaimsPrincipal)` instead of `(User, IReadOnlyDictionary)`:

```csharp
// Before:
jwtTokenService.GenerateToken(Arg.Is<User>(u => u.Id == 1), Arg.Any<IReadOnlyDictionary<string, string>>())
    .Returns("refreshed.jwt.token");

// After:
jwtTokenService.GenerateForSessionRefresh(Arg.Is<User>(u => u.Id == 1), Arg.Any<ClaimsPrincipal>())
    .Returns("refreshed.jwt.token");
```

Apply this pattern to all mock setups and `Received()` assertions in the file.

- [ ] **Step 4: Run all tests**

Run: `dotnet test test/SEBT.Portal.Tests/SEBT.Portal.Tests.csproj --filter "FullyQualifiedName~OidcControllerTests|FullyQualifiedName~ValidateOtpCommandHandlerTests|FullyQualifiedName~RefreshTokenCommandHandlerTests|FullyQualifiedName~OidcTokenServiceTests|FullyQualifiedName~LocalLoginTokenServiceTests|FullyQualifiedName~SessionRefreshTokenServiceTests" --verbosity normal`
Expected: All pass.

- [ ] **Step 5: Commit**

```
test: update OidcController, OTP, and refresh handler tests for new token interfaces
```

---

### Task 9: Remove old interface and cleanup

**Files:**
- Delete: `src/SEBT.Portal.Core/Services/IJwtTokenService.cs`
- Delete: `test/SEBT.Portal.Tests/Unit/Services/JwtTokenServiceTests.cs`
- Modify: Any remaining references to `IJwtTokenService`

- [ ] **Step 1: Search for remaining references**

Run: `grep -r "IJwtTokenService" src/ test/ --include="*.cs" -l`
Expected: Only `IJwtTokenService.cs` itself. If other files appear, update them.

- [ ] **Step 2: Delete old files**

Delete `src/SEBT.Portal.Core/Services/IJwtTokenService.cs` and `test/SEBT.Portal.Tests/Unit/Services/JwtTokenServiceTests.cs`.

- [ ] **Step 3: Full build**

Run: `dotnet build`
Expected: Build succeeded with no errors. May have warnings from other files (unrelated).

- [ ] **Step 4: Full test run**

Run: `dotnet test test/SEBT.Portal.Tests/SEBT.Portal.Tests.csproj --filter "Category!=Integration&Category!=SqlServer&Category!=Socure" --verbosity normal`
Expected: All unit tests pass.

- [ ] **Step 5: Also run UseCases.Tests**

Run: `dotnet test test/SEBT.Portal.UseCases.Tests/SEBT.Portal.UseCases.Tests.csproj --verbosity normal`
Expected: All pass.

- [ ] **Step 6: Commit**

```
refactor: remove old IJwtTokenService and JwtTokenServiceTests

All callers now use focused interfaces. Test coverage is maintained
by OidcTokenServiceTests, LocalLoginTokenServiceTests,
SessionRefreshTokenServiceTests, and updated caller tests.
```

---

### Task 10: Final verification

- [ ] **Step 1: Run full unit test suite**

Run: `pnpm api:test:unit`
Expected: All tests pass.

- [ ] **Step 2: Verify no remaining references to old patterns**

Run: `grep -r "GenerateToken\b" src/ test/ --include="*.cs" -l`
Expected: No matches (all call sites updated).

Run: `grep -r "additionalClaims" src/SEBT.Portal.Infrastructure/Services/JwtTokenService.cs`
Expected: No matches (the `additionalClaims` parameter pattern is gone).

Run: `grep -r "ApplyVerificationToClaims" src/ --include="*.cs" -l`
Expected: No matches (moved into `GenerateForOidcLogin`).

- [ ] **Step 3: Review the diff for accidental scope creep**

Run: `git diff main --stat`
Verify the change set is limited to:
- `src/SEBT.Portal.Core/Services/` — three new interfaces, one deleted
- `src/SEBT.Portal.Infrastructure/Services/JwtTokenService.cs` — rewritten
- `src/SEBT.Portal.Infrastructure/Dependencies.cs` — DI registration
- `src/SEBT.Portal.Api/Controllers/Auth/OidcController.cs` — simplified CompleteLogin
- `src/SEBT.Portal.UseCases/Auth/ValidateOtp/ValidateOtpCommandHandler.cs` — interface swap
- `src/SEBT.Portal.UseCases/Auth/RefreshToken/RefreshTokenCommandHandler.cs` — interface swap
- `test/` — three new test files, one deleted, three updated
