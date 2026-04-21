# OIDC User: IdP Claims as Source of Truth

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix stale IAL bug for OIDC users by making IdP claims the sole source of truth for identity attributes, and reducing the DB footprint for OIDC users to just an identity anchor (Id, ExternalProviderId, timestamps).

**Architecture:** OIDC users (Colorado) currently store IAL, email, and ID proofing state in the portal DB, which goes stale between logins. This change makes the IdP authoritative: the portal DB stores only a minimal record linking the user to the IdP's `sub` claim. IAL, email, and verification state all flow from IdP claims at login time and are carried in the portal JWT — never read back from the DB. OTP users (DC) are unaffected; the DB remains their source of truth.

**Tech Stack:** C# / .NET 10 / EF Core / xUnit / NSubstitute / Bogus

---

## Root Cause (for partner communication)

Two code paths read IAL from the portal database instead of from the identity provider:

1. **OIDC re-login when verification claims are absent:** If the IdP stops sending verification claims for a user (e.g., verification expires at the IdP, claim format changes), `OidcVerificationClaimTranslator.Translate()` returns `null` and the reconciliation block is skipped. The user retains whatever IAL was stored in the portal DB from a prior login — even though the IdP no longer asserts that level.

2. **Token refresh always reads DB:** `RefreshTokenCommandHandler` fetches the user from the DB and passes it to `JwtTokenService.GenerateToken()`, which hard-codes the IAL claim from `user.IalLevel`. The IAL from the existing JWT (which was correct at login time) is in `additionalClaims` but gets shadowed because `GenerateToken` sets the IAL claim from the User object first, and the dedup guard prevents the claims-based value from overriding it.

**Net effect:** A user whose IdP verification status changes (upgraded, expired, revoked) retains their old IAL in the portal until they complete a fresh OIDC login where the IdP happens to include the verification claims.

---

## Resolved Questions

1. **Household identifiers for CO:** Phone is always read from user claims (IdP), not stored in the DB for OIDC users. No DB column needed.

2. **Existing CO users in production DB:** Handled via in-flight migration: on OIDC login, if no user found by `sub` but one exists by email, adopt that record by setting `ExternalProviderId = sub`. Migration code will be marked for removal in a future release.

3. **IsCoLoaded for CO:** Irrelevant for CO. May not belong in the DB at all — revisit separately.

## Design Principles

- **For OIDC users, `sub` is the username.** The IdP subject claim is the identity anchor used for user lookup, creation, and correlation — stored as `ExternalProviderId`. Email, phone, IAL, and all other attributes flow from IdP claims and live only in the JWT.
- **For OTP users, email is the username.** No change to the existing DC/OTP flow.
- **Phone comes from claims, not DB.** For OIDC users, the phone claim from the IdP is passed through to the JWT. No phone storage in the Users table for OIDC users.

---

## File Structure

### New files
- `src/SEBT.Portal.Infrastructure/Migrations/{timestamp}_AddExternalProviderIdToUsers.cs` — EF migration

### Modified files

| File | Change |
|------|--------|
| `src/SEBT.Portal.Infrastructure/Data/Entities/UserEntity.cs` | Add `ExternalProviderId`, make `Email` nullable |
| `src/SEBT.Portal.Core/Models/Auth/User.cs` | Add `ExternalProviderId`, make `Email` nullable |
| `src/SEBT.Portal.Infrastructure/Data/PortalDbContext.cs` | Configure `ExternalProviderId` column + unique filtered index |
| `src/SEBT.Portal.Core/Repositories/IUserRepository.cs` | Add `GetOrCreateUserByExternalIdAsync` |
| `src/SEBT.Portal.Infrastructure/Repositories/DatabaseUserRepository.cs` | Implement new method |
| `src/SEBT.Portal.Api/Controllers/Auth/OidcController.cs` | Use `sub` for lookup, stop persisting IAL/email to DB |
| `src/SEBT.Portal.Infrastructure/Services/JwtTokenService.cs` | Allow additionalClaims to provide email/sub, make IAL source configurable |
| `src/SEBT.Portal.UseCases/Auth/RefreshToken/RefreshTokenCommandHandler.cs` | For OIDC users, populate user fields from JWT claims |
| `src/SEBT.Portal.UseCases/Auth/RefreshToken/RefreshTokenCommand.cs` | May need ExternalProviderId field for lookup |
| `src/SEBT.Portal.Api/Controllers/Auth/AuthController.cs` | Extract ExternalProviderId for refresh lookup |
| `src/SEBT.Portal.Infrastructure/Services/DataSeeder.cs` | Handle nullable Email |
| `test/SEBT.Portal.Tests/Unit/Controllers/OidcControllerTests.cs` | Update for new lookup method |
| `test/SEBT.Portal.Tests/Unit/UseCases/Auth/RefreshTokenCommandHandlerTests.cs` | Add OIDC user tests |
| `test/SEBT.Portal.Tests/Unit/Services/JwtTokenServiceTests.cs` | Test claims-based email/IAL override |
| `test/SEBT.Portal.Tests/Unit/Repositories/DatabaseUserRepositoryTests.cs` | Test ExternalProviderId lookup |

---

## Task 1: Add ExternalProviderId to Domain Model and Entity

**Files:**
- Modify: `src/SEBT.Portal.Core/Models/Auth/User.cs`
- Modify: `src/SEBT.Portal.Infrastructure/Data/Entities/UserEntity.cs`

- [ ] **Step 1: Add ExternalProviderId to User domain model**

```csharp
// In src/SEBT.Portal.Core/Models/Auth/User.cs, add after the Email property:

/// <summary>
/// The subject identifier from the external identity provider (e.g., PingOne sub claim).
/// Set for OIDC users; null for OTP-authenticated users.
/// </summary>
public string? ExternalProviderId { get; set; }
```

Also make `Email` nullable to support OIDC users who don't store email:

```csharp
// Change:
public string Email { get; set; } = string.Empty;
// To:
public string? Email { get; set; }
```

