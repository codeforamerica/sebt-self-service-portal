# Colorado CBMS Household Response Cache — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Eliminate user-visible CBMS latency for the Colorado portal by introducing a cache-aside, stale-while-revalidate (SWR), write-through cache layer over the slow `get-account-details` endpoint.

**Architecture:** A new internal `ICbmsHouseholdCache` (CO plugin) wraps every `get-account-details` call with HybridCache (L1 in-memory + L2 Redis). All three Colorado services (`ColoradoSummerEbtCaseService`, `ColoradoAddressUpdateService`, `ColoradoCardReplacementService`) collapse onto it. `ColoradoAddressUpdateService` does write-through after PATCH; `ColoradoCardReplacementService` is read-only against the cache. A new `IHMACSHA256Hasher` primitive in the state-connector contracts package provides PII-safe key hashing reused from main-portal config. The cache itself is a process-wide singleton owned by a small static `PluginCache` class that constructs it via `ActivatorUtilities.CreateInstance` against the host service provider.

**Tech Stack:** .NET 10, `Microsoft.Extensions.Caching.Hybrid` (Redis L2), `Microsoft.Extensions.DependencyInjection`, xUnit, NSubstitute, Bogus.

**Spec:** `docs/superpowers/specs/2026-04-30-co-cbms-household-cache-design.md`
**ADR:** `docs/adr/0004-cbms-response-caching.md` (already merged via PR #31)

---

## Repos and Worktrees

This work spans three repos. Worktrees on the same branch name (`feature/co-cbms-household-cache`) already exist in each:

| Repo | Worktree path | Phase |
|---|---|---|
| `sebt-self-service-portal-state-connector` | `.worktrees/feature/co-cbms-household-cache/` | Phase A (contracts) |
| `sebt-self-service-portal` | `.worktrees/feature/co-cbms-household-cache/` | Phase B (main portal HMAC impl) |
| `sebt-self-service-portal-co-connector` | `.worktrees/feature/co-cbms-household-cache/` | Phases C–F (cache + plugin wiring) |

> **Critical:** Use `git -C <absolute-worktree-path>` for git operations — shell `cwd` does not reliably reflect the worktree across multiple Bash calls. Run `dotnet` and `gh` commands with absolute paths or `cd` into the specific worktree at the start of a step.

---

## File Map

### Phase A — `sebt-self-service-portal-state-connector`

| Action | File | Responsibility |
|---|---|---|
| Create | `src/SEBT.Portal.StatesPlugins.Interfaces/Services/IHMACSHA256Hasher.cs` | Domain-agnostic HMAC-SHA256 hasher contract |
| Create | `src/SEBT.Portal.StatesPlugins.Interfaces.Tests/Services/IHMACSHA256HasherContractTests.cs` | Reflection-only contract shape test |

### Phase B — `sebt-self-service-portal`

| Action | File | Responsibility |
|---|---|---|
| Create | `src/SEBT.Portal.Infrastructure/Services/HMACSHA256Hasher.cs` | `IHMACSHA256Hasher` implementation, reusing `IdentifierHasher:SecretKey` |
| Create | `test/SEBT.Portal.Tests/Unit/Services/HMACSHA256HasherTests.cs` | Behavior tests |
| Modify | `src/SEBT.Portal.Infrastructure/Dependencies.cs` | Register `IHMACSHA256Hasher` as singleton |
| Modify | `src/SEBT.Portal.Infrastructure/Services/IdentifierHasher.cs` | (Optional, separate commit) Refactor to delegate to `IHMACSHA256Hasher` |
| Modify | `test/SEBT.Portal.Tests/Unit/Services/IdentifierHasherTests.cs` | (Optional) Adjust to confirm delegation behavior preserved |

### Phase C, D, E, F — `sebt-self-service-portal-co-connector`

| Action | File | Responsibility |
|---|---|---|
| Create | `src/SEBT.Portal.StatePlugins.CO/Cbms/Cache/CbmsHouseholdCacheOptions.cs` | Config-bound options (TTLs, timeouts) |
| Create | `src/SEBT.Portal.StatePlugins.CO/Cbms/Cache/CbmsHouseholdCacheEnvelope.cs` | Cached payload wrapper with soft/hard expiry |
| Create | `src/SEBT.Portal.StatePlugins.CO/Cbms/Cache/ICbmsHouseholdCache.cs` | Internal cache contract (Get/Set/Invalidate) |
| Create | `src/SEBT.Portal.StatePlugins.CO/Cbms/Cache/CbmsHouseholdCache.cs` | Cache implementation |
| Create | `src/SEBT.Portal.StatePlugins.CO/Cbms/Cache/CbmsFetchAccountDetailsDelegate.cs` | Named delegate for the CBMS fetch function |
| Create | `src/SEBT.Portal.StatePlugins.CO/Cbms/Cache/PluginCache.cs` | Static lazy singleton holder + test override |
| Modify | `src/SEBT.Portal.StatePlugins.CO/ColoradoCbmsServiceBase.cs` | Accept `IServiceProvider`, expose `HouseholdCache`, retain existing client cache |
| Modify | `src/SEBT.Portal.StatePlugins.CO/ColoradoSummerEbtCaseService.cs` | Read via `HouseholdCache` |
| Modify | `src/SEBT.Portal.StatePlugins.CO/ColoradoAddressUpdateService.cs` | Inherit from base; read via cache; write-through on PATCH success |
| Modify | `src/SEBT.Portal.StatePlugins.CO/ColoradoCardReplacementService.cs` | Inherit from base; read via cache (no write-through) |
| Modify | `src/SEBT.Portal.StatePlugins.CO/SEBT.Portal.StatePlugins.CO.csproj` | Bump `SEBT.Portal.StatesPlugins.Interfaces` package version |
| Create | `src/SEBT.Portal.StatePlugins.CO.Tests/Cbms/Cache/CbmsHouseholdCacheReadTests.cs` | Read-path unit tests |
| Create | `src/SEBT.Portal.StatePlugins.CO.Tests/Cbms/Cache/CbmsHouseholdCacheWriteThroughTests.cs` | Write-through + tripwire tests |
| Create | `src/SEBT.Portal.StatePlugins.CO.Tests/Cbms/Cache/CbmsHouseholdCacheBackgroundRefreshTests.cs` | SWR + cancellation/timeout tests |
| Create | `src/SEBT.Portal.StatePlugins.CO.Tests/Cbms/Cache/CbmsHouseholdCacheStampedeTests.cs` | In-flight coalescing tests |
| Create | `src/SEBT.Portal.StatePlugins.CO.Tests/Cbms/Cache/CbmsHouseholdCacheKeyHashingTests.cs` | Key shape and PII verification |
| Create | `src/SEBT.Portal.StatePlugins.CO.Tests/Cbms/Cache/PluginCacheTests.cs` | Lazy singleton + override behavior |
| Modify | `src/SEBT.Portal.StatePlugins.CO.Tests/SEBT.Portal.StatePlugins.CO.Tests.csproj` | Reference newer state-connector package |
| Modify | `src/SEBT.Portal.StatePlugins.CO.Tests/ColoradoSummerEbtCaseServiceTests.cs` | Update to inject cache via `PluginCache.OverrideForTesting` |
| Modify | `src/SEBT.Portal.StatePlugins.CO.Tests/ColoradoAddressUpdateServiceTests.cs` | Same; add write-through assertions |
| Modify | `src/SEBT.Portal.StatePlugins.CO.Tests/ColoradoCardReplacementServiceTests.cs` | Same; assert NO write-through |

---

## Phase A — Contracts package: `IHMACSHA256Hasher`

> **Working directory:** `/Users/jblair/Projects/SEBT/sebt-self-service-portal-state-connector/.worktrees/feature/co-cbms-household-cache` (substitute your actual home path).
>
> Standard build command from CLAUDE.md: `dotnet build` (also generates the NuGet package to `~/nuget-store/`).

### Task A1: Add the `IHMACSHA256Hasher` interface

**Files:**
- Create: `src/SEBT.Portal.StatesPlugins.Interfaces/Services/IHMACSHA256Hasher.cs`
- Create: `src/SEBT.Portal.StatesPlugins.Interfaces.Tests/Services/IHMACSHA256HasherContractTests.cs`

- [ ] **Step 1 — Write the failing contract test**

The contracts package has a tradition of contract-shape tests using reflection (see `EnumContractTests.cs`, `ModelContractTests.cs`). Add the new test before the interface exists.

```csharp
// src/SEBT.Portal.StatesPlugins.Interfaces.Tests/Services/IHMACSHA256HasherContractTests.cs
using System.Reflection;
using SEBT.Portal.StatesPlugins.Interfaces.Services;

namespace SEBT.Portal.StatesPlugins.Interfaces.Tests.Services;

public class IHMACSHA256HasherContractTests
{
    [Fact]
    public void Interface_exists_in_Services_namespace()
    {
        var t = typeof(IHMACSHA256Hasher);
        Assert.Equal("SEBT.Portal.StatesPlugins.Interfaces.Services", t.Namespace);
        Assert.True(t.IsInterface);
    }

    [Fact]
    public void Hash_method_takes_string_returns_string()
    {
        var method = typeof(IHMACSHA256Hasher).GetMethod(
            "Hash",
            BindingFlags.Public | BindingFlags.Instance,
            new[] { typeof(string) });

        Assert.NotNull(method);
        Assert.Equal(typeof(string), method!.ReturnType);
    }
}
```

- [ ] **Step 2 — Run the test; expect compile failure**

```bash
dotnet test --filter "FullyQualifiedName~IHMACSHA256HasherContractTests"
```

Expected: FAIL — type `SEBT.Portal.StatesPlugins.Interfaces.Services.IHMACSHA256Hasher` does not exist.

- [ ] **Step 3 — Create the interface**

```csharp
// src/SEBT.Portal.StatesPlugins.Interfaces/Services/IHMACSHA256Hasher.cs
namespace SEBT.Portal.StatesPlugins.Interfaces.Services;

/// <summary>
/// Domain-agnostic HMAC-SHA256 hasher. Implementations use a configured secret key
/// to produce deterministic hashes suitable for cache keys, lookup tokens, and
/// other consistent-hash use cases. Input is hashed as UTF-8 bytes; the output is
/// a lowercase hexadecimal string of the 32-byte HMAC.
/// </summary>
public interface IHMACSHA256Hasher
{
    /// <summary>
    /// Computes HMAC-SHA256 over the UTF-8 bytes of <paramref name="input"/> using the
    /// implementation's configured secret key. Returns the result as a 64-character
    /// lowercase hexadecimal string. Throws if <paramref name="input"/> is null.
    /// </summary>
    string Hash(string input);
}
```

- [ ] **Step 4 — Run the test; expect pass**

```bash
dotnet test --filter "FullyQualifiedName~IHMACSHA256HasherContractTests"
```

Expected: PASS.

- [ ] **Step 5 — Build the package**

```bash
dotnet build
```

Verify the build succeeds and outputs a new `.nupkg` (with `-dev-*` suffix per CLAUDE.md) to `~/nuget-store/`.

- [ ] **Step 6 — Commit**

```bash
git -C /Users/jblair/Projects/SEBT/sebt-self-service-portal-state-connector/.worktrees/feature/co-cbms-household-cache add \
  src/SEBT.Portal.StatesPlugins.Interfaces/Services/IHMACSHA256Hasher.cs \
  src/SEBT.Portal.StatesPlugins.Interfaces.Tests/Services/IHMACSHA256HasherContractTests.cs

git -C /Users/jblair/Projects/SEBT/sebt-self-service-portal-state-connector/.worktrees/feature/co-cbms-household-cache commit -m "feat: add IHMACSHA256Hasher contract for plugin-shared hashing"
```

### Task A2: Push and open draft PR (state-connector)

- [ ] **Step 1 — Push**

```bash
git -C /Users/jblair/Projects/SEBT/sebt-self-service-portal-state-connector/.worktrees/feature/co-cbms-household-cache push -u origin feature/co-cbms-household-cache
```

- [ ] **Step 2 — Open draft PR**

```bash
cd /Users/jblair/Projects/SEBT/sebt-self-service-portal-state-connector/.worktrees/feature/co-cbms-household-cache
gh pr create --draft --base main --head feature/co-cbms-household-cache \
  --title "Add IHMACSHA256Hasher primitive for plugin-shared hashing" \
  --body "Introduces a domain-agnostic HMAC-SHA256 hasher contract reusable across plugins. Companion to portal-side implementation (in main-portal feature/co-cbms-household-cache branch) and the CO CBMS cache work."
```

- [ ] **Step 3 — Wait for CI; resolve any contract test issues if surfaced**

Skim CI logs. If the contract tests pass and the package builds, this PR is ready for review.

---

## Phase B — Main portal: implement `IHMACSHA256Hasher`

> **Working directory:** `/Users/jblair/Projects/SEBT/sebt-self-service-portal/.worktrees/feature/co-cbms-household-cache`.

### Task B1: Bump the contracts package reference

The CO connector and main portal reference the state-connector NuGet via a wildcard `0.0.1-dev-*`. After Phase A's build produced a new `.nupkg`, restore should pick it up automatically.

- [ ] **Step 1 — Restore**

```bash
dotnet restore
```

- [ ] **Step 2 — Verify the new type is resolvable**

```bash
dotnet build src/SEBT.Portal.Infrastructure/SEBT.Portal.Infrastructure.csproj 2>&1 | head -5
```

Build should succeed.

### Task B2: TDD `HMACSHA256Hasher` implementation

**Files:**
- Test: `test/SEBT.Portal.Tests/Unit/Services/HMACSHA256HasherTests.cs`
- Create: `src/SEBT.Portal.Infrastructure/Services/HMACSHA256Hasher.cs`

- [ ] **Step 1 — Write failing tests**

```csharp
// test/SEBT.Portal.Tests/Unit/Services/HMACSHA256HasherTests.cs
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Infrastructure.Services;
using SEBT.Portal.StatesPlugins.Interfaces.Services;

namespace SEBT.Portal.Tests.Unit.Services;

public class HMACSHA256HasherTests
{
    private const string TestKey = "test-key-of-at-least-32-characters-long-aaaaa";

    private static IHMACSHA256Hasher CreateSut(string? key = null)
    {
        var settings = new IdentifierHasherSettings { SecretKey = key ?? TestKey };
        return new HMACSHA256Hasher(Options.Create(settings));
    }

    [Fact]
    public void Hash_returns_64_character_lowercase_hex()
    {
        var sut = CreateSut();

        var result = sut.Hash("hello");

        Assert.Equal(64, result.Length);
        Assert.Matches("^[0-9a-f]{64}$", result);
    }

    [Fact]
    public void Hash_is_deterministic_for_same_input_and_key()
    {
        var sut = CreateSut();

        Assert.Equal(sut.Hash("phone-1234567890"), sut.Hash("phone-1234567890"));
    }

    [Fact]
    public void Hash_differs_for_different_inputs()
    {
        var sut = CreateSut();

        Assert.NotEqual(sut.Hash("alpha"), sut.Hash("beta"));
    }

    [Fact]
    public void Hash_differs_for_different_keys_on_same_input()
    {
        var sutA = CreateSut("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
        var sutB = CreateSut("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");

        Assert.NotEqual(sutA.Hash("same-input"), sutB.Hash("same-input"));
    }

    [Fact]
    public void Hash_matches_reference_HMACSHA256_computation()
    {
        var sut = CreateSut();
        var expected = Convert.ToHexString(
            HMACSHA256.HashData(Encoding.UTF8.GetBytes(TestKey), Encoding.UTF8.GetBytes("known-input"))
        ).ToLowerInvariant();

        Assert.Equal(expected, sut.Hash("known-input"));
    }

    [Fact]
    public void Hash_throws_when_input_is_null()
    {
        var sut = CreateSut();

        Assert.Throws<ArgumentNullException>(() => sut.Hash(null!));
    }

    [Fact]
    public void Constructor_throws_when_key_missing()
    {
        var settings = new IdentifierHasherSettings { SecretKey = string.Empty };

        Assert.Throws<InvalidOperationException>(
            () => new HMACSHA256Hasher(Options.Create(settings)));
    }

    [Fact]
    public void Constructor_throws_when_key_too_short()
    {
        var settings = new IdentifierHasherSettings { SecretKey = "too-short" };

        Assert.Throws<InvalidOperationException>(
            () => new HMACSHA256Hasher(Options.Create(settings)));
    }
}
```

- [ ] **Step 2 — Run the tests; expect compile failure**

```bash
dotnet test test/SEBT.Portal.Tests/SEBT.Portal.Tests.csproj --filter "FullyQualifiedName~HMACSHA256HasherTests"
```

Expected: FAIL — type `HMACSHA256Hasher` not defined.

- [ ] **Step 3 — Implement `HMACSHA256Hasher`**

```csharp
// src/SEBT.Portal.Infrastructure/Services/HMACSHA256Hasher.cs
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.StatesPlugins.Interfaces.Services;

namespace SEBT.Portal.Infrastructure.Services;

/// <summary>
/// HMAC-SHA256 implementation backed by <see cref="IdentifierHasherSettings.SecretKey"/>.
/// Same key as <see cref="IdentifierHasher"/>; this is the underlying primitive that
/// IIdentifierHasher will eventually delegate to (see future cleanup in ADR-0004).
/// </summary>
public class HMACSHA256Hasher : IHMACSHA256Hasher
{
    private readonly byte[] _keyBytes;

    public HMACSHA256Hasher(IOptions<IdentifierHasherSettings> options)
    {
        var secretKey = options?.Value?.SecretKey
            ?? throw new InvalidOperationException("IdentifierHasher:SecretKey must be configured.");
        _keyBytes = Encoding.UTF8.GetBytes(secretKey);
        if (_keyBytes.Length < 32)
        {
            throw new InvalidOperationException("IdentifierHasher:SecretKey must be at least 32 characters.");
        }
    }

    public string Hash(string input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var hashBytes = HMACSHA256.HashData(_keyBytes, Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
```

- [ ] **Step 4 — Run tests; expect pass**

```bash
dotnet test test/SEBT.Portal.Tests/SEBT.Portal.Tests.csproj --filter "FullyQualifiedName~HMACSHA256HasherTests"
```

Expected: PASS (8 tests).

- [ ] **Step 5 — Commit**

```bash
git -C /Users/jblair/Projects/SEBT/sebt-self-service-portal/.worktrees/feature/co-cbms-household-cache add \
  src/SEBT.Portal.Infrastructure/Services/HMACSHA256Hasher.cs \
  test/SEBT.Portal.Tests/Unit/Services/HMACSHA256HasherTests.cs

git -C /Users/jblair/Projects/SEBT/sebt-self-service-portal/.worktrees/feature/co-cbms-household-cache commit -m "feat: implement IHMACSHA256Hasher backed by IdentifierHasher key"
```

### Task B3: Register `IHMACSHA256Hasher` in DI

**Files:**
- Modify: `src/SEBT.Portal.Infrastructure/Dependencies.cs`

- [ ] **Step 1 — Locate the IdentifierHasher registration**

```bash
grep -n "IdentifierHasher\|AddSingleton.*IIdentifier" \
  /Users/jblair/Projects/SEBT/sebt-self-service-portal/.worktrees/feature/co-cbms-household-cache/src/SEBT.Portal.Infrastructure/Dependencies.cs
```

You'll see a line like `services.AddSingleton<IIdentifierHasher, IdentifierHasher>();` (exact line varies — locate it before editing).

- [ ] **Step 2 — Add a sibling registration**

Use the Edit tool to insert *immediately after* the `IIdentifierHasher` registration:

```csharp
services.AddSingleton<IHMACSHA256Hasher, HMACSHA256Hasher>();
```

Make sure the `using SEBT.Portal.StatesPlugins.Interfaces.Services;` import is present at the top of the file.

- [ ] **Step 3 — Build to confirm compile**

```bash
dotnet build src/SEBT.Portal.Infrastructure/SEBT.Portal.Infrastructure.csproj
```

- [ ] **Step 4 — Add a DI-resolution test**

There's a `DependenciesTests` pattern in the test project (see `test/SEBT.Portal.Tests/Unit/UseCases/DependenciesTests.cs`). Add a single test asserting the new singleton resolves. Find the existing test class location:

```bash
grep -rn "IIdentifierHasher" /Users/jblair/Projects/SEBT/sebt-self-service-portal/.worktrees/feature/co-cbms-household-cache/test/SEBT.Portal.Tests/Unit/ | grep "Dependencies\|Resolves"
```

If a test exists for `IIdentifierHasher` resolution, add a parallel one for `IHMACSHA256Hasher`. If no such test exists, skip — the build itself plus the unit tests above are sufficient coverage.

- [ ] **Step 5 — Run the relevant tests**

```bash
dotnet test test/SEBT.Portal.Tests/SEBT.Portal.Tests.csproj --filter "Category!=Integration&Category!=SqlServer&Category!=Socure"
```

Expected: PASS, no regressions.

- [ ] **Step 6 — Commit**

```bash
git -C /Users/jblair/Projects/SEBT/sebt-self-service-portal/.worktrees/feature/co-cbms-household-cache add \
  src/SEBT.Portal.Infrastructure/Dependencies.cs

# Include any test additions in this commit if you added them
git -C /Users/jblair/Projects/SEBT/sebt-self-service-portal/.worktrees/feature/co-cbms-household-cache commit -m "chore: register IHMACSHA256Hasher in DI"
```

### Task B4 (OPTIONAL — separate commit): Refactor `IIdentifierHasher` to delegate

This is the cleanup the user explicitly asked for as a follow-up. Not blocking the cache work — `IHMACSHA256Hasher` already exists and works. Only do this if Phase B time permits AND tests stay green.

**Files:**
- Modify: `src/SEBT.Portal.Infrastructure/Services/IdentifierHasher.cs`

- [ ] **Step 1 — Modify constructor to take `IHMACSHA256Hasher`**

```csharp
public class IdentifierHasher : IIdentifierHasher
{
    private readonly IHMACSHA256Hasher _hmac;
    private const int HashLengthHex = 64;

    public IdentifierHasher(IHMACSHA256Hasher hmac)
    {
        _hmac = hmac ?? throw new ArgumentNullException(nameof(hmac));
    }

    public string? Hash(string? plaintext)
    {
        var normalized = IdentifierNormalizer.NormalizeOrNull(plaintext);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }
        return _hmac.Hash(normalized);
    }

    public bool Matches(string? plaintext, string? storedHash)
    {
        if (string.IsNullOrWhiteSpace(storedHash) || storedHash.Length != HashLengthHex)
        {
            return false;
        }

        var computed = Hash(plaintext);
        if (computed == null)
        {
            return false;
        }

        var computedBytes = Convert.FromHexString(computed);
        var storedBytes = Convert.FromHexString(storedHash);
        return CryptographicOperations.FixedTimeEquals(computedBytes, storedBytes);
    }

    public string? HashForStorage(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (value.Length == HashLengthHex && value.All(IsHexChar))
        {
            return value;
        }

        return Hash(value);
    }

    private static bool IsHexChar(char c) =>
        c is (>= '0' and <= '9') or (>= 'a' and <= 'f') or (>= 'A' and <= 'F');
}
```

- [ ] **Step 2 — Run the existing `IdentifierHasherTests`**

```bash
dotnet test test/SEBT.Portal.Tests/SEBT.Portal.Tests.csproj --filter "FullyQualifiedName~IdentifierHasherTests"
```

If tests use `Options.Create(...)` to construct the SUT directly, they need to be updated to construct via `new IdentifierHasher(new HMACSHA256Hasher(Options.Create(...)))`. Make those test edits before re-running.

Expected: PASS — output bytes are identical because the underlying primitive is the same.

- [ ] **Step 3 — Run unit suite**

```bash
dotnet test test/SEBT.Portal.Tests/SEBT.Portal.Tests.csproj --filter "Category!=Integration&Category!=SqlServer&Category!=Socure"
```

- [ ] **Step 4 — Commit (separate commit)**

```bash
git -C /Users/jblair/Projects/SEBT/sebt-self-service-portal/.worktrees/feature/co-cbms-household-cache add \
  src/SEBT.Portal.Infrastructure/Services/IdentifierHasher.cs \
  test/SEBT.Portal.Tests/Unit/Services/IdentifierHasherTests.cs

git -C /Users/jblair/Projects/SEBT/sebt-self-service-portal/.worktrees/feature/co-cbms-household-cache commit -m "refactor: delegate IdentifierHasher hashing to IHMACSHA256Hasher"
```

### Task B5: Push and open draft PR (main portal)

- [ ] **Step 1 — Push**

```bash
git -C /Users/jblair/Projects/SEBT/sebt-self-service-portal/.worktrees/feature/co-cbms-household-cache push -u origin feature/co-cbms-household-cache
```

- [ ] **Step 2 — Open draft PR**

```bash
cd /Users/jblair/Projects/SEBT/sebt-self-service-portal/.worktrees/feature/co-cbms-household-cache
gh pr create --draft --base main --head feature/co-cbms-household-cache \
  --title "Implement IHMACSHA256Hasher; (optional) refactor IIdentifierHasher to delegate" \
  --body "$(cat <<'EOF'
#### 🔗 Jira ticket
_To be added — ticket pending creation._

#### ✍️ Description
Provides the portal-side implementation of `IHMACSHA256Hasher` introduced in the state-connector PR. Reuses `IdentifierHasher:SecretKey`. Optionally refactors `IIdentifierHasher` to delegate to the new primitive (separate commit).

Companion PR — depends on contracts package PR landing first (see related PRs).

#### 🔗 Links to related PRs
- state-connector: `feature/co-cbms-household-cache` (introduces `IHMACSHA256Hasher`)
- co-connector: `feature/co-cbms-household-cache` (consumes `IHMACSHA256Hasher` for the CBMS cache)

#### ✅ Completion tasks
- [x] Added relevant tests
- [x] Meets acceptance criteria — primitive available via DI, optional delegation refactor preserves existing behavior
- [ ] Configuration changes:
  - [ ] If new environment variables are added, update in Tofu — _none added; reuses `IdentifierHasher:SecretKey`_
  - [ ] If new environment secrets are added — _none_
  - [ ] If you're adding an appsetting, add it to the requisite example file — _none_
  - [ ] If appsetttings are changed, update in AWS AppConfig — _none_
EOF
)"
```

---

## Phase C — CO connector: cache foundation types

> **Working directory:** `/Users/jblair/Projects/SEBT/sebt-self-service-portal-co-connector/.worktrees/feature/co-cbms-household-cache`.
>
> **Build flow:** the CO plugin references the state-connector contracts package via NuGet wildcard. After Phase A's package was built, run `dotnet restore` to pick up the new types.

### Task C1: Bump the contracts package reference and restore

- [ ] **Step 1 — Clean any stale state-connector packages from `~/nuget-store`**

Per memory note `feedback_worktree_nuget_cleanup`: stale packages in `~/nuget-store/` can cause version resolution surprises. List them:

```bash
ls -lt ~/nuget-store/SEBT.Portal.StatesPlugins.Interfaces.*.nupkg | head -5
```

Keep only the latest one if there's clutter; remove older `0.0.1-dev-*.nupkg` files older than the Phase A build.

- [ ] **Step 2 — Restore**

```bash
cd /Users/jblair/Projects/SEBT/sebt-self-service-portal-co-connector/.worktrees/feature/co-cbms-household-cache
dotnet restore
```

- [ ] **Step 3 — Verify the new type resolves**

```bash
grep -r "IHMACSHA256Hasher" obj/ 2>/dev/null | head -3
```

If the symbol isn't in the restored assembly, the package version is stale — re-clean `~/nuget-store/` and `dotnet restore --force`.

### Task C2: Add `CbmsHouseholdCacheOptions`

**Files:**
- Create: `src/SEBT.Portal.StatePlugins.CO/Cbms/Cache/CbmsHouseholdCacheOptions.cs`
- Test: `src/SEBT.Portal.StatePlugins.CO.Tests/Cbms/Cache/CbmsHouseholdCacheOptionsTests.cs`

- [ ] **Step 1 — Failing test for the options class**

```csharp
// src/SEBT.Portal.StatePlugins.CO.Tests/Cbms/Cache/CbmsHouseholdCacheOptionsTests.cs
using Microsoft.Extensions.Configuration;
using SEBT.Portal.StatePlugins.CO.Cbms.Cache;

namespace SEBT.Portal.StatePlugins.CO.Tests.Cbms.Cache;

public class CbmsHouseholdCacheOptionsTests
{
    [Fact]
    public void Defaults_match_design_spec()
    {
        var sut = new CbmsHouseholdCacheOptions();

        Assert.Equal(15, sut.SoftExpirationMinutes);
        Assert.Equal(240, sut.HardExpirationMinutes);
        Assert.Equal(60, sut.NegativeCacheSeconds);
        Assert.Equal(60, sut.BackgroundRefreshTimeoutSeconds);
    }

    [Fact]
    public void TimeSpan_helpers_compute_correctly()
    {
        var sut = new CbmsHouseholdCacheOptions
        {
            SoftExpirationMinutes = 20,
            HardExpirationMinutes = 300,
            NegativeCacheSeconds = 30,
            BackgroundRefreshTimeoutSeconds = 45
        };

        Assert.Equal(TimeSpan.FromMinutes(20), sut.SoftExpiration);
        Assert.Equal(TimeSpan.FromMinutes(300), sut.HardExpiration);
        Assert.Equal(TimeSpan.FromSeconds(30), sut.NegativeCacheExpiration);
        Assert.Equal(TimeSpan.FromSeconds(45), sut.BackgroundRefreshTimeout);
    }

    [Fact]
    public void Validate_returns_no_errors_for_defaults()
    {
        var sut = new CbmsHouseholdCacheOptions();
        Assert.Empty(sut.Validate());
    }

    [Theory]
    [InlineData(0, 240, 60, 60, "SoftExpirationMinutes must be > 0")]
    [InlineData(15, 0, 60, 60, "HardExpirationMinutes must be > 0")]
    [InlineData(240, 240, 60, 60, "SoftExpirationMinutes must be < HardExpirationMinutes")]
    [InlineData(15, 240, -1, 60, "NegativeCacheSeconds must be >= 0")]
    [InlineData(15, 240, 60, 0, "BackgroundRefreshTimeoutSeconds must be > 0")]
    public void Validate_returns_error_when_invalid(int soft, int hard, int neg, int bg, string expected)
    {
        var sut = new CbmsHouseholdCacheOptions
        {
            SoftExpirationMinutes = soft,
            HardExpirationMinutes = hard,
            NegativeCacheSeconds = neg,
            BackgroundRefreshTimeoutSeconds = bg
        };

        Assert.Contains(expected, sut.Validate());
    }

    [Fact]
    public void Bind_from_configuration_section()
    {
        var configBuilder = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Cbms:Cache:SoftExpirationMinutes"] = "5",
            ["Cbms:Cache:HardExpirationMinutes"] = "120",
            ["Cbms:Cache:NegativeCacheSeconds"] = "30",
            ["Cbms:Cache:BackgroundRefreshTimeoutSeconds"] = "45",
        });
        var config = configBuilder.Build();

        var sut = new CbmsHouseholdCacheOptions();
        config.GetSection("Cbms:Cache").Bind(sut);

        Assert.Equal(5, sut.SoftExpirationMinutes);
        Assert.Equal(120, sut.HardExpirationMinutes);
        Assert.Equal(30, sut.NegativeCacheSeconds);
        Assert.Equal(45, sut.BackgroundRefreshTimeoutSeconds);
    }
}
```

- [ ] **Step 2 — Run; expect compile failure**

```bash
dotnet test --filter "FullyQualifiedName~CbmsHouseholdCacheOptionsTests"
```

Expected: FAIL — type not defined.

- [ ] **Step 3 — Implement**

```csharp
// src/SEBT.Portal.StatePlugins.CO/Cbms/Cache/CbmsHouseholdCacheOptions.cs
namespace SEBT.Portal.StatePlugins.CO.Cbms.Cache;

internal sealed class CbmsHouseholdCacheOptions
{
    public int SoftExpirationMinutes { get; set; } = 15;
    public int HardExpirationMinutes { get; set; } = 240;
    public int NegativeCacheSeconds { get; set; } = 60;
    public int BackgroundRefreshTimeoutSeconds { get; set; } = 60;

    public TimeSpan SoftExpiration => TimeSpan.FromMinutes(SoftExpirationMinutes);
    public TimeSpan HardExpiration => TimeSpan.FromMinutes(HardExpirationMinutes);
    public TimeSpan NegativeCacheExpiration => TimeSpan.FromSeconds(NegativeCacheSeconds);
    public TimeSpan BackgroundRefreshTimeout => TimeSpan.FromSeconds(BackgroundRefreshTimeoutSeconds);

    public IEnumerable<string> Validate()
    {
        if (SoftExpirationMinutes <= 0) yield return "SoftExpirationMinutes must be > 0";
        if (HardExpirationMinutes <= 0) yield return "HardExpirationMinutes must be > 0";
        if (SoftExpirationMinutes >= HardExpirationMinutes) yield return "SoftExpirationMinutes must be < HardExpirationMinutes";
        if (NegativeCacheSeconds < 0) yield return "NegativeCacheSeconds must be >= 0";
        if (BackgroundRefreshTimeoutSeconds <= 0) yield return "BackgroundRefreshTimeoutSeconds must be > 0";
    }
}
```

Note: `internal sealed` because nothing outside the CO plugin assembly should reference it. Tests reach in via `[InternalsVisibleTo]` (added in Task C5).

- [ ] **Step 4 — Run tests; expect PASS**

- [ ] **Step 5 — Commit**

```bash
git -C /Users/jblair/Projects/SEBT/sebt-self-service-portal-co-connector/.worktrees/feature/co-cbms-household-cache add \
  src/SEBT.Portal.StatePlugins.CO/Cbms/Cache/CbmsHouseholdCacheOptions.cs \
  src/SEBT.Portal.StatePlugins.CO.Tests/Cbms/Cache/CbmsHouseholdCacheOptionsTests.cs

git -C /Users/jblair/Projects/SEBT/sebt-self-service-portal-co-connector/.worktrees/feature/co-cbms-household-cache commit -m "feat(co-cache): add CbmsHouseholdCacheOptions with defaults + validation"
```

### Task C3: Add `CbmsHouseholdCacheEnvelope`

**Files:**
- Create: `src/SEBT.Portal.StatePlugins.CO/Cbms/Cache/CbmsHouseholdCacheEnvelope.cs`
- Test: `src/SEBT.Portal.StatePlugins.CO.Tests/Cbms/Cache/CbmsHouseholdCacheEnvelopeTests.cs`

- [ ] **Step 1 — Failing test**

```csharp
// src/SEBT.Portal.StatePlugins.CO.Tests/Cbms/Cache/CbmsHouseholdCacheEnvelopeTests.cs
using SEBT.Portal.StatePlugins.CO.Cbms.Cache;
using SEBT.Portal.StatePlugins.CO.CbmsApi.Models;

namespace SEBT.Portal.StatePlugins.CO.Tests.Cbms.Cache;

public class CbmsHouseholdCacheEnvelopeTests
{
    [Fact]
    public void Envelope_carries_response_and_expiries()
    {
        var response = new GetAccountDetailsResponse();
        var now = DateTimeOffset.UtcNow;
        var envelope = new CbmsHouseholdCacheEnvelope(
            Response: response,
            SoftExpiryUtc: now.AddMinutes(15),
            HardExpiryUtc: now.AddHours(4),
            CachedAtUtc: now);

        Assert.Same(response, envelope.Response);
        Assert.Equal(now.AddMinutes(15), envelope.SoftExpiryUtc);
        Assert.Equal(now.AddHours(4), envelope.HardExpiryUtc);
        Assert.Equal(now, envelope.CachedAtUtc);
    }

    [Fact]
    public void Envelope_is_record_with_value_equality()
    {
        var response = new GetAccountDetailsResponse();
        var now = DateTimeOffset.UtcNow;
        var a = new CbmsHouseholdCacheEnvelope(response, now.AddMinutes(15), now.AddHours(4), now);
        var b = new CbmsHouseholdCacheEnvelope(response, now.AddMinutes(15), now.AddHours(4), now);

        Assert.Equal(a, b);
    }
}
```

- [ ] **Step 2 — Run; expect FAIL**

- [ ] **Step 3 — Implement**

```csharp
// src/SEBT.Portal.StatePlugins.CO/Cbms/Cache/CbmsHouseholdCacheEnvelope.cs
using SEBT.Portal.StatePlugins.CO.CbmsApi.Models;

namespace SEBT.Portal.StatePlugins.CO.Cbms.Cache;

internal sealed record CbmsHouseholdCacheEnvelope(
    GetAccountDetailsResponse Response,
    DateTimeOffset SoftExpiryUtc,
    DateTimeOffset HardExpiryUtc,
    DateTimeOffset CachedAtUtc);
```

- [ ] **Step 4 — Run; expect PASS**

- [ ] **Step 5 — Commit**

```bash
git -C /Users/jblair/Projects/SEBT/sebt-self-service-portal-co-connector/.worktrees/feature/co-cbms-household-cache add \
  src/SEBT.Portal.StatePlugins.CO/Cbms/Cache/CbmsHouseholdCacheEnvelope.cs \
  src/SEBT.Portal.StatePlugins.CO.Tests/Cbms/Cache/CbmsHouseholdCacheEnvelopeTests.cs

git -C /Users/jblair/Projects/SEBT/sebt-self-service-portal-co-connector/.worktrees/feature/co-cbms-household-cache commit -m "feat(co-cache): add CbmsHouseholdCacheEnvelope record"
```

### Task C4: Add `ICbmsHouseholdCache` interface and the fetch delegate

**Files:**
- Create: `src/SEBT.Portal.StatePlugins.CO/Cbms/Cache/ICbmsHouseholdCache.cs`
- Create: `src/SEBT.Portal.StatePlugins.CO/Cbms/Cache/CbmsFetchAccountDetailsDelegate.cs`

- [ ] **Step 1 — Implement the delegate**

```csharp
// src/SEBT.Portal.StatePlugins.CO/Cbms/Cache/CbmsFetchAccountDetailsDelegate.cs
using SEBT.Portal.StatePlugins.CO.CbmsApi.Models;

namespace SEBT.Portal.StatePlugins.CO.Cbms.Cache;

/// <summary>
/// Captures the CBMS get-account-details call so the cache can be tested
/// without instantiating a real Kiota client. Returns null when CBMS reports
/// no household for the given normalized phone (404 or empty rows).
/// </summary>
internal delegate Task<GetAccountDetailsResponse?> CbmsFetchAccountDetailsDelegate(
    string normalizedPhone,
    CancellationToken cancellationToken);
```

- [ ] **Step 2 — Implement the interface**

```csharp
// src/SEBT.Portal.StatePlugins.CO/Cbms/Cache/ICbmsHouseholdCache.cs
using SEBT.Portal.StatePlugins.CO.CbmsApi.Models;

namespace SEBT.Portal.StatePlugins.CO.Cbms.Cache;

internal interface ICbmsHouseholdCache
{
    /// <summary>
    /// Returns the cached CBMS GetAccountDetailsResponse for the household,
    /// fetching from CBMS on miss or hard-expiry. On soft-expiry, returns the
    /// cached value AND triggers a coalesced background refresh.
    /// Returns null when CBMS reports no household for the normalized phone.
    /// </summary>
    Task<GetAccountDetailsResponse?> GetAsync(string normalizedPhone, CancellationToken cancellationToken);

    /// <summary>
    /// Write-through: store the (locally-mutated) response after a successful PATCH.
    /// On underlying SetAsync failure, falls back to InvalidateAsync (tripwire).
    /// </summary>
    Task SetAsync(string normalizedPhone, GetAccountDetailsResponse value, CancellationToken cancellationToken);

    /// <summary>
    /// Explicit invalidation. Used by the tripwire and as an escape hatch.
    /// </summary>
    Task InvalidateAsync(string normalizedPhone, CancellationToken cancellationToken);
}
```

- [ ] **Step 3 — Build to confirm compile**

```bash
dotnet build src/SEBT.Portal.StatePlugins.CO/SEBT.Portal.StatePlugins.CO.csproj
```

- [ ] **Step 4 — Commit**

```bash
git -C /Users/jblair/Projects/SEBT/sebt-self-service-portal-co-connector/.worktrees/feature/co-cbms-household-cache add \
  src/SEBT.Portal.StatePlugins.CO/Cbms/Cache/ICbmsHouseholdCache.cs \
  src/SEBT.Portal.StatePlugins.CO/Cbms/Cache/CbmsFetchAccountDetailsDelegate.cs

git -C /Users/jblair/Projects/SEBT/sebt-self-service-portal-co-connector/.worktrees/feature/co-cbms-household-cache commit -m "feat(co-cache): add ICbmsHouseholdCache contract and fetch delegate"
```

### Task C5: Enable `InternalsVisibleTo` for the test project

**Files:**
- Modify: `src/SEBT.Portal.StatePlugins.CO/SEBT.Portal.StatePlugins.CO.csproj`

- [ ] **Step 1 — Read the current csproj**

Use the Read tool on `src/SEBT.Portal.StatePlugins.CO/SEBT.Portal.StatePlugins.CO.csproj`.

- [ ] **Step 2 — Add `InternalsVisibleTo`**

If the csproj does not already contain an `<ItemGroup>` with `<InternalsVisibleTo>` for the test project, add one:

```xml
  <ItemGroup>
    <InternalsVisibleTo Include="SEBT.Portal.StatePlugins.CO.Tests" />
  </ItemGroup>
```

- [ ] **Step 3 — Build**

```bash
dotnet build
```

Both the plugin and tests projects should still compile.

- [ ] **Step 4 — Commit**

```bash
git -C /Users/jblair/Projects/SEBT/sebt-self-service-portal-co-connector/.worktrees/feature/co-cbms-household-cache add \
  src/SEBT.Portal.StatePlugins.CO/SEBT.Portal.StatePlugins.CO.csproj

git -C /Users/jblair/Projects/SEBT/sebt-self-service-portal-co-connector/.worktrees/feature/co-cbms-household-cache commit -m "chore(co-cache): expose CO plugin internals to test project"
```

---

## Phase D — CO connector: cache implementation (TDD)

> All tasks in this phase work in `src/SEBT.Portal.StatePlugins.CO/Cbms/Cache/CbmsHouseholdCache.cs`. Each task adds tests, makes them fail, adds code to make them pass, and commits.
>
> **Test infrastructure:** all cache tests use a fake `HybridCache` (we'll add a tiny in-memory implementation as a test helper) and a stub `CbmsFetchAccountDetailsDelegate` whose call count we assert on. `IHMACSHA256Hasher` is stubbed via NSubstitute.

### Task D0: Test fixture / helpers

**Files:**
- Create: `src/SEBT.Portal.StatePlugins.CO.Tests/Cbms/Cache/InMemoryHybridCache.cs`

A minimal in-memory `HybridCache` implementation for tests. HybridCache itself is abstract; we can't easily mock it because of its many overloads, so we provide a real-but-simple impl that supports our usage.

- [ ] **Step 1 — Implement the test fixture**

```csharp
// src/SEBT.Portal.StatePlugins.CO.Tests/Cbms/Cache/InMemoryHybridCache.cs
using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Hybrid;

namespace SEBT.Portal.StatePlugins.CO.Tests.Cbms.Cache;

/// <summary>
/// Minimal HybridCache stand-in for unit tests. Provides per-key coalescing
/// for GetOrCreateAsync, immediate Set/Remove, and a way to inspect cached entries.
/// Does NOT honor expiration timestamps — tests that need to simulate expiration
/// should call RemoveAsync directly to evict.
/// </summary>
internal sealed class InMemoryHybridCache : HybridCache
{
    private readonly ConcurrentDictionary<string, object?> _store = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    public int FactoryInvocations { get; private set; }

    public bool TryGet<T>(string key, out T? value)
    {
        if (_store.TryGetValue(key, out var raw) && raw is T t)
        {
            value = t;
            return true;
        }
        value = default;
        return false;
    }

    public override async ValueTask<T> GetOrCreateAsync<TState, T>(
        string key,
        TState state,
        Func<TState, CancellationToken, ValueTask<T>> factory,
        HybridCacheEntryOptions? options = null,
        IEnumerable<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        if (_store.TryGetValue(key, out var existing) && existing is T cached)
        {
            return cached;
        }

        var sem = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await sem.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_store.TryGetValue(key, out existing) && existing is T cached2)
            {
                return cached2;
            }
            FactoryInvocations++;
            var produced = await factory(state, cancellationToken).ConfigureAwait(false);
            _store[key] = produced;
            return produced;
        }
        finally
        {
            sem.Release();
        }
    }

    public override async ValueTask SetAsync<T>(
        string key,
        T value,
        HybridCacheEntryOptions? options = null,
        IEnumerable<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        _store[key] = value;
    }

    public override ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        _store.TryRemove(key, out _);
        return ValueTask.CompletedTask;
    }

    public override ValueTask RemoveByTagAsync(string tag, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;
}
```

> **Implementation note:** if `HybridCache` adds new abstract methods in a future .NET version, this stub must implement them. Track upstream changes if updating the .NET runtime.

- [ ] **Step 2 — Build and confirm the helper compiles**

```bash
dotnet build src/SEBT.Portal.StatePlugins.CO.Tests/SEBT.Portal.StatePlugins.CO.Tests.csproj
```

- [ ] **Step 3 — Commit**

```bash
git -C /Users/jblair/Projects/SEBT/sebt-self-service-portal-co-connector/.worktrees/feature/co-cbms-household-cache add \
  src/SEBT.Portal.StatePlugins.CO.Tests/Cbms/Cache/InMemoryHybridCache.cs

git -C /Users/jblair/Projects/SEBT/sebt-self-service-portal-co-connector/.worktrees/feature/co-cbms-household-cache commit -m "test(co-cache): add in-memory HybridCache test helper"
```

### Task D1: Implement the cache class scaffold and key hashing

**Files:**
- Create: `src/SEBT.Portal.StatePlugins.CO/Cbms/Cache/CbmsHouseholdCache.cs`
- Test: `src/SEBT.Portal.StatePlugins.CO.Tests/Cbms/Cache/CbmsHouseholdCacheKeyHashingTests.cs`

- [ ] **Step 1 — Failing test for key shape**

```csharp
// src/SEBT.Portal.StatePlugins.CO.Tests/Cbms/Cache/CbmsHouseholdCacheKeyHashingTests.cs
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using SEBT.Portal.StatePlugins.CO.Cbms.Cache;
using SEBT.Portal.StatePlugins.CO.CbmsApi.Models;
using SEBT.Portal.StatesPlugins.Interfaces.Services;

namespace SEBT.Portal.StatePlugins.CO.Tests.Cbms.Cache;

public class CbmsHouseholdCacheKeyHashingTests
{
    private const string Phone = "3035550199";

    [Fact]
    public async Task GetAsync_uses_co_cbms_namespace_with_hashed_phone()
    {
        var hybrid = new InMemoryHybridCache();
        var hasher = Substitute.For<IHMACSHA256Hasher>();
        hasher.Hash(Phone).Returns("deadbeef");
        var lifetime = Substitute.For<IHostApplicationLifetime>();
        lifetime.ApplicationStopping.Returns(CancellationToken.None);

        CbmsFetchAccountDetailsDelegate fetch = (_, _) => Task.FromResult<GetAccountDetailsResponse?>(new());

        var sut = new CbmsHouseholdCache(
            hybrid, hasher, lifetime, NullLoggerFactory.Instance,
            Options.Create(new CbmsHouseholdCacheOptions()),
            fetch);

        await sut.GetAsync(Phone, CancellationToken.None);

        Assert.True(hybrid.TryGet<CbmsHouseholdCacheEnvelope?>("co:cbms:deadbeef", out _));
    }

    [Fact]
    public async Task GetAsync_invokes_hasher_with_normalized_phone()
    {
        var hybrid = new InMemoryHybridCache();
        var hasher = Substitute.For<IHMACSHA256Hasher>();
        hasher.Hash(Arg.Any<string>()).Returns("hash");
        var lifetime = Substitute.For<IHostApplicationLifetime>();
        lifetime.ApplicationStopping.Returns(CancellationToken.None);

        CbmsFetchAccountDetailsDelegate fetch = (_, _) => Task.FromResult<GetAccountDetailsResponse?>(new());
        var sut = new CbmsHouseholdCache(
            hybrid, hasher, lifetime, NullLoggerFactory.Instance,
            Options.Create(new CbmsHouseholdCacheOptions()),
            fetch);

        await sut.GetAsync(Phone, CancellationToken.None);

        hasher.Received(1).Hash(Phone);
    }

    [Fact]
    public async Task Cache_key_never_contains_raw_phone()
    {
        var hybrid = new InMemoryHybridCache();
        var hasher = Substitute.For<IHMACSHA256Hasher>();
        hasher.Hash(Phone).Returns("nothingmatchingaphone");
        var lifetime = Substitute.For<IHostApplicationLifetime>();
        lifetime.ApplicationStopping.Returns(CancellationToken.None);

        CbmsFetchAccountDetailsDelegate fetch = (_, _) => Task.FromResult<GetAccountDetailsResponse?>(new());
        var sut = new CbmsHouseholdCache(
            hybrid, hasher, lifetime, NullLoggerFactory.Instance,
            Options.Create(new CbmsHouseholdCacheOptions()),
            fetch);

        await sut.GetAsync(Phone, CancellationToken.None);

        var keys = hybrid.GetType()
            .GetField("_store", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(hybrid) as System.Collections.Concurrent.ConcurrentDictionary<string, object?>;

        Assert.NotNull(keys);
        Assert.DoesNotContain(keys!.Keys, k => k.Contains(Phone));
    }
}
```

- [ ] **Step 2 — Run; expect FAIL (CbmsHouseholdCache type missing)**

```bash
dotnet test --filter "FullyQualifiedName~CbmsHouseholdCacheKeyHashingTests"
```

- [ ] **Step 3 — Implement the cache scaffold (read flow + key hashing only)**

```csharp
// src/SEBT.Portal.StatePlugins.CO/Cbms/Cache/CbmsHouseholdCache.cs
using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SEBT.Portal.StatePlugins.CO.CbmsApi.Models;
using SEBT.Portal.StatesPlugins.Interfaces.Services;

namespace SEBT.Portal.StatePlugins.CO.Cbms.Cache;

internal sealed class CbmsHouseholdCache : ICbmsHouseholdCache
{
    private const string KeyPrefix = "co:cbms:";

    private readonly HybridCache _hybridCache;
    private readonly IHMACSHA256Hasher _hasher;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<CbmsHouseholdCache> _logger;
    private readonly CbmsHouseholdCacheOptions _options;
    private readonly CbmsFetchAccountDetailsDelegate _fetchFromCbms;
    private readonly ConcurrentDictionary<string, Task> _inFlightRefreshes = new();

    public CbmsHouseholdCache(
        HybridCache hybridCache,
        IHMACSHA256Hasher hasher,
        IHostApplicationLifetime lifetime,
        ILoggerFactory loggerFactory,
        IOptions<CbmsHouseholdCacheOptions> options,
        CbmsFetchAccountDetailsDelegate fetchFromCbms)
    {
        _hybridCache = hybridCache;
        _hasher = hasher;
        _lifetime = lifetime;
        _logger = loggerFactory.CreateLogger<CbmsHouseholdCache>();
        _options = options.Value;
        _fetchFromCbms = fetchFromCbms;
    }

    private string KeyFor(string normalizedPhone) => KeyPrefix + _hasher.Hash(normalizedPhone);

    public async Task<GetAccountDetailsResponse?> GetAsync(string normalizedPhone, CancellationToken cancellationToken)
    {
        var key = KeyFor(normalizedPhone);
        var hybridOptions = new HybridCacheEntryOptions { Expiration = _options.HardExpiration };

        var envelope = await _hybridCache.GetOrCreateAsync(
            key,
            (Phone: normalizedPhone, Cache: this),
            (state, ct) => state.Cache.FetchAndWrapAsync(state.Phone, ct),
            hybridOptions,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (envelope is null) return null;

        if (DateTimeOffset.UtcNow < envelope.SoftExpiryUtc) return envelope.Response;

        // Stale: return value immediately and trigger background refresh.
        TriggerBackgroundRefresh(key, normalizedPhone);
        return envelope.Response;
    }

    private async ValueTask<CbmsHouseholdCacheEnvelope?> FetchAndWrapAsync(
        string normalizedPhone, CancellationToken cancellationToken)
    {
        var response = await _fetchFromCbms(normalizedPhone, cancellationToken).ConfigureAwait(false);
        if (response is null || response.StdntEnrollDtls is null || response.StdntEnrollDtls.Count == 0)
        {
            // Negative cache: short envelope, nothing to mutate
            return null;
        }
        var now = DateTimeOffset.UtcNow;
        return new CbmsHouseholdCacheEnvelope(
            Response: response,
            SoftExpiryUtc: now + _options.SoftExpiration,
            HardExpiryUtc: now + _options.HardExpiration,
            CachedAtUtc: now);
    }

    private void TriggerBackgroundRefresh(string key, string normalizedPhone)
    {
        _ = _inFlightRefreshes.GetOrAdd(key, k => RunRefreshAsync(k, normalizedPhone));
    }

    private async Task RunRefreshAsync(string key, string normalizedPhone)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.ApplicationStopping);
            cts.CancelAfter(_options.BackgroundRefreshTimeout);
            var fresh = await FetchAndWrapAsync(normalizedPhone, cts.Token).ConfigureAwait(false);
            if (fresh is not null)
            {
                await _hybridCache.SetAsync(
                    key, fresh,
                    new HybridCacheEntryOptions { Expiration = _options.HardExpiration },
                    cancellationToken: cts.Token).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Background CBMS refresh failed; stale envelope retained");
        }
        finally
        {
            _inFlightRefreshes.TryRemove(key, out _);
        }
    }

    public Task SetAsync(string normalizedPhone, GetAccountDetailsResponse value, CancellationToken cancellationToken)
        => throw new NotImplementedException("Implemented in Task D3");

    public Task InvalidateAsync(string normalizedPhone, CancellationToken cancellationToken)
        => throw new NotImplementedException("Implemented in Task D3");
}
```

The `SetAsync`/`InvalidateAsync` placeholders are intentional — Task D3 fills them in. They throw so any test that accidentally invokes them fails loudly.

- [ ] **Step 4 — Run key-hashing tests; expect PASS**

```bash
dotnet test --filter "FullyQualifiedName~CbmsHouseholdCacheKeyHashingTests"
```

- [ ] **Step 5 — Commit**

```bash
git -C /Users/jblair/Projects/SEBT/sebt-self-service-portal-co-connector/.worktrees/feature/co-cbms-household-cache add \
  src/SEBT.Portal.StatePlugins.CO/Cbms/Cache/CbmsHouseholdCache.cs \
  src/SEBT.Portal.StatePlugins.CO.Tests/Cbms/Cache/CbmsHouseholdCacheKeyHashingTests.cs

git -C /Users/jblair/Projects/SEBT/sebt-self-service-portal-co-connector/.worktrees/feature/co-cbms-household-cache commit -m "feat(co-cache): scaffold CbmsHouseholdCache with hashed-key read path"
```

### Task D2: Read flow tests (hit, stale, miss, negative cache)

**Files:**
- Create: `src/SEBT.Portal.StatePlugins.CO.Tests/Cbms/Cache/CbmsHouseholdCacheReadTests.cs`

- [ ] **Step 1 — Add the failing tests**

```csharp
// src/SEBT.Portal.StatePlugins.CO.Tests/Cbms/Cache/CbmsHouseholdCacheReadTests.cs
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using SEBT.Portal.StatePlugins.CO.Cbms.Cache;
using SEBT.Portal.StatePlugins.CO.CbmsApi.Models;
using SEBT.Portal.StatesPlugins.Interfaces.Services;

namespace SEBT.Portal.StatePlugins.CO.Tests.Cbms.Cache;

public class CbmsHouseholdCacheReadTests
{
    private const string Phone = "3035550199";

    private static GetAccountDetailsResponse Populated() => new()
    {
        StdntEnrollDtls = new() { new StdntEnrollDtl() }
    };

    private static GetAccountDetailsResponse Empty() => new()
    {
        StdntEnrollDtls = new()
    };

    private (CbmsHouseholdCache sut, InMemoryHybridCache hybrid, FakeFetch fetch) Build(
        CbmsHouseholdCacheOptions? options = null)
    {
        var hybrid = new InMemoryHybridCache();
        var hasher = Substitute.For<IHMACSHA256Hasher>();
        hasher.Hash(Arg.Any<string>()).Returns(c => "hash:" + c.Arg<string>());
        var lifetime = Substitute.For<IHostApplicationLifetime>();
        lifetime.ApplicationStopping.Returns(CancellationToken.None);
        var fetch = new FakeFetch();
        var sut = new CbmsHouseholdCache(
            hybrid, hasher, lifetime, NullLoggerFactory.Instance,
            Options.Create(options ?? new CbmsHouseholdCacheOptions()),
            fetch.Delegate);
        return (sut, hybrid, fetch);
    }

    [Fact]
    public async Task Cache_miss_calls_CBMS_then_caches()
    {
        var (sut, hybrid, fetch) = Build();
        fetch.NextResponse = Populated();

        var result = await sut.GetAsync(Phone, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(1, fetch.CallCount);
    }

    [Fact]
    public async Task Cache_hit_does_not_call_CBMS_again()
    {
        var (sut, _, fetch) = Build();
        fetch.NextResponse = Populated();
        await sut.GetAsync(Phone, CancellationToken.None); // primes
        fetch.CallCount = 0;

        await sut.GetAsync(Phone, CancellationToken.None);

        Assert.Equal(0, fetch.CallCount);
    }

    [Fact]
    public async Task Cache_returns_null_when_CBMS_returns_empty_household()
    {
        var (sut, _, fetch) = Build();
        fetch.NextResponse = Empty();

        var result = await sut.GetAsync(Phone, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Cache_returns_null_when_CBMS_returns_null()
    {
        var (sut, _, fetch) = Build();
        fetch.NextResponse = null;

        var result = await sut.GetAsync(Phone, CancellationToken.None);

        Assert.Null(result);
    }
}

internal sealed class FakeFetch
{
    public int CallCount { get; set; }
    public GetAccountDetailsResponse? NextResponse { get; set; }

    public CbmsFetchAccountDetailsDelegate Delegate => (_, _) =>
    {
        CallCount++;
        return Task.FromResult(NextResponse);
    };
}
```

- [ ] **Step 2 — Run; expect mostly PASS (the implementation already supports basic read), confirm**

```bash
dotnet test --filter "FullyQualifiedName~CbmsHouseholdCacheReadTests"
```

If any test fails, adjust the cache implementation OR the tests until both align with the spec. The main risk is the negative cache behavior — currently the cache returns `null` envelope, which means subsequent calls re-fetch. We address that in the next step.

- [ ] **Step 3 — Add a negative-cache test**

```csharp
[Fact]
public async Task Negative_cache_does_not_immediately_refetch_within_window()
{
    var (sut, _, fetch) = Build(new CbmsHouseholdCacheOptions { NegativeCacheSeconds = 60 });
    fetch.NextResponse = Empty();
    await sut.GetAsync(Phone, CancellationToken.None);
    fetch.CallCount = 0;

    await sut.GetAsync(Phone, CancellationToken.None);

    Assert.Equal(0, fetch.CallCount);
}
```

- [ ] **Step 4 — Run; expect FAIL** (negative envelope currently returns null and re-fetches)

- [ ] **Step 5 — Implement negative cache**

Modify `FetchAndWrapAsync` to return a "negative envelope" sentinel rather than null. We use a private sentinel class so we can short-circuit:

```csharp
// In CbmsHouseholdCache.cs — replace FetchAndWrapAsync with:

private async ValueTask<CbmsHouseholdCacheEnvelope?> FetchAndWrapAsync(
    string normalizedPhone, CancellationToken cancellationToken)
{
    var response = await _fetchFromCbms(normalizedPhone, cancellationToken).ConfigureAwait(false);
    var now = DateTimeOffset.UtcNow;

    if (response is null || response.StdntEnrollDtls is null || response.StdntEnrollDtls.Count == 0)
    {
        // Negative cache: empty response with shorter expiry. Stored as an envelope so the
        // HybridCache layer treats it the same as a hit (doesn't re-invoke factory).
        return new CbmsHouseholdCacheEnvelope(
            Response: NegativeMarkerResponse,
            SoftExpiryUtc: now + _options.NegativeCacheExpiration,
            HardExpiryUtc: now + _options.NegativeCacheExpiration,
            CachedAtUtc: now);
    }

    return new CbmsHouseholdCacheEnvelope(
        Response: response,
        SoftExpiryUtc: now + _options.SoftExpiration,
        HardExpiryUtc: now + _options.HardExpiration,
        CachedAtUtc: now);
}

// Add a static singleton sentinel:
private static readonly GetAccountDetailsResponse NegativeMarkerResponse = new()
{
    StdntEnrollDtls = new()
};

// And change GetAsync to detect it:
public async Task<GetAccountDetailsResponse?> GetAsync(string normalizedPhone, CancellationToken cancellationToken)
{
    var key = KeyFor(normalizedPhone);
    var hybridOptions = new HybridCacheEntryOptions { Expiration = _options.HardExpiration };

    var envelope = await _hybridCache.GetOrCreateAsync(
        key,
        (Phone: normalizedPhone, Cache: this),
        (state, ct) => state.Cache.FetchAndWrapAsync(state.Phone, ct),
        hybridOptions,
        cancellationToken: cancellationToken).ConfigureAwait(false);

    if (envelope is null) return null;
    if (ReferenceEquals(envelope.Response, NegativeMarkerResponse)) return null;

    if (DateTimeOffset.UtcNow < envelope.SoftExpiryUtc) return envelope.Response;

    TriggerBackgroundRefresh(key, normalizedPhone);
    return envelope.Response;
}
```

> **Trade-off note:** the sentinel approach means a negative envelope's hard expiry equals its soft expiry (`NegativeCacheExpiration`). HybridCache's framework expiry honors the same value. After that window, the next read will re-fetch from CBMS — matching the spec's "negative caching for `NegativeCacheSeconds`" requirement.

- [ ] **Step 6 — Run; expect PASS**

- [ ] **Step 7 — Commit**

```bash
git -C /Users/jblair/Projects/SEBT/sebt-self-service-portal-co-connector/.worktrees/feature/co-cbms-household-cache add \
  src/SEBT.Portal.StatePlugins.CO/Cbms/Cache/CbmsHouseholdCache.cs \
  src/SEBT.Portal.StatePlugins.CO.Tests/Cbms/Cache/CbmsHouseholdCacheReadTests.cs

git -C /Users/jblair/Projects/SEBT/sebt-self-service-portal-co-connector/.worktrees/feature/co-cbms-household-cache commit -m "feat(co-cache): read path with negative caching"
```

### Task D3: Write-through and tripwire (`SetAsync` / `InvalidateAsync`)

**Files:**
- Modify: `src/SEBT.Portal.StatePlugins.CO/Cbms/Cache/CbmsHouseholdCache.cs`
- Create: `src/SEBT.Portal.StatePlugins.CO.Tests/Cbms/Cache/CbmsHouseholdCacheWriteThroughTests.cs`

- [ ] **Step 1 — Failing tests**

```csharp
// src/SEBT.Portal.StatePlugins.CO.Tests/Cbms/Cache/CbmsHouseholdCacheWriteThroughTests.cs
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using SEBT.Portal.StatePlugins.CO.Cbms.Cache;
using SEBT.Portal.StatePlugins.CO.CbmsApi.Models;
using SEBT.Portal.StatesPlugins.Interfaces.Services;

namespace SEBT.Portal.StatePlugins.CO.Tests.Cbms.Cache;

public class CbmsHouseholdCacheWriteThroughTests
{
    private const string Phone = "3035550199";

    private static GetAccountDetailsResponse Populated() => new()
    {
        StdntEnrollDtls = new() { new StdntEnrollDtl() }
    };

    [Fact]
    public async Task SetAsync_makes_subsequent_reads_return_set_value()
    {
        var hybrid = new InMemoryHybridCache();
        var hasher = Substitute.For<IHMACSHA256Hasher>();
        hasher.Hash(Arg.Any<string>()).Returns("hash");
        var lifetime = Substitute.For<IHostApplicationLifetime>();
        lifetime.ApplicationStopping.Returns(CancellationToken.None);
        var fetch = new FakeFetch();
        var sut = new CbmsHouseholdCache(
            hybrid, hasher, lifetime, NullLoggerFactory.Instance,
            Options.Create(new CbmsHouseholdCacheOptions()),
            fetch.Delegate);

        var newResponse = Populated();
        await sut.SetAsync(Phone, newResponse, CancellationToken.None);

        var read = await sut.GetAsync(Phone, CancellationToken.None);

        Assert.Same(newResponse, read);
        Assert.Equal(0, fetch.CallCount);
    }

    [Fact]
    public async Task InvalidateAsync_causes_next_read_to_refetch()
    {
        var hybrid = new InMemoryHybridCache();
        var hasher = Substitute.For<IHMACSHA256Hasher>();
        hasher.Hash(Arg.Any<string>()).Returns("hash");
        var lifetime = Substitute.For<IHostApplicationLifetime>();
        lifetime.ApplicationStopping.Returns(CancellationToken.None);
        var fetch = new FakeFetch { NextResponse = Populated() };
        var sut = new CbmsHouseholdCache(
            hybrid, hasher, lifetime, NullLoggerFactory.Instance,
            Options.Create(new CbmsHouseholdCacheOptions()),
            fetch.Delegate);
        await sut.GetAsync(Phone, CancellationToken.None);
        Assert.Equal(1, fetch.CallCount);

        await sut.InvalidateAsync(Phone, CancellationToken.None);
        await sut.GetAsync(Phone, CancellationToken.None);

        Assert.Equal(2, fetch.CallCount);
    }

    [Fact]
    public async Task SetAsync_tripwire_falls_back_to_invalidate_when_cache_write_throws()
    {
        var hybrid = new ThrowingHybridCache();
        var hasher = Substitute.For<IHMACSHA256Hasher>();
        hasher.Hash(Arg.Any<string>()).Returns("hash");
        var lifetime = Substitute.For<IHostApplicationLifetime>();
        lifetime.ApplicationStopping.Returns(CancellationToken.None);
        var fetch = new FakeFetch();
        var sut = new CbmsHouseholdCache(
            hybrid, hasher, lifetime, NullLoggerFactory.Instance,
            Options.Create(new CbmsHouseholdCacheOptions()),
            fetch.Delegate);

        // Should not throw even though the underlying SetAsync throws.
        await sut.SetAsync(Phone, Populated(), CancellationToken.None);

        Assert.True(hybrid.RemoveCalled);
    }

    private sealed class ThrowingHybridCache : HybridCache
    {
        public bool RemoveCalled { get; private set; }

        public override ValueTask<T> GetOrCreateAsync<TState, T>(
            string key, TState state,
            Func<TState, CancellationToken, ValueTask<T>> factory,
            HybridCacheEntryOptions? options = null,
            IEnumerable<string>? tags = null,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public override ValueTask SetAsync<T>(
            string key, T value, HybridCacheEntryOptions? options = null,
            IEnumerable<string>? tags = null,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Simulated cache write failure");

        public override ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            RemoveCalled = true;
            return ValueTask.CompletedTask;
        }

        public override ValueTask RemoveByTagAsync(string tag, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;
    }
}
```

- [ ] **Step 2 — Run; expect FAIL (NotImplementedException for Set/Invalidate)**

- [ ] **Step 3 — Implement Set/Invalidate**

Replace the throwing placeholders with:

```csharp
public async Task SetAsync(string normalizedPhone, GetAccountDetailsResponse value, CancellationToken cancellationToken)
{
    ArgumentNullException.ThrowIfNull(value);
    var key = KeyFor(normalizedPhone);
    var now = DateTimeOffset.UtcNow;
    var envelope = new CbmsHouseholdCacheEnvelope(
        Response: value,
        SoftExpiryUtc: now + _options.SoftExpiration,
        HardExpiryUtc: now + _options.HardExpiration,
        CachedAtUtc: now);

    try
    {
        await _hybridCache.SetAsync(
            key, envelope,
            new HybridCacheEntryOptions { Expiration = _options.HardExpiration },
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "Cache write-through failed for {Prefix}; invalidating to force re-fetch", KeyPrefix + "*");
        try
        {
            await InvalidateAsync(normalizedPhone, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception inner)
        {
            // Best-effort secondary; if Invalidate also fails, log and move on.
            _logger.LogWarning(inner, "Cache invalidation also failed during tripwire");
        }
    }
}

public Task InvalidateAsync(string normalizedPhone, CancellationToken cancellationToken)
{
    var key = KeyFor(normalizedPhone);
    return _hybridCache.RemoveAsync(key, cancellationToken).AsTask();
}
```

- [ ] **Step 4 — Run; expect PASS**

- [ ] **Step 5 — Commit**

```bash
git -C /Users/jblair/Projects/SEBT/sebt-self-service-portal-co-connector/.worktrees/feature/co-cbms-household-cache add \
  src/SEBT.Portal.StatePlugins.CO/Cbms/Cache/CbmsHouseholdCache.cs \
  src/SEBT.Portal.StatePlugins.CO.Tests/Cbms/Cache/CbmsHouseholdCacheWriteThroughTests.cs

git -C /Users/jblair/Projects/SEBT/sebt-self-service-portal-co-connector/.worktrees/feature/co-cbms-household-cache commit -m "feat(co-cache): write-through SetAsync with tripwire on cache failure"
```

### Task D4: Background refresh tests

**Files:**
- Create: `src/SEBT.Portal.StatePlugins.CO.Tests/Cbms/Cache/CbmsHouseholdCacheBackgroundRefreshTests.cs`

The cache implementation already supports background refresh from D1; this task adds tests that validate the SWR contract.

- [ ] **Step 1 — Add the tests**

```csharp
// src/SEBT.Portal.StatePlugins.CO.Tests/Cbms/Cache/CbmsHouseholdCacheBackgroundRefreshTests.cs
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using SEBT.Portal.StatePlugins.CO.Cbms.Cache;
using SEBT.Portal.StatePlugins.CO.CbmsApi.Models;
using SEBT.Portal.StatesPlugins.Interfaces.Services;

namespace SEBT.Portal.StatePlugins.CO.Tests.Cbms.Cache;

public class CbmsHouseholdCacheBackgroundRefreshTests
{
    private const string Phone = "3035550199";

    private static GetAccountDetailsResponse Populated(string marker = "v1") => new()
    {
        StdntEnrollDtls = new() { new StdntEnrollDtl { GurdFstNm = marker } }
    };

    [Fact]
    public async Task Stale_read_returns_cached_value_immediately()
    {
        var hybrid = new InMemoryHybridCache();
        var hasher = Substitute.For<IHMACSHA256Hasher>();
        hasher.Hash(Arg.Any<string>()).Returns("hash");
        var lifetime = Substitute.For<IHostApplicationLifetime>();
        lifetime.ApplicationStopping.Returns(CancellationToken.None);
        var fetch = new FakeFetch { NextResponse = Populated("v1") };
        var sut = new CbmsHouseholdCache(
            hybrid, hasher, lifetime, NullLoggerFactory.Instance,
            // Soft = 0 makes every read past soft-boundary
            Options.Create(new CbmsHouseholdCacheOptions { SoftExpirationMinutes = 1, HardExpirationMinutes = 240 }),
            fetch.Delegate);

        // Prime the cache with a stale envelope.
        await sut.SetAsync(Phone, Populated("v1"), CancellationToken.None);
        // Manually mutate the envelope to be past soft expiry.
        // (We use the InMemoryHybridCache's accessor — TryGet returns the envelope by reference.)
        Assert.True(hybrid.TryGet<CbmsHouseholdCacheEnvelope?>("co:cbms:hash", out var envelope));
        Assert.NotNull(envelope);
        var staleEnvelope = envelope! with
        {
            SoftExpiryUtc = DateTimeOffset.UtcNow.AddMinutes(-5),
            HardExpiryUtc = DateTimeOffset.UtcNow.AddHours(2)
        };
        await hybrid.SetAsync("co:cbms:hash", staleEnvelope);

        fetch.NextResponse = Populated("v2");
        fetch.CallCount = 0;
        var result = await sut.GetAsync(Phone, CancellationToken.None);

        // Stale read returns v1 immediately
        Assert.NotNull(result);
        Assert.Equal("v1", result!.StdntEnrollDtls![0].GurdFstNm);
    }

    [Fact]
    public async Task Stale_read_triggers_background_refresh()
    {
        var hybrid = new InMemoryHybridCache();
        var hasher = Substitute.For<IHMACSHA256Hasher>();
        hasher.Hash(Arg.Any<string>()).Returns("hash");
        var lifetime = Substitute.For<IHostApplicationLifetime>();
        lifetime.ApplicationStopping.Returns(CancellationToken.None);
        var fetch = new FakeFetch();
        var sut = new CbmsHouseholdCache(
            hybrid, hasher, lifetime, NullLoggerFactory.Instance,
            Options.Create(new CbmsHouseholdCacheOptions { SoftExpirationMinutes = 1, HardExpirationMinutes = 240 }),
            fetch.Delegate);

        await sut.SetAsync(Phone, Populated("v1"), CancellationToken.None);
        Assert.True(hybrid.TryGet<CbmsHouseholdCacheEnvelope?>("co:cbms:hash", out var envelope));
        var staleEnvelope = envelope! with
        {
            SoftExpiryUtc = DateTimeOffset.UtcNow.AddMinutes(-5)
        };
        await hybrid.SetAsync("co:cbms:hash", staleEnvelope);

        fetch.NextResponse = Populated("v2");
        fetch.CallCount = 0;

        await sut.GetAsync(Phone, CancellationToken.None);

        // Wait briefly for background task to complete.
        var deadline = DateTimeOffset.UtcNow.AddSeconds(2);
        while (fetch.CallCount == 0 && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(20);
        }

        Assert.Equal(1, fetch.CallCount);
    }

    [Fact]
    public async Task Background_refresh_failure_keeps_stale_envelope()
    {
        var hybrid = new InMemoryHybridCache();
        var hasher = Substitute.For<IHMACSHA256Hasher>();
        hasher.Hash(Arg.Any<string>()).Returns("hash");
        var lifetime = Substitute.For<IHostApplicationLifetime>();
        lifetime.ApplicationStopping.Returns(CancellationToken.None);
        var failingFetch = new FakeFetch { ThrowOnNext = true };
        var sut = new CbmsHouseholdCache(
            hybrid, hasher, lifetime, NullLoggerFactory.Instance,
            Options.Create(new CbmsHouseholdCacheOptions { SoftExpirationMinutes = 1, HardExpirationMinutes = 240 }),
            failingFetch.Delegate);

        await sut.SetAsync(Phone, Populated("v1"), CancellationToken.None);
        Assert.True(hybrid.TryGet<CbmsHouseholdCacheEnvelope?>("co:cbms:hash", out var envelope));
        var staleEnvelope = envelope! with { SoftExpiryUtc = DateTimeOffset.UtcNow.AddMinutes(-5) };
        await hybrid.SetAsync("co:cbms:hash", staleEnvelope);

        failingFetch.CallCount = 0;
        var result = await sut.GetAsync(Phone, CancellationToken.None);

        // Stale value still returned synchronously.
        Assert.Equal("v1", result!.StdntEnrollDtls![0].GurdFstNm);

        await Task.Delay(100); // let background refresh attempt happen

        // Cache still has v1 (refresh failed).
        Assert.True(hybrid.TryGet<CbmsHouseholdCacheEnvelope?>("co:cbms:hash", out var stillThere));
        Assert.NotNull(stillThere);
        Assert.Equal("v1", stillThere!.Response.StdntEnrollDtls![0].GurdFstNm);
    }
}
```

Update `FakeFetch` to support throwing:

```csharp
internal sealed class FakeFetch
{
    public int CallCount { get; set; }
    public GetAccountDetailsResponse? NextResponse { get; set; }
    public bool ThrowOnNext { get; set; }

    public CbmsFetchAccountDetailsDelegate Delegate => (_, _) =>
    {
        CallCount++;
        if (ThrowOnNext) throw new InvalidOperationException("Simulated CBMS failure");
        return Task.FromResult(NextResponse);
    };
}
```

(Edit `CbmsHouseholdCacheReadTests.cs` to match the updated `FakeFetch`.)

- [ ] **Step 2 — Run all cache tests; expect PASS**

```bash
dotnet test --filter "FullyQualifiedName~CbmsHouseholdCache"
```

- [ ] **Step 3 — Commit**

```bash
git -C /Users/jblair/Projects/SEBT/sebt-self-service-portal-co-connector/.worktrees/feature/co-cbms-household-cache add \
  src/SEBT.Portal.StatePlugins.CO.Tests/Cbms/Cache/CbmsHouseholdCacheBackgroundRefreshTests.cs \
  src/SEBT.Portal.StatePlugins.CO.Tests/Cbms/Cache/CbmsHouseholdCacheReadTests.cs

git -C /Users/jblair/Projects/SEBT/sebt-self-service-portal-co-connector/.worktrees/feature/co-cbms-household-cache commit -m "test(co-cache): background refresh tests (SWR contract)"
```

### Task D5: Stampede coalescing tests

**Files:**
- Create: `src/SEBT.Portal.StatePlugins.CO.Tests/Cbms/Cache/CbmsHouseholdCacheStampedeTests.cs`

- [ ] **Step 1 — Add the tests**

```csharp
// src/SEBT.Portal.StatePlugins.CO.Tests/Cbms/Cache/CbmsHouseholdCacheStampedeTests.cs
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using SEBT.Portal.StatePlugins.CO.Cbms.Cache;
using SEBT.Portal.StatePlugins.CO.CbmsApi.Models;
using SEBT.Portal.StatesPlugins.Interfaces.Services;

namespace SEBT.Portal.StatePlugins.CO.Tests.Cbms.Cache;

public class CbmsHouseholdCacheStampedeTests
{
    private const string Phone = "3035550199";

    [Fact]
    public async Task Concurrent_stale_reads_trigger_at_most_one_background_refresh()
    {
        var hybrid = new InMemoryHybridCache();
        var hasher = Substitute.For<IHMACSHA256Hasher>();
        hasher.Hash(Arg.Any<string>()).Returns("hash");
        var lifetime = Substitute.For<IHostApplicationLifetime>();
        lifetime.ApplicationStopping.Returns(CancellationToken.None);

        var slowFetchTcs = new TaskCompletionSource<GetAccountDetailsResponse?>();
        var fetchCalls = 0;
        CbmsFetchAccountDetailsDelegate fetch = async (_, _) =>
        {
            Interlocked.Increment(ref fetchCalls);
            return await slowFetchTcs.Task;
        };

        var sut = new CbmsHouseholdCache(
            hybrid, hasher, lifetime, NullLoggerFactory.Instance,
            Options.Create(new CbmsHouseholdCacheOptions { SoftExpirationMinutes = 1, HardExpirationMinutes = 240 }),
            fetch);

        // Prime cache with a stale envelope.
        await sut.SetAsync(Phone, new GetAccountDetailsResponse { StdntEnrollDtls = new() { new() } }, CancellationToken.None);
        Assert.True(hybrid.TryGet<CbmsHouseholdCacheEnvelope?>("co:cbms:hash", out var envelope));
        var staleEnvelope = envelope! with { SoftExpiryUtc = DateTimeOffset.UtcNow.AddMinutes(-5) };
        await hybrid.SetAsync("co:cbms:hash", staleEnvelope);

        // Fire 50 concurrent reads; each should return the stale envelope and (collectively) trigger only 1 fetch.
        var tasks = Enumerable.Range(0, 50)
            .Select(_ => sut.GetAsync(Phone, CancellationToken.None))
            .ToArray();
        await Task.WhenAll(tasks);

        // Give the fire-and-forget task a moment to invoke the fetch.
        var deadline = DateTimeOffset.UtcNow.AddMilliseconds(500);
        while (fetchCalls == 0 && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(10);
        }

        Assert.Equal(1, fetchCalls);

        // Release the slow fetch so the test cleans up.
        slowFetchTcs.SetResult(new GetAccountDetailsResponse { StdntEnrollDtls = new() { new() } });
    }

    [Fact]
    public async Task After_background_refresh_completes_in_flight_entry_is_removed()
    {
        // Ensures a subsequent stale read can trigger a fresh in-flight task,
        // which would not happen if _inFlightRefreshes leaked entries.
        var hybrid = new InMemoryHybridCache();
        var hasher = Substitute.For<IHMACSHA256Hasher>();
        hasher.Hash(Arg.Any<string>()).Returns("hash");
        var lifetime = Substitute.For<IHostApplicationLifetime>();
        lifetime.ApplicationStopping.Returns(CancellationToken.None);
        var fetch = new FakeFetch { NextResponse = new GetAccountDetailsResponse { StdntEnrollDtls = new() { new() } } };
        var sut = new CbmsHouseholdCache(
            hybrid, hasher, lifetime, NullLoggerFactory.Instance,
            Options.Create(new CbmsHouseholdCacheOptions { SoftExpirationMinutes = 1, HardExpirationMinutes = 240 }),
            fetch.Delegate);

        await sut.SetAsync(Phone, new GetAccountDetailsResponse { StdntEnrollDtls = new() { new() } }, CancellationToken.None);

        for (int round = 0; round < 3; round++)
        {
            // Re-stale before each round
            Assert.True(hybrid.TryGet<CbmsHouseholdCacheEnvelope?>("co:cbms:hash", out var envelope));
            var staleEnvelope = envelope! with { SoftExpiryUtc = DateTimeOffset.UtcNow.AddMinutes(-5) };
            await hybrid.SetAsync("co:cbms:hash", staleEnvelope);

            var beforeCalls = fetch.CallCount;
            await sut.GetAsync(Phone, CancellationToken.None);

            var deadline = DateTimeOffset.UtcNow.AddMilliseconds(500);
            while (fetch.CallCount == beforeCalls && DateTimeOffset.UtcNow < deadline)
            {
                await Task.Delay(10);
            }
            Assert.Equal(beforeCalls + 1, fetch.CallCount);
        }
    }
}
```

- [ ] **Step 2 — Run; expect PASS**

```bash
dotnet test --filter "FullyQualifiedName~CbmsHouseholdCacheStampedeTests"
```

- [ ] **Step 3 — Commit**

```bash
git -C /Users/jblair/Projects/SEBT/sebt-self-service-portal-co-connector/.worktrees/feature/co-cbms-household-cache add \
  src/SEBT.Portal.StatePlugins.CO.Tests/Cbms/Cache/CbmsHouseholdCacheStampedeTests.cs

git -C /Users/jblair/Projects/SEBT/sebt-self-service-portal-co-connector/.worktrees/feature/co-cbms-household-cache commit -m "test(co-cache): in-flight refresh coalescing under concurrent reads"
```

---

## Phase E — CO connector: plugin wiring

### Task E1: Add `PluginCache` static class

**Files:**
- Create: `src/SEBT.Portal.StatePlugins.CO/Cbms/Cache/PluginCache.cs`
- Create: `src/SEBT.Portal.StatePlugins.CO.Tests/Cbms/Cache/PluginCacheTests.cs`

- [ ] **Step 1 — Failing tests**

```csharp
// src/SEBT.Portal.StatePlugins.CO.Tests/Cbms/Cache/PluginCacheTests.cs
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SEBT.Portal.StatePlugins.CO.Cbms.Cache;
using SEBT.Portal.StatePlugins.CO.CbmsApi.Models;
using SEBT.Portal.StatesPlugins.Interfaces.Services;

namespace SEBT.Portal.StatePlugins.CO.Tests.Cbms.Cache;

public class PluginCacheTests : IDisposable
{
    public PluginCacheTests() => PluginCache.ResetForTesting();
    public void Dispose() => PluginCache.ResetForTesting();

    private static IServiceProvider BuildHostProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMemoryCache();
        services.AddHybridCache();
        services.AddSingleton<IHostApplicationLifetime>(_ => Substitute.For<IHostApplicationLifetime>());
        var hasher = Substitute.For<IHMACSHA256Hasher>();
        hasher.Hash(Arg.Any<string>()).Returns(c => "h:" + c.Arg<string>());
        services.AddSingleton(hasher);
        return services.BuildServiceProvider();
    }

    private static IConfiguration BuildConfig() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Cbms:UseMockResponses"] = "true",
            ["Cbms:Cache:SoftExpirationMinutes"] = "5",
            ["Cbms:Cache:HardExpirationMinutes"] = "60",
        })
        .Build();

    [Fact]
    public void GetOrBuild_returns_singleton_across_calls()
    {
        var host = BuildHostProvider();
        var config = BuildConfig();

        var a = PluginCache.GetOrBuild(host, config);
        var b = PluginCache.GetOrBuild(host, config);

        Assert.Same(a, b);
    }

    [Fact]
    public void OverrideForTesting_replaces_instance()
    {
        var fake = Substitute.For<ICbmsHouseholdCache>();
        PluginCache.OverrideForTesting(fake);

        var host = BuildHostProvider();
        var resolved = PluginCache.GetOrBuild(host, BuildConfig());

        Assert.Same(fake, resolved);
    }

    [Fact]
    public void ResetForTesting_clears_instance()
    {
        var host = BuildHostProvider();
        var config = BuildConfig();

        var a = PluginCache.GetOrBuild(host, config);
        PluginCache.ResetForTesting();
        var b = PluginCache.GetOrBuild(host, config);

        Assert.NotSame(a, b);
    }
}
```

- [ ] **Step 2 — Run; expect FAIL**

- [ ] **Step 3 — Implement `PluginCache`**

```csharp
// src/SEBT.Portal.StatePlugins.CO/Cbms/Cache/PluginCache.cs
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SEBT.Portal.StatePlugins.CO.CbmsApi;
using SEBT.Portal.StatePlugins.CO.CbmsApi.Models;
using SEBT.Portal.StatePlugins.CO.CbmsApi.Mocks;

namespace SEBT.Portal.StatePlugins.CO.Cbms.Cache;

/// <summary>
/// Process-wide singleton holder for <see cref="ICbmsHouseholdCache"/>.
/// Constructed lazily on first plugin construction using the host service provider
/// for shared services (HybridCache, IHMACSHA256Hasher, IHostApplicationLifetime,
/// ILoggerFactory) and explicit args for plugin-internal options + the CBMS fetch
/// function. Tests substitute a fake via <see cref="OverrideForTesting"/>.
/// </summary>
internal static class PluginCache
{
    private static ICbmsHouseholdCache? _instance;
    private static readonly object _lock = new();

    public static ICbmsHouseholdCache GetOrBuild(IServiceProvider hostProvider, IConfiguration configuration)
    {
        if (_instance is not null) return _instance;
        lock (_lock)
        {
            if (_instance is not null) return _instance;

            var cacheOptions = new CbmsHouseholdCacheOptions();
            configuration.GetSection("Cbms:Cache").Bind(cacheOptions);
            foreach (var error in cacheOptions.Validate())
            {
                throw new InvalidOperationException("Cbms:Cache configuration invalid: " + error);
            }

            // Build the CBMS fetch function once.
            var cbmsConnection = CbmsOptionsHelper.GetCbmsOptions(configuration);
            var loggerFactory = hostProvider.GetRequiredService<ILoggerFactory>();
            var cbmsLogger = loggerFactory.CreateLogger("CbmsHouseholdCache.CbmsClient");

            HttpMessageHandler? handler = null;
            if (cbmsConnection.UseMockResponses)
            {
                var hybridCache = hostProvider.GetRequiredService<Microsoft.Extensions.Caching.Hybrid.HybridCache>();
                var dataStore = new MockCbmsDataStore(hybridCache);
                handler = new MockCbmsHttpHandler(dataStore);
            }

            var cbmsClient = CbmsSebtApiClientFactory.Create(
                cbmsConnection.UseMockResponses ? "mock-client-id" : cbmsConnection.ClientId,
                cbmsConnection.UseMockResponses ? "mock-client-secret" : cbmsConnection.ClientSecret,
                cbmsConnection.ApiBaseUrl, cbmsConnection.TokenEndpointUrl,
                handler, cbmsLogger);

            CbmsFetchAccountDetailsDelegate fetchFromCbms = async (phone, ct) =>
            {
                try
                {
                    var request = new GetAccountDetailsRequest { PhnNm = phone };
                    return await cbmsClient.Sebt.GetAccountDetails.PostAsync(request, cancellationToken: ct);
                }
                catch (ErrorResponse ex) when (ex.ResponseStatusCode == 404)
                {
                    return null;
                }
            };

            _instance = ActivatorUtilities.CreateInstance<CbmsHouseholdCache>(
                hostProvider,
                Options.Create(cacheOptions),
                fetchFromCbms);

            return _instance;
        }
    }

    internal static void OverrideForTesting(ICbmsHouseholdCache testCache)
    {
        lock (_lock) { _instance = testCache; }
    }

    internal static void ResetForTesting()
    {
        lock (_lock) { _instance = null; }
    }
}
```

- [ ] **Step 4 — Run; expect PASS**

- [ ] **Step 5 — Commit**

```bash
git -C /Users/jblair/Projects/SEBT/sebt-self-service-portal-co-connector/.worktrees/feature/co-cbms-household-cache add \
  src/SEBT.Portal.StatePlugins.CO/Cbms/Cache/PluginCache.cs \
  src/SEBT.Portal.StatePlugins.CO.Tests/Cbms/Cache/PluginCacheTests.cs

git -C /Users/jblair/Projects/SEBT/sebt-self-service-portal-co-connector/.worktrees/feature/co-cbms-household-cache commit -m "feat(co-cache): add PluginCache lazy singleton with test override hooks"
```

### Task E2: Refactor `ColoradoCbmsServiceBase`

**Files:**
- Modify: `src/SEBT.Portal.StatePlugins.CO/ColoradoCbmsServiceBase.cs`

The base class today takes `HybridCache?` and `ILogger`, and exposes `GetOrCreateClient(options)`. We add an `IServiceProvider hostProvider` and `IConfiguration configuration` constructor parameter, then expose `HouseholdCache`. The existing `GetOrCreateClient` logic stays — it's used by the *write* paths (PATCH calls), which still want a client per `CbmsConnectionOptions`.

- [ ] **Step 1 — Modify the base class**

```csharp
// src/SEBT.Portal.StatePlugins.CO/ColoradoCbmsServiceBase.cs
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SEBT.Portal.StatePlugins.CO.Cbms;
using SEBT.Portal.StatePlugins.CO.Cbms.Cache;
using SEBT.Portal.StatePlugins.CO.CbmsApi;
using SEBT.Portal.StatePlugins.CO.CbmsApi.Mocks;

namespace SEBT.Portal.StatePlugins.CO;

public abstract class ColoradoCbmsServiceBase
{
    private readonly object _clientCacheLock = new();
    private readonly HybridCache? _cache;
    private readonly ILogger _logger;

    private CbmsConnectionOptions? _cachedOptions;
    private CbmsSebtApiClient? _cachedClient;

    /// <summary>
    /// The plugin-wide household-cache singleton. Read paths use this; write paths
    /// also read from it before PATCHing and (for address updates) write through.
    /// </summary>
    protected ICbmsHouseholdCache HouseholdCache { get; }

    protected ColoradoCbmsServiceBase(
        IServiceProvider hostProvider,
        IConfiguration configuration,
        HybridCache? cache,
        ILogger logger)
    {
        _cache = cache;
        _logger = logger;
        HouseholdCache = PluginCache.GetOrBuild(hostProvider, configuration);
    }

    protected CbmsSebtApiClient GetOrCreateClient(CbmsConnectionOptions options)
    {
        lock (_clientCacheLock)
        {
            if (_cachedOptions == options && _cachedClient != null)
                return _cachedClient;

            HttpMessageHandler? handler = null;
            var clientId = options.ClientId;
            var clientSecret = options.ClientSecret;

            if (options.UseMockResponses)
            {
                if (_cache == null)
                {
                    throw new InvalidOperationException(
                        "HybridCache must be registered in DI when Cbms:UseMockResponses is enabled. " +
                        "Ensure services.AddHybridCache() is called during service registration.");
                }

                clientId = "mock-client-id";
                clientSecret = "mock-client-secret";
                var dataStore = new MockCbmsDataStore(_cache);
                handler = new MockCbmsHttpHandler(dataStore);
            }

            _cachedClient = CbmsSebtApiClientFactory.Create(
                clientId, clientSecret,
                options.ApiBaseUrl, options.TokenEndpointUrl,
                handler, _logger);
            _cachedOptions = options;
            return _cachedClient;
        }
    }
}
```

- [ ] **Step 2 — Build (expects compile errors in derived classes)**

```bash
dotnet build src/SEBT.Portal.StatePlugins.CO/SEBT.Portal.StatePlugins.CO.csproj 2>&1 | head -30
```

The three Colorado* services will fail to compile until E3–E5 update them.

- [ ] **Step 3 — Commit (combined with E3 once green; do not push partial)**

Hold off committing until E3 is done — keep build green commits.

### Task E3: Refactor `ColoradoSummerEbtCaseService`

**Files:**
- Modify: `src/SEBT.Portal.StatePlugins.CO/ColoradoSummerEbtCaseService.cs`
- Modify: `src/SEBT.Portal.StatePlugins.CO.Tests/ColoradoSummerEbtCaseServiceTests.cs`

- [ ] **Step 1 — Modify the service to use the cache**

Replace the `GetHouseholdByPhoneAsync` method's direct CBMS call with a cache call:

```csharp
private async Task<HouseholdData?> GetHouseholdByPhoneAsync(
    string phoneNumber,
    PiiVisibility piiVisibility,
    CancellationToken cancellationToken)
{
    var options = CbmsOptionsHelper.GetCbmsOptions(_configuration);
    if (!options.IsConfigured) return null;

    var normalizedPhone = PhoneNormalizer.Normalize(phoneNumber);
    if (string.IsNullOrEmpty(normalizedPhone)) return null;

    GetAccountDetailsResponse? response;
    try
    {
        response = await HouseholdCache.GetAsync(normalizedPhone, cancellationToken).ConfigureAwait(false);
    }
    catch (ErrorResponse ex)
    {
        _logger.LogWarning(ex, "CBMS GetAccountDetails (via cache) failed: {StatusCode}", ex.ResponseStatusCode);
        throw;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "CBMS GetAccountDetails (via cache) failed for phone lookup");
        throw;
    }

    if (response is null || response.StdntEnrollDtls is null || response.StdntEnrollDtls.Count == 0)
        return null;

    return CbmsResponseMapper.MapToHouseholdData(response, normalizedPhone, piiVisibility, _logger);
}
```

Update the constructor signature to match the new base:

```csharp
[ImportingConstructor]
public ColoradoSummerEbtCaseService(
    [Import] IServiceProvider hostProvider,
    [Import] IConfiguration configuration,
    [Import] ILoggerFactory loggerFactory,
    HybridCache? cache = null)
    : base(hostProvider, configuration, cache, loggerFactory.CreateLogger<ColoradoSummerEbtCaseService>())
{
    ArgumentNullException.ThrowIfNull(configuration);
    ArgumentNullException.ThrowIfNull(loggerFactory);

    _configuration = configuration;
    _logger = loggerFactory.CreateLogger<ColoradoSummerEbtCaseService>();
}
```

- [ ] **Step 2 — Update existing tests**

The test class needs to override the cache via `PluginCache.OverrideForTesting`. Convert the test class to use `IDisposable`:

```csharp
public class ColoradoSummerEbtCaseServiceTests : IDisposable
{
    public ColoradoSummerEbtCaseServiceTests() => PluginCache.ResetForTesting();
    public void Dispose() => PluginCache.ResetForTesting();

    [Fact]
    public async Task GetHouseholdByPhoneAsync_returns_data_when_cache_returns_response()
    {
        var fakeCache = Substitute.For<ICbmsHouseholdCache>();
        fakeCache.GetAsync("3035550199", Arg.Any<CancellationToken>())
            .Returns(new GetAccountDetailsResponse { StdntEnrollDtls = new() { /* … */ } });
        PluginCache.OverrideForTesting(fakeCache);

        var sut = ConstructServiceUnderTest(/* ... */);
        var result = await sut.GetHouseholdByIdentifierAsync(
            HouseholdIdentifierType.Phone, "303-555-0199",
            PiiVisibility.MaskedDefaults, IdentityAssuranceLevel.IAL2, CancellationToken.None);

        Assert.NotNull(result);
        await fakeCache.Received(1).GetAsync("3035550199", Arg.Any<CancellationToken>());
    }
}
```

Use the existing `ColoradoSummerEbtCaseServiceTests.cs` constructor pattern as a starting point.

- [ ] **Step 3 — Build, run cache + service tests**

```bash
dotnet build
dotnet test --filter "FullyQualifiedName~ColoradoSummerEbtCaseServiceTests|FullyQualifiedName~CbmsHouseholdCache"
```

Expected: PASS.

- [ ] **Step 4 — Commit (E2 + E3 together)**

```bash
git -C /Users/jblair/Projects/SEBT/sebt-self-service-portal-co-connector/.worktrees/feature/co-cbms-household-cache add \
  src/SEBT.Portal.StatePlugins.CO/ColoradoCbmsServiceBase.cs \
  src/SEBT.Portal.StatePlugins.CO/ColoradoSummerEbtCaseService.cs \
  src/SEBT.Portal.StatePlugins.CO.Tests/ColoradoSummerEbtCaseServiceTests.cs

git -C /Users/jblair/Projects/SEBT/sebt-self-service-portal-co-connector/.worktrees/feature/co-cbms-household-cache commit -m "refactor(co): route ColoradoSummerEbtCaseService reads through household cache"
```

### Task E4: Refactor `ColoradoAddressUpdateService` (with write-through)

**Files:**
- Modify: `src/SEBT.Portal.StatePlugins.CO/ColoradoAddressUpdateService.cs`
- Modify: `src/SEBT.Portal.StatePlugins.CO.Tests/ColoradoAddressUpdateServiceTests.cs`

- [ ] **Step 1 — Inherit from `ColoradoCbmsServiceBase`; remove duplicated client cache**

Replace the local `_cachedOptions`/`_cachedClient`/`_clientCacheLock`/`GetOrCreateClient` with inheritance from the base. Update the constructor:

```csharp
[ImportingConstructor]
public ColoradoAddressUpdateService(
    [Import] IServiceProvider hostProvider,
    [Import(AllowDefault = true)] IConfiguration? configuration = null,
    [Import(AllowDefault = true)] ILoggerFactory? loggerFactory = null,
    HybridCache? cache = null)
    : base(
        hostProvider,
        configuration ?? throw new InvalidOperationException("IConfiguration required."),
        cache,
        loggerFactory?.CreateLogger<ColoradoAddressUpdateService>() ?? NullLogger<ColoradoAddressUpdateService>.Instance)
{
    _configuration = configuration;
    _logger = loggerFactory?.CreateLogger<ColoradoAddressUpdateService>() ?? NullLogger<ColoradoAddressUpdateService>.Instance;
}

internal ColoradoAddressUpdateService(
    IServiceProvider hostProvider,
    IConfiguration? configuration,
    HttpMessageHandler? testHttpMessageHandler,
    HybridCache? cache,
    ILogger? logger = null)
    : base(
        hostProvider,
        configuration ?? throw new InvalidOperationException("IConfiguration required."),
        cache,
        logger ?? NullLogger<ColoradoAddressUpdateService>.Instance)
{
    _configuration = configuration;
    _testHttpMessageHandler = testHttpMessageHandler;
    _logger = logger ?? NullLogger<ColoradoAddressUpdateService>.Instance;
}
```

- [ ] **Step 2 — Replace the read leg with `HouseholdCache.GetAsync`**

In `UpdateAddressAsync`, replace the `client.Sebt.GetAccountDetails.PostAsync` call with:

```csharp
GetAccountDetailsResponse? accountResponse;
try
{
    accountResponse = await HouseholdCache.GetAsync(phone10, cancellationToken).ConfigureAwait(false);
}
catch (ErrorResponse ex)
{
    _logger.LogWarning("CBMS AddressUpdate: get-account-details (cache) failed StatusCode={StatusCode}", ex.ResponseStatusCode);
    return MapErrorResponse(ex);
}
catch (ApiException ex)
{
    _logger.LogWarning("CBMS AddressUpdate: get-account-details (cache) failed HTTP {StatusCode}", ex.ResponseStatusCode);
    return BackendErrorFromApiException(ex);
}

if (accountResponse is null)
{
    return AddressUpdateResult.PolicyRejected(
        "HOUSEHOLD_NOT_FOUND",
        "CBMS get-account-details returned no enrollment rows for the household identifier.");
}
```

- [ ] **Step 3 — Add write-through after successful PATCH**

After the PATCH success block (`return AddressUpdateResult.Success();`), but **before** returning, mutate the cached response in memory and write it back:

```csharp
if (updateResponse != null && IsCbmsUpdateSuccessCode(updateResponse.RespCd))
{
    // Write-through: update the cached response so subsequent reads reflect the change.
    foreach (var (row, _) in actionable)
    {
        CbmsAddressUpdateMapper.ApplyAddressToRow(request.Address, row);
    }
    await HouseholdCache.SetAsync(phone10, accountResponse, cancellationToken).ConfigureAwait(false);

    return AddressUpdateResult.Success();
}
```

You'll need a small helper on `CbmsAddressUpdateMapper`:

```csharp
// In CbmsAddressUpdateMapper.cs, add:
internal static void ApplyAddressToRow(Address address, StdntEnrollDtl row)
{
    row.AddrLn1 = address.Line1;
    row.AddrLn2 = address.Line2 ?? string.Empty;
    row.Cty = address.City;
    row.StaCd = address.StateCode;
    row.Zip = address.ZipCode;
    row.Zip4 = address.ZipPlus4 ?? string.Empty;
}
```

- [ ] **Step 4 — Update tests**

Modify `ColoradoAddressUpdateServiceTests.cs`:
- Add `IDisposable` with `PluginCache.Reset/OverrideForTesting`
- New test: PATCH success → `HouseholdCache.SetAsync` called with mutated response
- New test: PATCH failure → `HouseholdCache.SetAsync` NOT called

```csharp
[Fact]
public async Task UpdateAddressAsync_writes_through_to_cache_on_PATCH_success()
{
    var fakeCache = Substitute.For<ICbmsHouseholdCache>();
    var existingResponse = BuildAccountDetailsWithOneStudent();
    fakeCache.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(existingResponse);
    PluginCache.OverrideForTesting(fakeCache);

    var sut = ConstructWithMockHandler(/* PATCH returns respCd 00 */);
    var result = await sut.UpdateAddressAsync(BuildRequest(/* new address */), CancellationToken.None);

    Assert.True(result.IsSuccess);
    await fakeCache.Received(1).SetAsync(
        Arg.Any<string>(),
        Arg.Is<GetAccountDetailsResponse>(r => r.StdntEnrollDtls![0].AddrLn1 == "456 New St"),
        Arg.Any<CancellationToken>());
}

[Fact]
public async Task UpdateAddressAsync_does_not_write_through_on_PATCH_failure()
{
    var fakeCache = Substitute.For<ICbmsHouseholdCache>();
    fakeCache.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(BuildAccountDetailsWithOneStudent());
    PluginCache.OverrideForTesting(fakeCache);

    var sut = ConstructWithMockHandler(/* PATCH returns respCd 99 */);
    var result = await sut.UpdateAddressAsync(BuildRequest(), CancellationToken.None);

    Assert.False(result.IsSuccess);
    await fakeCache.DidNotReceive().SetAsync(Arg.Any<string>(), Arg.Any<GetAccountDetailsResponse>(), Arg.Any<CancellationToken>());
}
```

Use the existing test helpers in `ColoradoAddressUpdateServiceTests.cs` for `ConstructWithMockHandler` / `BuildAccountDetailsWithOneStudent` patterns; do not invent new ones.

- [ ] **Step 5 — Build and test**

```bash
dotnet build
dotnet test --filter "FullyQualifiedName~ColoradoAddressUpdateServiceTests"
```

Expected: PASS.

- [ ] **Step 6 — Commit**

```bash
git -C /Users/jblair/Projects/SEBT/sebt-self-service-portal-co-connector/.worktrees/feature/co-cbms-household-cache add \
  src/SEBT.Portal.StatePlugins.CO/ColoradoAddressUpdateService.cs \
  src/SEBT.Portal.StatePlugins.CO/Cbms/CbmsAddressUpdateMapper.cs \
  src/SEBT.Portal.StatePlugins.CO.Tests/ColoradoAddressUpdateServiceTests.cs

git -C /Users/jblair/Projects/SEBT/sebt-self-service-portal-co-connector/.worktrees/feature/co-cbms-household-cache commit -m "refactor(co): route ColoradoAddressUpdateService through cache with write-through"
```

### Task E5: Refactor `ColoradoCardReplacementService`

**Files:**
- Modify: `src/SEBT.Portal.StatePlugins.CO/ColoradoCardReplacementService.cs`
- Modify: `src/SEBT.Portal.StatePlugins.CO.Tests/ColoradoCardReplacementServiceTests.cs`

Same pattern as E4 but **no write-through**. The service inherits from the base, reads via `HouseholdCache.GetAsync`, PATCHes, and is done.

- [ ] **Step 1 — Refactor the service**

Inherit from the base, remove duplicated client logic, replace the read leg with `HouseholdCache.GetAsync`. Constructor signature mirrors `ColoradoAddressUpdateService` from E4.

- [ ] **Step 2 — Update tests**

Add `IDisposable` with `PluginCache.Reset/OverrideForTesting`. Add a test asserting that `SetAsync` is **not** called even on PATCH success:

```csharp
[Fact]
public async Task RequestCardReplacementAsync_does_not_write_through_to_cache()
{
    var fakeCache = Substitute.For<ICbmsHouseholdCache>();
    fakeCache.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
        .Returns(BuildAccountDetailsWithMatchingCwin("12345"));
    PluginCache.OverrideForTesting(fakeCache);

    var sut = ConstructWithMockHandler(/* PATCH returns respCd 00 */);
    var result = await sut.RequestCardReplacementAsync(BuildRequest("12345"), CancellationToken.None);

    Assert.True(result.IsSuccess);
    await fakeCache.DidNotReceive().SetAsync(Arg.Any<string>(), Arg.Any<GetAccountDetailsResponse>(), Arg.Any<CancellationToken>());
    await fakeCache.DidNotReceive().InvalidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
}
```

- [ ] **Step 3 — Build and test**

```bash
dotnet build
dotnet test
```

Expected: PASS, all suites green.

- [ ] **Step 4 — Commit**

```bash
git -C /Users/jblair/Projects/SEBT/sebt-self-service-portal-co-connector/.worktrees/feature/co-cbms-household-cache add \
  src/SEBT.Portal.StatePlugins.CO/ColoradoCardReplacementService.cs \
  src/SEBT.Portal.StatePlugins.CO.Tests/ColoradoCardReplacementServiceTests.cs

git -C /Users/jblair/Projects/SEBT/sebt-self-service-portal-co-connector/.worktrees/feature/co-cbms-household-cache commit -m "refactor(co): route ColoradoCardReplacementService through cache (read-only)"
```

---

## Phase F — Verification, ADR adjustment, PR prep

### Task F1: Full test suite

- [ ] **Step 1 — Run all CO connector tests**

```bash
cd /Users/jblair/Projects/SEBT/sebt-self-service-portal-co-connector/.worktrees/feature/co-cbms-household-cache
dotnet test
```

Expected: PASS, no failures.

- [ ] **Step 2 — Run main portal unit tests**

```bash
cd /Users/jblair/Projects/SEBT/sebt-self-service-portal/.worktrees/feature/co-cbms-household-cache
dotnet test test/SEBT.Portal.Tests/SEBT.Portal.Tests.csproj --filter "Category!=Integration&Category!=SqlServer&Category!=Socure"
dotnet test test/SEBT.Portal.UseCases.Tests/SEBT.Portal.UseCases.Tests.csproj
```

Expected: PASS.

- [ ] **Step 3 — Run state-connector tests**

```bash
cd /Users/jblair/Projects/SEBT/sebt-self-service-portal-state-connector/.worktrees/feature/co-cbms-household-cache
dotnet test
```

Expected: PASS.

### Task F2: Manual mock-mode smoke test

- [ ] **Step 1 — Build CO plugin (post-build target copies DLLs into the portal)**

```bash
cd /Users/jblair/Projects/SEBT/sebt-self-service-portal-co-connector/.worktrees/feature/co-cbms-household-cache
dotnet build
```

- [ ] **Step 2 — Start the portal in CO mode with mock responses**

In a separate terminal, from the *main* main-portal checkout (not the worktree — `pnpm dev:co` infrastructure assumes the main checkout):

```bash
cd /Users/jblair/Projects/SEBT/sebt-self-service-portal
pnpm dev:co
```

> If you'd rather not muddle the main checkout, use the main-portal worktree's `pnpm dev:co` — confirm the `plugins-co` dir is populated by the CO build's post-build target.

- [ ] **Step 3 — Hit the household endpoint twice with the same phone**

Use a tool like `curl` or the portal UI. Confirm:
- First request takes the slow CBMS-mock path (or normal latency for mock)
- Second request returns near-instantly (cache hit)
- Logs include `CbmsHouseholdCache` traces

- [ ] **Step 4 — Submit an address change**

Through the portal UI or a direct API call. Confirm:
- The redirect/confirmation page loads quickly (cache-hit read + fast PATCH)
- A subsequent fresh load of the household reflects the new address

### Task F3: Update the ADR for multi-repo footprint

The ADR currently says "encapsulated entirely inside the CO plugin." That's no longer strictly true now that `IHMACSHA256Hasher` lives in the contracts package and main portal implements it. Update the ADR's "Decision" and "Consequences" sections to reflect the actual shape.

- [ ] **Step 1 — Edit the ADR**

In the ADR file (already merged in PR #31, but we have a local copy in the CO worktree at `docs/adr/0004-cbms-response-caching.md`), adjust two sections:

In **Decision**:
> "Introduce an internal `ICbmsHouseholdCache` abstraction inside the CO plugin..."

Add a new bullet:
> "**`IHMACSHA256Hasher` primitive in the state-connector contracts package.** A new domain-agnostic HMAC-SHA256 hasher contract is added to the contracts package with a portal-side implementation (reusing `IdentifierHasher:SecretKey`). The CO plugin consumes it via DI for cache-key hashing. Future plugins can reuse the same primitive."

In **Consequences**:
> "**No state-connector contract change.** No NuGet bump..."

Replace with:
> "**Minimal cross-repo footprint.** The state-connector contracts package gains one new interface (`IHMACSHA256Hasher`); the main portal adds one implementation and one DI registration. DC connector is unaffected (it can ignore the new interface)."

- [ ] **Step 2 — Commit the ADR amendment**

```bash
git -C /Users/jblair/Projects/SEBT/sebt-self-service-portal-co-connector/.worktrees/feature/co-cbms-household-cache add \
  docs/adr/0004-cbms-response-caching.md

git -C /Users/jblair/Projects/SEBT/sebt-self-service-portal-co-connector/.worktrees/feature/co-cbms-household-cache commit -m "docs(adr): amend 0004 to reflect IHMACSHA256Hasher contract addition"
```

### Task F4: Push and open the CO connector PR

- [ ] **Step 1 — Push**

```bash
git -C /Users/jblair/Projects/SEBT/sebt-self-service-portal-co-connector/.worktrees/feature/co-cbms-household-cache push
```

(The branch is already tracking origin from PR #31; this just pushes the new commits.)

- [ ] **Step 2 — Update PR #31 description**

Use `gh pr edit 31 --body "$(cat <<'EOF' ... EOF)"` to expand the description to cover the implementation work and the multi-repo dependencies. Reference the state-connector and main-portal companion PRs.

- [ ] **Step 3 — Mark PR #31 ready for review (after companion PRs merge)**

```bash
cd /Users/jblair/Projects/SEBT/sebt-self-service-portal-co-connector/.worktrees/feature/co-cbms-household-cache
gh pr ready 31
```

> **Merge order:** state-connector PR → main-portal PR → CO connector PR (this one). Each depends on the prior NuGet/binary build.

---

## Self-Review

Run these checks before handing the plan to a subagent for execution:

1. **Spec coverage:** every component in the design spec maps to a Phase C/D/E task. The 5 test files (read, write-through, background refresh, stampede, key hashing) match the spec's test strategy.
2. **No placeholders:** every step has either explicit code or an explicit command.
3. **Type consistency:** `CbmsHouseholdCacheEnvelope`, `ICbmsHouseholdCache`, `CbmsHouseholdCacheOptions`, `CbmsFetchAccountDetailsDelegate`, `IHMACSHA256Hasher`, `PluginCache` are referenced consistently throughout.
4. **TDD discipline:** each new behavior follows red → green → commit. The cache scaffold (D1) is the one place where some methods are placeholder-thrown until D3 fills them in — explicitly noted.
5. **Multi-repo build order:** Phase A → Phase B → Phase C is enforced by NuGet dependency. Each phase is shippable on its own; later phases gracefully fall through if their dependency hasn't merged (e.g., contracts PR can land first).
6. **Worktree paths:** every git/dotnet command uses absolute paths or explicit `cd` to the right worktree. `git -C` is preferred where the prior shell state is uncertain.

If any item fails, fix inline before execution.