- [ ] **Step 2: Add ExternalProviderId to UserEntity**

```csharp
// In src/SEBT.Portal.Infrastructure/Data/Entities/UserEntity.cs, add after Email:

/// <summary>
/// The subject identifier from the external identity provider (e.g., PingOne sub claim).
/// Null for OTP-authenticated users.
/// </summary>
public string? ExternalProviderId { get; set; }
```

Make Email nullable:

```csharp
// Change:
public string Email { get; set; } = string.Empty;
// To:
public string? Email { get; set; }
```

- [ ] **Step 3: Verify the project compiles**

Run: `dotnet build src/SEBT.Portal.Api/SEBT.Portal.Api.csproj`
Expected: Compilation errors in places that assume Email is non-null. Note them — we'll fix in subsequent tasks.

- [ ] **Step 4: Commit**

```
feat: add ExternalProviderId to User model, make Email nullable

Supports OIDC users where the IdP subject claim is the identity anchor
and email is carried only in JWT claims, not stored in the DB.
```

---

## Task 2: Database Schema — Migration and DbContext Configuration

**Files:**
- Modify: `src/SEBT.Portal.Infrastructure/Data/PortalDbContext.cs`
- Create: EF migration (auto-generated)

- [ ] **Step 1: Update PortalDbContext model configuration**

In `PortalDbContext.OnModelCreating`, update the `UserEntity` configuration:

```csharp
// Change Email from required to optional:
entity.Property(e => e.Email)
    .HasMaxLength(255);
// (Remove .IsRequired() — Email is now nullable for OIDC users)

// Keep the existing unique index but make it filtered (only for non-null emails):
entity.HasIndex(e => e.Email)
    .IsUnique()
    .HasDatabaseName("IX_Users_Email")
    .HasFilter("[Email] IS NOT NULL");

// Add ExternalProviderId configuration:
entity.Property(e => e.ExternalProviderId)
    .HasMaxLength(255);
entity.HasIndex(e => e.ExternalProviderId)
    .IsUnique()
    .HasDatabaseName("IX_Users_ExternalProviderId")
    .HasFilter("[ExternalProviderId] IS NOT NULL");
```

- [ ] **Step 2: Generate the EF migration**

Run:
```bash
dotnet ef migrations add AddExternalProviderIdToUsers \
  --project src/SEBT.Portal.Infrastructure/SEBT.Portal.Infrastructure.csproj \
  --startup-project src/SEBT.Portal.Api/SEBT.Portal.Api.csproj
```

Expected: Migration file created in `src/SEBT.Portal.Infrastructure/Migrations/`.

- [ ] **Step 3: Review the generated migration**

Verify it:
- Adds `ExternalProviderId` column (nullable, max 255)
- Creates filtered unique index `IX_Users_ExternalProviderId`
- Alters `Email` to be nullable
- Updates the `IX_Users_Email` index with the filter

- [ ] **Step 4: Verify migration applies cleanly**

Run:
```bash
docker compose up -d mssql
dotnet ef database update \
  --project src/SEBT.Portal.Infrastructure/SEBT.Portal.Infrastructure.csproj \
  --startup-project src/SEBT.Portal.Api/SEBT.Portal.Api.csproj
```

Expected: Migration applied successfully.

- [ ] **Step 5: Commit**

```
feat: add ExternalProviderId column and make Email nullable

EF migration adds ExternalProviderId with filtered unique index,
makes Email nullable with filtered unique index for OIDC user support.
```

---

## Task 3: Repository — ExternalProviderId Lookup with Email Migration

**Files:**
- Modify: `src/SEBT.Portal.Core/Repositories/IUserRepository.cs`
- Modify: `src/SEBT.Portal.Infrastructure/Repositories/DatabaseUserRepository.cs`
- Test: `test/SEBT.Portal.Tests/Unit/Repositories/DatabaseUserRepositoryTests.cs` (or integration test)

The key design here: `GetOrCreateUserByExternalIdAsync` accepts an optional `email` parameter. If no user is found by `externalProviderId`, it falls back to an email-based lookup and **adopts** that record by setting its `ExternalProviderId`. This migrates pre-existing email-only users to the new identity model on their next login.

- [ ] **Step 1: Write the failing tests**

```csharp
// In a new or existing repository test file:

[Fact]
public async Task GetOrCreateUserByExternalIdAsync_WhenUserDoesNotExist_CreatesMinimalRecord()
{
    var externalId = "pingone-sub-12345";

    var (user, isNew) = await repository.GetOrCreateUserByExternalIdAsync(externalId);

    Assert.True(isNew);
    Assert.Equal(externalId, user.ExternalProviderId);
    Assert.Null(user.Email);
    Assert.Equal(UserIalLevel.None, user.IalLevel);
    Assert.True(user.Id > 0);
}

[Fact]
public async Task GetOrCreateUserByExternalIdAsync_WhenUserExists_ReturnsExisting()
{
    var externalId = "pingone-sub-12345";
    await repository.GetOrCreateUserByExternalIdAsync(externalId);

    var (user, isNew) = await repository.GetOrCreateUserByExternalIdAsync(externalId);

    Assert.False(isNew);
    Assert.Equal(externalId, user.ExternalProviderId);
}

[Fact]
public async Task GetOrCreateUserByExternalIdAsync_WhenLegacyEmailUserExists_AdoptsRecord()
{
    // Pre-existing user created by the old email-based flow (no ExternalProviderId)
    var legacyUser = new User { Email = "user@example.com", IalLevel = UserIalLevel.IAL1 };
    await repository.CreateUserAsync(legacyUser);

    var externalId = "pingone-sub-12345";
    var (user, isNew) = await repository.GetOrCreateUserByExternalIdAsync(
        externalId, email: "user@example.com");

    // Should adopt the existing record, not create a new one
    Assert.False(isNew);
    Assert.Equal(externalId, user.ExternalProviderId);
    Assert.Null(user.Email); // email cleared — OIDC users derive it from IdP claims
    Assert.Equal(legacyUser.Id, user.Id); // same DB record
}

[Fact]
public async Task GetOrCreateUserByExternalIdAsync_WhenNoEmailProvided_DoesNotFallBackToEmail()
{
    // Pre-existing email user, but no email hint provided
    var legacyUser = new User { Email = "user@example.com" };
    await repository.CreateUserAsync(legacyUser);

    var externalId = "pingone-sub-12345";
    var (user, isNew) = await repository.GetOrCreateUserByExternalIdAsync(externalId);

    // Should create a new record, not find the legacy one
    Assert.True(isNew);
    Assert.NotEqual(legacyUser.Id, user.Id);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~GetOrCreateUserByExternalIdAsync"`
Expected: Compilation error — method doesn't exist yet.

- [ ] **Step 3: Add interface methods**

```csharp
// In IUserRepository.cs, add:

/// <summary>
/// Gets or creates a user by their external identity provider subject ID.
/// Used for OIDC-authenticated users whose identity is anchored by the IdP's sub claim.
/// Creates a minimal record (no email, no IAL) — those attributes come from IdP claims.
///
/// When <paramref name="email"/> is provided and no user exists for the given
/// <paramref name="externalProviderId"/>, falls back to an email-based lookup and
/// adopts that record by setting its ExternalProviderId. This migrates pre-existing
/// email-only user records to the new sub-based identity model.
/// TODO: Remove email fallback migration once all existing users have logged in
/// under the new flow.
/// </summary>
Task<(User user, bool isNewUser)> GetOrCreateUserByExternalIdAsync(
    string externalProviderId,
    string? email = null,
    CancellationToken cancellationToken = default);

/// <summary>
/// Retrieves a user by their external identity provider subject ID.
/// </summary>
Task<User?> GetUserByExternalIdAsync(
    string externalProviderId, CancellationToken cancellationToken = default);
```

- [ ] **Step 4: Implement in DatabaseUserRepository**

```csharp
public async Task<User?> GetUserByExternalIdAsync(
    string externalProviderId, CancellationToken cancellationToken = default)
{
    if (string.IsNullOrWhiteSpace(externalProviderId))
    {
        return null;
    }

    var entity = await dbContext.Users
        .AsNoTracking()
        .FirstOrDefaultAsync(u => u.ExternalProviderId == externalProviderId, cancellationToken);

    return entity == null ? null : MapToDomainModel(entity);
}

public async Task<(User user, bool isNewUser)> GetOrCreateUserByExternalIdAsync(
    string externalProviderId,
    string? email = null,
    CancellationToken cancellationToken = default)
{
    if (string.IsNullOrWhiteSpace(externalProviderId))
    {
        throw new ArgumentException(
            "External provider ID cannot be null or empty.", nameof(externalProviderId));
    }

    // Primary lookup: by ExternalProviderId (the steady-state path)
    var entity = await dbContext.Users
        .FirstOrDefaultAsync(u => u.ExternalProviderId == externalProviderId, cancellationToken);

    if (entity != null)
    {
        return (MapToDomainModel(entity), false);
    }

    // Migration fallback: if an email hint is provided, check for a legacy
    // email-only record and adopt it by setting ExternalProviderId.
    // TODO: Remove this fallback once all existing users have logged in
    // under the new sub-based identity flow.
    if (!string.IsNullOrWhiteSpace(email))
    {
        var normalizedEmail = NormalizeEmail(email);
        var legacyEntity = await dbContext.Users
            .FirstOrDefaultAsync(
                u => u.Email == normalizedEmail && u.ExternalProviderId == null,
                cancellationToken);

        if (legacyEntity != null)
        {
            legacyEntity.ExternalProviderId = externalProviderId;
            legacyEntity.Email = null; // OIDC users derive email from IdP claims, not DB
            legacyEntity.UpdatedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            return (MapToDomainModel(legacyEntity), false);
        }
    }

    // No existing record found — create a new minimal one
    var newEntity = new UserEntity
    {
        ExternalProviderId = externalProviderId,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    dbContext.Users.Add(newEntity);

    try
    {
        await dbContext.SaveChangesAsync(cancellationToken);
    }
    catch (DbUpdateException ex)
    {
        if (ex.InnerException?.Message.Contains("UNIQUE") == true ||
            ex.InnerException?.Message.Contains("duplicate key") == true)
        {
            entity = await dbContext.Users
                .FirstOrDefaultAsync(
                    u => u.ExternalProviderId == externalProviderId, cancellationToken);

            if (entity != null)
            {
                return (MapToDomainModel(entity), false);
            }
        }
        throw;
    }

    return (MapToDomainModel(newEntity), true);
}
```

- [ ] **Step 5: Update MapToDomainModel and MapToEntity**

Add `ExternalProviderId` to both mapping methods in `DatabaseUserRepository`:

```csharp
// In MapToDomainModel:
ExternalProviderId = entity.ExternalProviderId,

// In MapToEntity:
ExternalProviderId = user.ExternalProviderId,
```

- [ ] **Step 6: Run tests**

Run: `dotnet test --filter "FullyQualifiedName~GetOrCreateUserByExternalIdAsync"`
Expected: PASS

- [ ] **Step 7: Commit**

```
feat: add ExternalProviderId user lookup with email migration fallback

OIDC users are looked up by IdP sub claim (ExternalProviderId).
When no match exists but a legacy email-only record does, the record
is adopted by setting ExternalProviderId — migrating it in-flight.
The email fallback is marked for removal in a future release.
```

---

## Task 4: JwtTokenService — Claims-Based Email and IAL

This is the core bug fix. `GenerateToken` currently hard-codes email from `user.Email` and IAL from `user.IalLevel`. For OIDC users, these need to come from `additionalClaims`.

**Files:**
- Modify: `src/SEBT.Portal.Infrastructure/Services/JwtTokenService.cs`
- Test: `test/SEBT.Portal.Tests/Unit/Services/JwtTokenServiceTests.cs`

- [ ] **Step 1: Write failing test — additionalClaims email overrides user.Email**

```csharp
[Fact]
public void GenerateToken_WhenAdditionalClaimsContainEmail_UsesClaimsEmail()
{
    var user = new User { Id = 1, Email = null }; // OIDC user, no stored email
    var claims = new Dictionary<string, string>
    {
        ["email"] = "oidc-user@example.com",
        ["sub"] = "pingone-sub-123"
    };

    var token = _service.GenerateToken(user, claims);

    var handler = new JwtSecurityTokenHandler();
    var jwt = handler.ReadJwtToken(token);
    var emailClaim = jwt.Claims.First(c => c.Type == "email");
    Assert.Equal("oidc-user@example.com", emailClaim.Value);
}
```

- [ ] **Step 2: Write failing test — additionalClaims IAL overrides user.IalLevel**

```csharp
[Fact]
public void GenerateToken_WhenAdditionalClaimsContainIal_UsesClaimsIal()
{
    var user = new User { Id = 1, IalLevel = UserIalLevel.None }; // OIDC user, no stored IAL
    var claims = new Dictionary<string, string>
    {
        ["email"] = "user@example.com",
        [JwtClaimTypes.Ial] = "1plus"
    };

    var token = _service.GenerateToken(user, claims);

    var handler = new JwtSecurityTokenHandler();
    var jwt = handler.ReadJwtToken(token);
    var ialClaim = jwt.Claims.First(c => c.Type == JwtClaimTypes.Ial);
    Assert.Equal("1plus", ialClaim.Value);
}
```

- [ ] **Step 3: Write test — OTP user still uses user object values (no additionalClaims)**

```csharp
[Fact]
public void GenerateToken_WhenNoAdditionalClaims_UsesUserProperties()
{
    var user = new User
    {
        Id = 1,
        Email = "otp-user@example.com",
        IalLevel = UserIalLevel.IAL1plus
    };

    var token = _service.GenerateToken(user);

    var handler = new JwtSecurityTokenHandler();
    var jwt = handler.ReadJwtToken(token);
    Assert.Equal("otp-user@example.com", jwt.Claims.First(c => c.Type == "email").Value);
    Assert.Equal("1plus", jwt.Claims.First(c => c.Type == JwtClaimTypes.Ial).Value);
}
```

- [ ] **Step 4: Run tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~JwtTokenServiceTests"`
Expected: First two tests FAIL (email and IAL still come from user object, ignoring claims).

- [ ] **Step 5: Refactor GenerateToken to prefer additionalClaims**

The key change: derive email, sub, IAL, and IdProofing claims from additionalClaims when available, falling back to user properties.

```csharp
public string GenerateToken(User user, IReadOnlyDictionary<string, string>? additionalClaims = null)
{
    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SecretKey));
    var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

    var now = DateTimeOffset.UtcNow;
    var unixTimeSeconds = now.ToUnixTimeSeconds();

    // For OIDC users, email and sub come from IdP claims (via additionalClaims).
    // For OTP users, they come from user.Email (the DB value).
    var email = additionalClaims?.GetValueOrDefault("email") ?? user.Email ?? "";
    var sub = additionalClaims?.GetValueOrDefault("sub") ?? email;

    // For OIDC users, IAL comes from IdP claims carried in additionalClaims.
    // For OTP users, it comes from user.IalLevel (the DB value).
    var ialValue = additionalClaims?.GetValueOrDefault(JwtClaimTypes.Ial)
        ?? user.IalLevel switch
        {
            UserIalLevel.IAL1 => "1",
            UserIalLevel.IAL1plus => "1plus",
            UserIalLevel.IAL2 => "2",
            _ => "0"
        };

    var idProofingStatusValue = additionalClaims?.GetValueOrDefault(JwtClaimTypes.IdProofingStatus)
        ?? ((int)user.IdProofingStatus).ToString();

    var claims = new List<Claim>
    {
        new Claim(ClaimTypes.Email, email),
        new Claim(JwtRegisteredClaimNames.Sub, sub),
        new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        new Claim(JwtRegisteredClaimNames.Iat, unixTimeSeconds.ToString(), ClaimValueTypes.Integer64),
        new Claim(JwtRegisteredClaimNames.Nbf, unixTimeSeconds.ToString(), ClaimValueTypes.Integer64),
        new Claim(JwtRegisteredClaimNames.Aud, "SEBT.Portal.Web"),
        new Claim(JwtRegisteredClaimNames.Iss, "SEBT.Portal.Api"),
        new Claim(JwtClaimTypes.IdProofingStatus, idProofingStatusValue, ClaimValueTypes.Integer32),
        new Claim(JwtClaimTypes.Ial, ialValue)
    };

    // Add optional ID proofing claims — prefer additionalClaims, fall back to user properties
    var sessionId = additionalClaims?.GetValueOrDefault(JwtClaimTypes.IdProofingSessionId)
        ?? user.IdProofingSessionId;
    if (!string.IsNullOrWhiteSpace(sessionId))
    {
        claims.Add(new Claim(JwtClaimTypes.IdProofingSessionId, sessionId));
    }

    var completedAtStr = additionalClaims?.GetValueOrDefault(JwtClaimTypes.IdProofingCompletedAt);
    if (completedAtStr != null)
    {
        claims.Add(new Claim(JwtClaimTypes.IdProofingCompletedAt, completedAtStr, ClaimValueTypes.Integer64));
    }
    else if (user.IdProofingCompletedAt.HasValue)
    {
        var completedAtOffset = new DateTimeOffset(user.IdProofingCompletedAt.Value, TimeSpan.Zero);
        claims.Add(new Claim(JwtClaimTypes.IdProofingCompletedAt,
            completedAtOffset.ToUnixTimeSeconds().ToString(),
            ClaimValueTypes.Integer64));
    }

    // Compute expiration from completedAt + validity, regardless of source
    var expiresAtStr = additionalClaims?.GetValueOrDefault(JwtClaimTypes.IdProofingExpiresAt);
    if (expiresAtStr != null)
    {
        claims.Add(new Claim(JwtClaimTypes.IdProofingExpiresAt, expiresAtStr, ClaimValueTypes.Integer64));
    }
    else if (user.IdProofingCompletedAt.HasValue)
    {
        var expiresAt = user.IdProofingCompletedAt.Value.AddDays(_validitySettings.ValidityDays);
        var expiresAtOffset = new DateTimeOffset(expiresAt, TimeSpan.Zero);
        claims.Add(new Claim(JwtClaimTypes.IdProofingExpiresAt,
            expiresAtOffset.ToUnixTimeSeconds().ToString(),
            ClaimValueTypes.Integer64));
    }

    // Add remaining additionalClaims that weren't already set above
    var reservedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
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

    if (additionalClaims != null)
    {
        foreach (var (name, value) in additionalClaims)
        {
            if (!string.IsNullOrEmpty(name) &&
                value != null &&
                !reservedNames.Contains(name) &&
                !claims.Select(c => c.Type).Contains(name))
            {
                claims.Add(new Claim(name, value));
            }
        }
    }

    var token = new JwtSecurityToken(
        issuer: _settings.Issuer,
        audience: _settings.Audience,
        claims: claims,
        expires: DateTime.UtcNow.AddMinutes(_settings.ExpirationMinutes),
        signingCredentials: credentials
    );

    return new JwtSecurityTokenHandler().WriteToken(token);
}
```

- [ ] **Step 6: Run tests**

Run: `dotnet test --filter "FullyQualifiedName~JwtTokenServiceTests"`
Expected: All PASS

- [ ] **Step 7: Commit**

```
fix: JwtTokenService prefers additionalClaims over DB for email/IAL

For OIDC users, email and IAL come from IdP claims (passed via
additionalClaims). For OTP users, the user object (DB) values are
used as before. This is the core fix for stale IAL on OIDC logins.
```

---

## Task 5: OidcController.CompleteLogin — Use ExternalProviderId, Stop Persisting IAL

**Files:**
- Modify: `src/SEBT.Portal.Api/Controllers/Auth/OidcController.cs`
- Test: `test/SEBT.Portal.Tests/Unit/Controllers/OidcControllerTests.cs`

- [ ] **Step 1: Write failing test — CompleteLogin looks up by sub, passes email for migration**

```csharp
[Fact]
public async Task CompleteLogin_WhenValidOidcLogin_LooksUpUserByExternalProviderId()
{
    SetupPreAuthSession();
    const string signingKey = "complete-login-signing-key-at-least-32-characters-long";
    _config["Oidc:CompleteLoginSigningKey"].Returns(signingKey);

    var callbackToken = CreateCallbackTokenWithClaims(signingKey,
        new Claim("email", "user@example.com"),
        new Claim("sub", "pingone-sub-12345"));
    var body = new CompleteLoginRequest(CoStateKey, callbackToken);

    var user = new User { Id = 1, ExternalProviderId = "pingone-sub-12345" };
    _userRepository.GetOrCreateUserByExternalIdAsync(
            "pingone-sub-12345", "user@example.com", Arg.Any<CancellationToken>())
        .Returns((user, false));
    _jwtService.GenerateToken(Arg.Any<User>(), Arg.Any<IReadOnlyDictionary<string, string>?>())
        .Returns("portal-jwt");

    await _controller.CompleteLogin(body, CancellationToken.None);

    // Should use ExternalProviderId lookup with email hint, NOT email-only lookup
    await _userRepository.Received(1)
        .GetOrCreateUserByExternalIdAsync(
            "pingone-sub-12345", "user@example.com", Arg.Any<CancellationToken>());
    await _userRepository.DidNotReceive()
        .GetOrCreateUserAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
}
```

- [ ] **Step 2: Write failing test — CompleteLogin does NOT persist IAL to DB**

```csharp
[Fact]
public async Task CompleteLogin_WhenOidcVerificationPresent_DoesNotPersistIalToDb()
{
    const string signingKey = "complete-login-signing-key-at-least-32-characters-long";
    _config["Oidc:CompleteLoginSigningKey"].Returns(signingKey);

    var controller = CreateControllerWithTranslator();
    SetupPreAuthSessionForController(controller);

    var callbackToken = CreateCallbackTokenWithClaims(signingKey,
        new Claim("email", "user@example.com"),
        new Claim("sub", "pingone-sub-12345"),
        new Claim("socureIdVerificationLevel", "1.5"),
        new Claim("socureIdVerificationDate", DateTime.UtcNow.AddDays(-30).ToString("o")));
    var body = new CompleteLoginRequest(CoStateKey, callbackToken);

    var user = new User { Id = 1, ExternalProviderId = "pingone-sub-12345" };
    _userRepository.GetOrCreateUserByExternalIdAsync(
            "pingone-sub-12345", "user@example.com", Arg.Any<CancellationToken>())
        .Returns((user, false));
    _jwtService.GenerateToken(Arg.Any<User>(), Arg.Any<IReadOnlyDictionary<string, string>?>())
        .Returns("portal-jwt");

    await controller.CompleteLogin(body, CancellationToken.None);

    // IAL reconciliation should NOT trigger a DB update for OIDC users
    await _userRepository.DidNotReceive()
        .UpdateUserAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
}
```

- [ ] **Step 3: Write test — IAL from verification claims is passed to GenerateToken via additionalClaims**

```csharp
[Fact]
public async Task CompleteLogin_WhenOidcVerificationPresent_PassesIalInAdditionalClaims()
{
    const string signingKey = "complete-login-signing-key-at-least-32-characters-long";
    _config["Oidc:CompleteLoginSigningKey"].Returns(signingKey);

    var controller = CreateControllerWithTranslator();
    SetupPreAuthSessionForController(controller);

    var callbackToken = CreateCallbackTokenWithClaims(signingKey,
        new Claim("email", "user@example.com"),
        new Claim("sub", "pingone-sub-12345"),
        new Claim("socureIdVerificationLevel", "1.5"),
        new Claim("socureIdVerificationDate", DateTime.UtcNow.AddDays(-30).ToString("o")));
    var body = new CompleteLoginRequest(CoStateKey, callbackToken);

    var user = new User { Id = 1, ExternalProviderId = "pingone-sub-12345" };
    _userRepository.GetOrCreateUserByExternalIdAsync(
            "pingone-sub-12345", "user@example.com", Arg.Any<CancellationToken>())
        .Returns((user, false));
    _jwtService.GenerateToken(Arg.Any<User>(), Arg.Any<IReadOnlyDictionary<string, string>?>())
        .Returns("portal-jwt");

    await controller.CompleteLogin(body, CancellationToken.None);

    // Verify IAL was passed in additionalClaims to GenerateToken
    _jwtService.Received(1).GenerateToken(
        Arg.Any<User>(),
        Arg.Is<IReadOnlyDictionary<string, string>>(c =>
            c.ContainsKey(JwtClaimTypes.Ial) && c[JwtClaimTypes.Ial] == "1plus"));
}
```

- [ ] **Step 4: Run tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~OidcControllerTests"`
Expected: New tests FAIL — controller still uses email-based lookup and persists IAL.

- [ ] **Step 5: Refactor CompleteLogin non-step-up flow**

In `OidcController.CompleteLogin`, replace the non-step-up block (lines 469-496) with:

```csharp
else
{
    // Extract the IdP subject claim — this is the OIDC user's identity anchor.
    var subClaim = additionalClaims.GetValueOrDefault("sub");
    if (string.IsNullOrWhiteSpace(subClaim))
    {
        logger.LogWarning(
            "OIDC CompleteLogin: callback token missing sub claim (SessionId={SessionId})",
            sessionId);
        return BadRequest(new ErrorResponse("Callback token must contain a sub claim."));
    }

    // Pass email from IdP claims as a migration hint: if no user exists for
    // this sub but one exists for this email, adopt that legacy record.
    // TODO: Remove email parameter once all existing users have been migrated.
    var emailHint = additionalClaims.GetValueOrDefault("email");
    var (createdUser, _) = await userRepository.GetOrCreateUserByExternalIdAsync(
        subClaim, emailHint, cancellationToken);
    user = createdUser;

    // Reconcile IAL from OIDC verification claims. The IdP is the source of truth —
    // we derive IAL from claims and pass it through to the JWT, but do NOT persist
    // it to the DB. The DB only stores the identity anchor (ExternalProviderId).
    var verification = verificationClaimTranslator.Translate(additionalClaims);
    if (verification != null)
    {
        // Set IAL in additionalClaims for JwtTokenService to pick up
        additionalClaims[JwtClaimTypes.Ial] = verification.IalLevel switch
        {
            UserIalLevel.IAL1plus => "1plus",
            UserIalLevel.IAL2 => "2",
            UserIalLevel.IAL1 => "1",
            _ => "0"
        };
        additionalClaims[JwtClaimTypes.IdProofingStatus] =
            ((int)(verification.IsExpired ? IdProofingStatus.Expired : IdProofingStatus.Completed)).ToString();

        if (verification.VerifiedAt != default)
        {
            var verifiedAtOffset = new DateTimeOffset(verification.VerifiedAt, TimeSpan.Zero);
            additionalClaims[JwtClaimTypes.IdProofingCompletedAt] =
                verifiedAtOffset.ToUnixTimeSeconds().ToString();
        }

        logger.LogInformation(
            "OIDC verification claim reconciled: UserId {UserId}, IalLevel {IalLevel}, IsExpired {IsExpired}, VerifiedAt {VerifiedAt}, SessionId={SessionId}",
            user.Id, verification.IalLevel, verification.IsExpired, verification.VerifiedAt, sessionId);
    }
    else
    {
        // No verification claims from IdP — user is IAL1 (authenticated but not verified)
        additionalClaims[JwtClaimTypes.Ial] = "1";
    }
}
```

Note: `additionalClaims` needs to change from `var additionalClaims = new Dictionary<string, string>(...)` — it's already mutable. The IAL and IdProofing values are injected into the same dictionary that gets passed to `GenerateToken`.

- [ ] **Step 6: Update the step-up flow too**

The step-up flow also needs to use ExternalProviderId for lookup:

```csharp
if (session.IsStepUp)
{
    var subClaim = additionalClaims.GetValueOrDefault("sub");
    if (string.IsNullOrWhiteSpace(subClaim))
    {
        logger.LogWarning("Step-up complete-login: missing sub claim (SessionId={SessionId})", sessionId);
        return BadRequest(new ErrorResponse("Callback token must contain a sub claim."));
    }

    // Look up by ExternalProviderId for OIDC step-up
    var existingEntity = await userRepository.GetUserByExternalIdAsync(subClaim, cancellationToken);
    if (existingEntity == null)
    {
        logger.LogWarning(
            "Step-up complete-login: no existing portal user for sub claim; sign-in required first (SessionId={SessionId}).",
            sessionId);
        return BadRequest(new { error = "Step-up requires an existing session. Please sign in again." });
    }

    user = existingEntity;

    // Set step-up IAL in claims, not DB
    additionalClaims[JwtClaimTypes.Ial] = "1plus";
    additionalClaims[JwtClaimTypes.IdProofingStatus] = ((int)IdProofingStatus.Completed).ToString();
    additionalClaims[JwtClaimTypes.IdProofingCompletedAt] =
        new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds().ToString();

    logger.LogInformation(
        "OIDC step-up complete-login succeeded: UserId {UserId}, StateCode {StateCode}, IalLevel IAL1plus, SessionId={SessionId}",
        user.Id, SanitizeForLog(stateKey), sessionId);
}
```

Note: Step-up needs a new `GetUserByExternalIdAsync` (read-only) method added to the repository interface. Add it alongside `GetOrCreateUserByExternalIdAsync`.

- [ ] **Step 7: Remove the GetEmailFromClaims usage for user lookup**

The `GetEmailFromClaims` helper is still useful for extracting email for logging/claims, but it should no longer be used as the user identity for DB lookup. The `sub` claim is now used directly.

- [ ] **Step 8: Run all tests**

Run: `dotnet test --filter "FullyQualifiedName~OidcControllerTests"`
Expected: All PASS (update existing tests that assumed email-based lookup)

- [ ] **Step 9: Commit**

```
fix: OidcController uses sub claim for user lookup, stops persisting IAL

OIDC users are now looked up by ExternalProviderId (IdP sub claim).
IAL is derived from IdP verification claims and passed through to
the JWT via additionalClaims — never persisted to the DB.
This fixes stale IAL on OIDC re-login and token refresh.
```

---

## Task 6: RefreshTokenCommandHandler — OIDC-Aware Refresh

**Files:**
- Modify: `src/SEBT.Portal.UseCases/Auth/RefreshToken/RefreshTokenCommandHandler.cs`
- Modify: `src/SEBT.Portal.UseCases/Auth/RefreshToken/RefreshTokenCommand.cs`
- Modify: `src/SEBT.Portal.Api/Controllers/Auth/AuthController.cs`
- Test: `test/SEBT.Portal.Tests/Unit/UseCases/Auth/RefreshTokenCommandHandlerTests.cs`

- [ ] **Step 1: Write failing test — OIDC user refresh preserves JWT IAL, ignores DB**

```csharp
[Fact]
public async Task Handle_WhenOidcUser_PreservesIalFromExistingJwtClaims()
{
    var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
    {
        new Claim("email", "user@example.com"),
        new Claim("sub", "pingone-sub-12345"),
        new Claim(JwtClaimTypes.Ial, "1plus"),
        new Claim(JwtClaimTypes.IdProofingStatus, "2"), // Completed
    }));

    var command = new RefreshTokenCommand
    {
        Email = "user@example.com",
        ExternalProviderId = "pingone-sub-12345",
        CurrentPrincipal = principal
    };

    // OIDC user has no IAL stored in DB
    var user = new User
    {
        Id = 1,
        ExternalProviderId = "pingone-sub-12345",
        IalLevel = UserIalLevel.None
    };

    userRepository.GetUserByExternalIdAsync("pingone-sub-12345", Arg.Any<CancellationToken>())
        .Returns(user);
    jwtTokenService.GenerateToken(Arg.Any<User>(), Arg.Any<IReadOnlyDictionary<string, string>>())
        .Returns("refreshed-jwt");

    var result = await handler.Handle(command);

    Assert.True(result.IsSuccess);
    // Verify that IAL from JWT claims (1plus) was passed through, not DB (None)
    jwtTokenService.Received(1).GenerateToken(
        Arg.Any<User>(),
        Arg.Is<IReadOnlyDictionary<string, string>>(c =>
            c.ContainsKey(JwtClaimTypes.Ial) && c[JwtClaimTypes.Ial] == "1plus"));
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~Handle_WhenOidcUser_PreservesIalFromExistingJwtClaims"`
Expected: FAIL — ExternalProviderId doesn't exist on RefreshTokenCommand yet.

- [ ] **Step 3: Add ExternalProviderId to RefreshTokenCommand**

```csharp
// In RefreshTokenCommand.cs, add:

/// <summary>
/// The external provider ID (IdP sub claim) for OIDC users. Null for OTP users.
/// When present, the handler looks up the user by ExternalProviderId instead of email
/// and preserves IAL from JWT claims instead of reading from DB.
/// </summary>
public string? ExternalProviderId { get; set; }
```

- [ ] **Step 4: Update AuthController.RefreshToken to extract ExternalProviderId**

```csharp
// In AuthController.RefreshToken, update the command construction:
var externalProviderId = User.FindFirst("sub")?.Value;
// If the JWT sub differs from the email, it's an OIDC user whose sub is the IdP subject
var email = GetUserEmail();
var isOidcUser = externalProviderId != null && externalProviderId != email;

var command = new RefreshTokenCommand
{
    Email = email,
    ExternalProviderId = isOidcUser ? externalProviderId : null,
    CurrentPrincipal = User
};
```

- [ ] **Step 5: Update RefreshTokenCommandHandler for OIDC users**

```csharp
public async Task<Result<string>> Handle(RefreshTokenCommand command, CancellationToken cancellationToken = default)
{
    var validationResult = await validator.Validate(command, cancellationToken);
    if (validationResult is ValidationFailedResult validationFailedResult)
    {
        logger.LogWarning("Token refresh validation failed: {Errors}",
            string.Join(", ", validationFailedResult.Errors.Select(e => $"{e.Key}: {e.Message}")));
        return Result<string>.ValidationFailed(validationFailedResult.Errors);
    }

    try
    {
        User? user;
        if (!string.IsNullOrEmpty(command.ExternalProviderId))
        {
            // OIDC user — look up by IdP subject, not email
            user = await userRepository.GetUserByExternalIdAsync(
                command.ExternalProviderId, cancellationToken);
        }
        else
        {
            // OTP user — look up by email (existing behavior)
            user = await userRepository.GetUserByEmailAsync(command.Email, cancellationToken);
        }

        if (user == null)
        {
            logger.LogWarning("Token refresh attempted for non-existent user");
            return Result<string>.PreconditionFailed(
                PreconditionFailedReason.NotFound, "User not found.");
        }

        // Pass all existing JWT claims through — for OIDC users, this preserves
        // IAL and other IdP-derived claims. For OTP users, GenerateToken will
        // prefer user object values (from DB) over these claims.
        var additionalClaims = command.CurrentPrincipal.Claims
            .DistinctBy(c => c.Type)
            .ToDictionary(c => c.Type, c => c.Value);
        var token = jwtTokenService.GenerateToken(user, additionalClaims);

        var maskedPhone = PiiMasker.MaskPhone(
            command.CurrentPrincipal.FindFirst("phone")?.Value
            ?? command.CurrentPrincipal.FindFirst("phone_number")?.Value);
        logger.LogInformation(
            "Token refreshed successfully for UserId {UserId}, Phone={MaskedPhone}",
            user.Id, maskedPhone);

        return Result<string>.Success(token);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error refreshing token");
        return Result<string>.DependencyFailed(
            DependencyFailedReason.ConnectionFailed,
            "An error occurred while refreshing the authentication token.");
    }
}
```

- [ ] **Step 6: Add GetUserByExternalIdAsync to IUserRepository**

```csharp
/// <summary>
/// Retrieves a user by their external identity provider subject ID.
/// </summary>
Task<User?> GetUserByExternalIdAsync(string externalProviderId, CancellationToken cancellationToken = default);
```

- [ ] **Step 7: Implement GetUserByExternalIdAsync in DatabaseUserRepository**

```csharp
public async Task<User?> GetUserByExternalIdAsync(
    string externalProviderId, CancellationToken cancellationToken = default)
{
    if (string.IsNullOrWhiteSpace(externalProviderId))
    {
        return null;
    }

    var entity = await dbContext.Users
        .AsNoTracking()
        .FirstOrDefaultAsync(u => u.ExternalProviderId == externalProviderId, cancellationToken);

    return entity == null ? null : MapToDomainModel(entity);
}
```

- [ ] **Step 8: Run all refresh tests**

Run: `dotnet test --filter "FullyQualifiedName~RefreshTokenCommandHandlerTests"`
Expected: All PASS (including new OIDC test and existing OTP tests)

- [ ] **Step 9: Commit**

```
fix: token refresh for OIDC users preserves IAL from JWT claims

OIDC users are looked up by ExternalProviderId during refresh.
IAL and other IdP-derived claims are passed through from the
existing JWT, preventing stale DB values from overriding them.
```

---

## Task 7: Fix Compilation Errors from Nullable Email

Making `User.Email` nullable will cause compilation errors across the codebase wherever it's assumed non-null. Fix each caller.

**Files:**
- Modify: Multiple files (compiler will tell us which)

- [ ] **Step 1: Build and catalog all nullable warnings/errors**

Run: `dotnet build src/SEBT.Portal.Api/SEBT.Portal.Api.csproj 2>&1 | grep -i "CS8"` (nullable warnings)

Fix each one:
- `DatabaseUserRepository.UpdateUserAsync` — guard against null email (only validate for OTP users)
- `DatabaseUserRepository.GetOrCreateUserAsync` — still requires email (OTP path)
- `DataSeeder.MapToEntity` — handle nullable email
- `ValidateOtpCommandHandler` — OTP path always has email, assert non-null
- Any other callers

- [ ] **Step 2: Fix each file**

Handle case-by-case. The pattern is:
- OTP code paths: `Email` is always non-null (validated at entry)
- OIDC code paths: `Email` may be null (comes from claims, not DB)
- Use null-forgiving (`!`) only when the caller has already validated

- [ ] **Step 3: Run full test suite**

Run: `dotnet test`
Expected: All existing tests PASS, no nullable reference warnings in the error output.

- [ ] **Step 4: Commit**

```
fix: handle nullable User.Email across codebase

OTP code paths validate email at entry. OIDC paths derive email
from IdP claims. Updated all callers to handle the nullable change.
```

---

## Task 8: Update Existing Tests

Existing tests for OidcController and RefreshTokenCommandHandler make assumptions that will break. Update them.

**Files:**
- Modify: `test/SEBT.Portal.Tests/Unit/Controllers/OidcControllerTests.cs`
- Modify: `test/SEBT.Portal.Tests/Unit/UseCases/Auth/RefreshTokenCommandHandlerTests.cs`

- [ ] **Step 1: Update OidcControllerTests**

Key changes:
- Tests that call `_userRepository.GetOrCreateUserAsync` need to mock `GetOrCreateUserByExternalIdAsync` instead
- Tests need to include `sub` claim in callback tokens
- Verification claim tests should verify IAL goes into additionalClaims, not into UpdateUserAsync
- Step-up tests need to use `GetUserByExternalIdAsync` instead of `GetUserByEmailAsync`

- [ ] **Step 2: Update RefreshTokenCommandHandlerTests**

Key changes:
- Existing tests become "OTP user" tests (no ExternalProviderId)
- Add parallel "OIDC user" tests for each scenario

- [ ] **Step 3: Run full test suite**

Run: `pnpm api:test`
Expected: All PASS

- [ ] **Step 4: Commit**

```
test: update auth tests for ExternalProviderId-based OIDC lookup

Existing tests now cover OTP path explicitly. New tests cover
OIDC path with ExternalProviderId lookup and claims-based IAL.
```

---

## Task 9: Final Verification

- [ ] **Step 1: Run full backend test suite**

Run: `pnpm api:test`
Expected: All PASS

- [ ] **Step 2: Run frontend tests**

Run: `cd src/SEBT.Portal.Web && pnpm test`
Expected: All PASS (frontend reads IAL from JWT claims via the status endpoint — no changes needed)

- [ ] **Step 3: Run lint**

Run: `cd src/SEBT.Portal.Web && pnpm lint`
Expected: Clean

- [ ] **Step 4: Manual smoke test (if local env available)**

1. Start the stack: `docker compose up -d && pnpm dev`
2. Log in via CO OIDC → verify JWT contains correct IAL from IdP
3. Refresh the page (triggers token refresh) → verify IAL is preserved
4. Log in via DC OTP → verify existing flow still works
5. Check the Users table → CO user should have ExternalProviderId, no email; DC user should have email, no ExternalProviderId

- [ ] **Step 5: Commit any remaining fixes**
