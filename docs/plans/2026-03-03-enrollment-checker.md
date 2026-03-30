# Enrollment Checker Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add a public enrollment checking API that delegates to state plugins (DC stored procedure, CO CBMS API) and persists de-identified results.

**Architecture:** New `IEnrollmentCheckService` plugin interface in the state connector package. DC and CO connectors each implement it. Main portal exposes `POST /api/enrollment/check` (unauthenticated, rate-limited) through a use case handler that calls the plugin and logs de-identified results.

**Tech Stack:** .NET 10, MEF (System.Composition), EF Core, xUnit + NSubstitute, Kiota (CO API client), Microsoft.Data.SqlClient (DC stored proc)

**Repos (build order):**
1. `sebt-self-service-portal-state-connector` — interface + models
2. `sebt-self-service-portal-dc-connector` — DC implementation
3. `sebt-self-service-portal-co-connector` — CO implementation
4. `sebt-self-service-portal` — API controller, use case, persistence, plugin registration

---

## Phase 1: Plugin Interface (state-connector repo)

Working directory: `~/Projects/SEBT/sebt-self-service-portal-state-connector`

### Task 1: Create EnrollmentStatus enum

**Files:**
- Create: `src/SEBT.Portal.StatesPlugins.Interfaces/Models/EnrollmentCheck/EnrollmentStatus.cs`

**Step 1: Create the enum**

```csharp
namespace SEBT.Portal.StatesPlugins.Interfaces.Models.EnrollmentCheck;

public enum EnrollmentStatus
{
    Match = 0,
    PossibleMatch = 1,
    NonMatch = 2,
    Error = 3
}
```

Follow the pattern of `ApplicationStatus.cs` in `Models/Household/`.

**Step 2: Verify it builds**

Run: `dotnet build src/SEBT.Portal.StatesPlugins.Interfaces/SEBT.Portal.StatesPlugins.Interfaces.csproj`
Expected: Build succeeded, 0 errors

### Task 2: Create EligibilityType enum

**Files:**
- Create: `src/SEBT.Portal.StatesPlugins.Interfaces/Models/EnrollmentCheck/EligibilityType.cs`

**Step 1: Create the enum**

```csharp
namespace SEBT.Portal.StatesPlugins.Interfaces.Models.EnrollmentCheck;

public enum EligibilityType
{
    Unknown = 0,
    Snap = 1,
    Tanf = 2,
    Frp = 3,
    DirectCert = 4
}
```

**Step 2: Verify it builds**

Run: `dotnet build src/SEBT.Portal.StatesPlugins.Interfaces/SEBT.Portal.StatesPlugins.Interfaces.csproj`
Expected: Build succeeded, 0 errors

### Task 3: Create request models

**Files:**
- Create: `src/SEBT.Portal.StatesPlugins.Interfaces/Models/EnrollmentCheck/ChildCheckRequest.cs`
- Create: `src/SEBT.Portal.StatesPlugins.Interfaces/Models/EnrollmentCheck/EnrollmentCheckRequest.cs`

**Step 1: Create ChildCheckRequest**

```csharp
namespace SEBT.Portal.StatesPlugins.Interfaces.Models.EnrollmentCheck;

public class ChildCheckRequest
{
    public required Guid CheckId { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public required DateOnly DateOfBirth { get; init; }
    public string? SchoolName { get; init; }
    public string? SchoolCode { get; init; }
    public IDictionary<string, string> AdditionalFields { get; init; } = new Dictionary<string, string>();
}
```

**Step 2: Create EnrollmentCheckRequest**

```csharp
namespace SEBT.Portal.StatesPlugins.Interfaces.Models.EnrollmentCheck;

public class EnrollmentCheckRequest
{
    public required IList<ChildCheckRequest> Children { get; init; }
    public string? GuardianContactInfo { get; init; }
}
```

**Step 3: Verify it builds**

Run: `dotnet build src/SEBT.Portal.StatesPlugins.Interfaces/SEBT.Portal.StatesPlugins.Interfaces.csproj`
Expected: Build succeeded, 0 errors

### Task 4: Create response models

**Files:**
- Create: `src/SEBT.Portal.StatesPlugins.Interfaces/Models/EnrollmentCheck/ChildCheckResult.cs`
- Create: `src/SEBT.Portal.StatesPlugins.Interfaces/Models/EnrollmentCheck/EnrollmentCheckResult.cs`

**Step 1: Create ChildCheckResult**

```csharp
namespace SEBT.Portal.StatesPlugins.Interfaces.Models.EnrollmentCheck;

public class ChildCheckResult
{
    public required Guid CheckId { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public required DateOnly DateOfBirth { get; init; }
    public required EnrollmentStatus Status { get; init; }
    public double? MatchConfidence { get; init; }
    public string? StatusMessage { get; init; }
    public EligibilityType? EligibilityType { get; init; }
    public string? SchoolName { get; init; }
    public IDictionary<string, object> Details { get; init; } = new Dictionary<string, object>();
}
```

**Step 2: Create EnrollmentCheckResult**

```csharp
namespace SEBT.Portal.StatesPlugins.Interfaces.Models.EnrollmentCheck;

public class EnrollmentCheckResult
{
    public required IList<ChildCheckResult> Results { get; init; }
    public string? ResponseMessage { get; init; }
}
```

**Step 3: Verify it builds**

Run: `dotnet build src/SEBT.Portal.StatesPlugins.Interfaces/SEBT.Portal.StatesPlugins.Interfaces.csproj`
Expected: Build succeeded, 0 errors

### Task 5: Create IEnrollmentCheckService interface

**Files:**
- Create: `src/SEBT.Portal.StatesPlugins.Interfaces/IEnrollmentCheckService.cs`

**Step 1: Create the interface**

```csharp
using SEBT.Portal.StatesPlugins.Interfaces.Models.EnrollmentCheck;

namespace SEBT.Portal.StatesPlugins.Interfaces;

/// <summary>
/// State plugin interface for checking child enrollment in Summer EBT benefits.
/// Each state provides its own implementation that delegates to state-specific
/// APIs or databases for enrollment matching.
/// </summary>
public interface IEnrollmentCheckService : IStatePlugin
{
    /// <summary>
    /// Checks enrollment status for one or more children.
    /// </summary>
    /// <param name="request">The enrollment check request containing children to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Results for each child checked.</returns>
    Task<EnrollmentCheckResult> CheckEnrollmentAsync(
        EnrollmentCheckRequest request,
        CancellationToken cancellationToken = default);
}
```

Follow the pattern of `ISummerEbtCaseService.cs` — extends `IStatePlugin`, has XML docs on the interface and method.

**Step 2: Verify it builds**

Run: `dotnet build src/SEBT.Portal.StatesPlugins.Interfaces/SEBT.Portal.StatesPlugins.Interfaces.csproj`
Expected: Build succeeded, 0 errors

### Task 6: Publish updated NuGet package

**Step 1: Build and pack to local NuGet store**

Run: `dotnet pack src/SEBT.Portal.StatesPlugins.Interfaces/SEBT.Portal.StatesPlugins.Interfaces.csproj --output ~/nuget-store/`
Expected: Package created successfully in ~/nuget-store/

**Step 2: Commit**

Stage all new files in `src/SEBT.Portal.StatesPlugins.Interfaces/Models/EnrollmentCheck/` and `IEnrollmentCheckService.cs`.
Commit message: `DC-172: Add IEnrollmentCheckService plugin interface and enrollment check models`

---

## Phase 2: DC Plugin Implementation (dc-connector repo)

Working directory: `~/Projects/SEBT/sebt-self-service-portal-dc-connector`

### Task 7: Restore updated state connector package

**Step 1: Restore NuGet packages to pick up updated Interfaces**

Run: `dotnet restore`
Expected: Restore succeeded with updated SEBT.Portal.StatesPlugins.Interfaces package

**Step 2: Verify it builds with new interface**

Run: `dotnet build src/SEBT.Portal.StatePlugins.DC/SEBT.Portal.StatePlugins.DC.csproj`
Expected: Build succeeded (existing code doesn't implement IEnrollmentCheckService yet, which is fine — it's a separate class)

### Task 8: Write failing test for DcEnrollmentCheckService

**Files:**
- Create: `test/SEBT.Portal.StatePlugins.DC.Tests/DcEnrollmentCheckServiceTests.cs`

**Step 1: Write the test**

The DC connector tests use direct SQL against Testcontainers, but for the enrollment check service we'll start with a unit test that verifies the service implements the interface and handles basic validation. The stored proc integration test requires the actual DC database schema.

```csharp
using SEBT.Portal.StatesPlugins.Interfaces.Models.EnrollmentCheck;

namespace SEBT.Portal.StatePlugins.DC.Tests;

public class DcEnrollmentCheckServiceTests
{
    [Fact]
    public async Task CheckEnrollmentAsync_WhenRequestHasNoChildren_ReturnsEmptyResults()
    {
        var service = new DcEnrollmentCheckService();
        var request = new EnrollmentCheckRequest
        {
            Children = new List<ChildCheckRequest>()
        };

        var result = await service.CheckEnrollmentAsync(request);

        Assert.NotNull(result);
        Assert.Empty(result.Results);
    }

    [Fact]
    public async Task CheckEnrollmentAsync_WhenNoConnectionString_ThrowsInvalidOperationException()
    {
        var service = new DcEnrollmentCheckService();
        var request = new EnrollmentCheckRequest
        {
            Children = new List<ChildCheckRequest>
            {
                new ChildCheckRequest
                {
                    CheckId = Guid.NewGuid(),
                    FirstName = "Jane",
                    LastName = "Doe",
                    DateOfBirth = new DateOnly(2015, 3, 12),
                    SchoolName = "Lincoln Elementary"
                }
            }
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CheckEnrollmentAsync(request));
    }
}
```

**Step 2: Run the test — verify it fails**

Run: `dotnet test test/SEBT.Portal.StatePlugins.DC.Tests/ --filter "FullyQualifiedName~DcEnrollmentCheckServiceTests"`
Expected: FAIL — `DcEnrollmentCheckService` does not exist yet

### Task 9: Implement DcEnrollmentCheckService

**Files:**
- Create: `src/SEBT.Portal.StatePlugins.DC/DcEnrollmentCheckService.cs`

**Step 1: Create the service**

Follow the exact pattern of `DcSummerEbtCaseService.cs`: MEF export, connection string from config, stored procedure call.

```csharp
using System.Composition;
using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using SEBT.Portal.StatesPlugins.Interfaces;
using SEBT.Portal.StatesPlugins.Interfaces.Models.EnrollmentCheck;

namespace SEBT.Portal.StatePlugins.DC;

[Export(typeof(IStatePlugin))]
[ExportMetadata("StateCode", "DC")]
public class DcEnrollmentCheckService : IEnrollmentCheckService
{
    private const string ConnectionStringKey = "DCConnector:ConnectionString";
    private readonly IConfiguration? _configuration;

    [ImportingConstructor]
    public DcEnrollmentCheckService([Import(AllowDefault = true)] IConfiguration? configuration = null)
    {
        _configuration = configuration;
    }

    private string GetConnectionString()
    {
        var connectionString = _configuration?[ConnectionStringKey]
            ?? Environment.GetEnvironmentVariable("DCConnector__ConnectionString");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "DC connector connection string is not configured. " +
                $"Set '{ConnectionStringKey}' in configuration or 'DCConnector__ConnectionString' environment variable.");
        }

        return connectionString;
    }

    public async Task<EnrollmentCheckResult> CheckEnrollmentAsync(
        EnrollmentCheckRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Children.Count == 0)
        {
            return new EnrollmentCheckResult { Results = new List<ChildCheckResult>() };
        }

        await using var connection = new SqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);

        var results = new List<ChildCheckResult>();

        foreach (var child in request.Children)
        {
            var result = await CheckSingleChildAsync(connection, child, cancellationToken);
            results.Add(result);
        }

        return new EnrollmentCheckResult { Results = results };
    }

    private static async Task<ChildCheckResult> CheckSingleChildAsync(
        SqlConnection connection,
        ChildCheckRequest child,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand("dbo.sp_CheckEligibility", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        command.Parameters.AddWithValue("@submissionId", child.CheckId);

        // The legacy stored proc accepts form data as JSON
        var formData = new
        {
            firstName = child.FirstName,
            lastName = child.LastName,
            dateOfBirth = child.DateOfBirth.ToString("yyyy-MM-dd"),
            schoolName = child.SchoolName
        };
        command.Parameters.AddWithValue("@formData",
            System.Text.Json.JsonSerializer.Serialize(formData));

        var isEligible = new SqlParameter("@isEligible", SqlDbType.Bit)
        {
            Direction = ParameterDirection.Output
        };
        command.Parameters.Add(isEligible);

        // Add output params for mailing address (we won't use these for the response
        // but the proc requires them)
        AddOutputStringParam(command, "@mailingAddressStreetLine1", 100);
        AddOutputStringParam(command, "@mailingAddressStreetLine2", 100);
        AddOutputStringParam(command, "@mailingAddressCity", 100);
        AddOutputStringParam(command, "@mailingAddressState", 100);
        AddOutputStringParam(command, "@mailingAddressZipCode", 100);

        await command.ExecuteReaderAsync(cancellationToken);

        var matched = isEligible.Value != DBNull.Value && Convert.ToBoolean(isEligible.Value);

        return new ChildCheckResult
        {
            CheckId = child.CheckId,
            FirstName = child.FirstName,
            LastName = child.LastName,
            DateOfBirth = child.DateOfBirth,
            Status = matched ? EnrollmentStatus.Match : EnrollmentStatus.NonMatch,
            SchoolName = child.SchoolName
        };
    }

    private static void AddOutputStringParam(SqlCommand command, string name, int size)
    {
        command.Parameters.Add(new SqlParameter(name, SqlDbType.VarChar, size)
        {
            Direction = ParameterDirection.Output,
            Value = string.Empty
        });
    }
}
```

**NOTE:** The exact stored procedure parameters may need adjustment when testing against the real DC database. The legacy `sp_CheckEligibility` accepts `@submissionId` (GUID) and `@formData` (JSON) and returns `@isEligible` (BIT) plus address output params. This implementation follows that pattern. If the proc interface differs, update accordingly.

**Step 2: Run the tests — verify they pass**

Run: `dotnet test test/SEBT.Portal.StatePlugins.DC.Tests/ --filter "FullyQualifiedName~DcEnrollmentCheckServiceTests"`
Expected: 2 tests pass

**Step 3: Commit**

Stage `DcEnrollmentCheckService.cs` and `DcEnrollmentCheckServiceTests.cs`.
Commit message: `DC-172: Add DcEnrollmentCheckService with stored procedure integration`

---

## Phase 3: CO Plugin Implementation (co-connector repo)

Working directory: `~/Projects/SEBT/sebt-self-service-portal-co-connector`

### Task 10: Restore updated state connector package

**Step 1: Restore**

Run: `dotnet restore`
Expected: Restore succeeded with updated Interfaces package

### Task 11: Write failing tests for ColoradoEnrollmentCheckService

**Files:**
- Create: `test/SEBT.Portal.StatePlugins.CO.Tests/ColoradoEnrollmentCheckServiceTests.cs`

**Step 1: Write the tests**

The CO service wraps the Kiota-generated CBMS API client. We'll mock the HTTP layer for unit tests.

```csharp
using SEBT.Portal.StatesPlugins.Interfaces.Models.EnrollmentCheck;

namespace SEBT.Portal.StatePlugins.CO.Tests;

public class ColoradoEnrollmentCheckServiceTests
{
    [Fact]
    public async Task CheckEnrollmentAsync_WhenRequestHasNoChildren_ReturnsEmptyResults()
    {
        var service = new ColoradoEnrollmentCheckService();
        var request = new EnrollmentCheckRequest
        {
            Children = new List<ChildCheckRequest>()
        };

        var result = await service.CheckEnrollmentAsync(request);

        Assert.NotNull(result);
        Assert.Empty(result.Results);
    }

    [Fact]
    public async Task CheckEnrollmentAsync_WhenNoApiConfiguration_ThrowsInvalidOperationException()
    {
        var service = new ColoradoEnrollmentCheckService();
        var request = new EnrollmentCheckRequest
        {
            Children = new List<ChildCheckRequest>
            {
                new ChildCheckRequest
                {
                    CheckId = Guid.NewGuid(),
                    FirstName = "Jane",
                    LastName = "Doe",
                    DateOfBirth = new DateOnly(2015, 3, 12),
                    SchoolName = "Lincoln Elementary"
                }
            }
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CheckEnrollmentAsync(request));
    }
}
```

**Step 2: Run tests — verify they fail**

Run: `dotnet test test/SEBT.Portal.StatePlugins.CO.Tests/ --filter "FullyQualifiedName~ColoradoEnrollmentCheckServiceTests"`
Expected: FAIL — `ColoradoEnrollmentCheckService` doesn't have `CheckEnrollmentAsync` yet

### Task 12: Implement ColoradoEnrollmentCheckService

**Files:**
- Create: `src/SEBT.Portal.StatePlugins.CO/ColoradoEnrollmentCheckService.cs`

**Step 1: Create the service**

```csharp
using System.Composition;
using Microsoft.Extensions.Configuration;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;
using SEBT.Portal.StatePlugins.CO.CbmsApi;
using SEBT.Portal.StatePlugins.CO.CbmsApi.Models;
using SEBT.Portal.StatesPlugins.Interfaces;
using SEBT.Portal.StatesPlugins.Interfaces.Models.EnrollmentCheck;
using CbmsCheckEnrollmentRequest = SEBT.Portal.StatePlugins.CO.CbmsApi.Models.CheckEnrollmentRequest;

namespace SEBT.Portal.StatePlugins.CO;

[Export(typeof(IStatePlugin))]
[ExportMetadata("StateCode", "CO")]
public class ColoradoEnrollmentCheckService : IEnrollmentCheckService
{
    private const string BaseUrlKey = "COConnector:CbmsApi:BaseUrl";
    private const string ApiKeyKey = "COConnector:CbmsApi:ApiKey";
    private readonly IConfiguration? _configuration;

    [ImportingConstructor]
    public ColoradoEnrollmentCheckService(
        [Import(AllowDefault = true)] IConfiguration? configuration = null)
    {
        _configuration = configuration;
    }

    private (string baseUrl, string apiKey) GetApiConfiguration()
    {
        var baseUrl = _configuration?[BaseUrlKey]
            ?? Environment.GetEnvironmentVariable("COConnector__CbmsApi__BaseUrl");
        var apiKey = _configuration?[ApiKeyKey]
            ?? Environment.GetEnvironmentVariable("COConnector__CbmsApi__ApiKey");

        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "CO CBMS API configuration is missing. " +
                $"Set '{BaseUrlKey}' and '{ApiKeyKey}' in configuration.");
        }

        return (baseUrl, apiKey);
    }

    public async Task<EnrollmentCheckResult> CheckEnrollmentAsync(
        EnrollmentCheckRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Children.Count == 0)
        {
            return new EnrollmentCheckResult { Results = new List<ChildCheckResult>() };
        }

        var (baseUrl, apiKey) = GetApiConfiguration();
        var client = CreateApiClient(baseUrl, apiKey);

        var cbmsRequests = request.Children.Select(child => new CbmsCheckEnrollmentRequest
        {
            StdFirstName = child.FirstName,
            StdLastName = child.LastName,
            StdDob = child.DateOfBirth.ToString("yyyy-MM-dd"),
            StdSchlCd = child.SchoolCode
        }).ToList();

        var response = await client.Sebt.CheckEnrollment.PostAsync(cbmsRequests, cancellationToken: cancellationToken);

        return MapResponse(request.Children, response);
    }

    private static EnrollmentCheckResult MapResponse(
        IList<ChildCheckRequest> children,
        CheckEnrollmentResponse? response)
    {
        if (response?.StdntDtls == null || response.StdntDtls.Count == 0)
        {
            // No matches from CBMS — return NonMatch for all children
            return new EnrollmentCheckResult
            {
                Results = children.Select(child => new ChildCheckResult
                {
                    CheckId = child.CheckId,
                    FirstName = child.FirstName,
                    LastName = child.LastName,
                    DateOfBirth = child.DateOfBirth,
                    Status = EnrollmentStatus.NonMatch,
                    SchoolName = child.SchoolName
                }).ToList(),
                ResponseMessage = response?.RespMsg
            };
        }

        // Correlate response items to request children by matching on name + DOB
        var results = new List<ChildCheckResult>();
        foreach (var child in children)
        {
            var match = response.StdntDtls.FirstOrDefault(d =>
                string.Equals(d.StdFstNm, child.FirstName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(d.StdLstNm, child.LastName, StringComparison.OrdinalIgnoreCase) &&
                d.StdDob == child.DateOfBirth.ToString("yyyy-MM-dd"));

            if (match == null)
            {
                results.Add(new ChildCheckResult
                {
                    CheckId = child.CheckId,
                    FirstName = child.FirstName,
                    LastName = child.LastName,
                    DateOfBirth = child.DateOfBirth,
                    Status = EnrollmentStatus.NonMatch,
                    SchoolName = child.SchoolName
                });
                continue;
            }

            results.Add(new ChildCheckResult
            {
                CheckId = child.CheckId,
                FirstName = child.FirstName,
                LastName = child.LastName,
                DateOfBirth = child.DateOfBirth,
                Status = MapEnrollmentStatus(match.StdntEligSts, match.MtchCnfd),
                MatchConfidence = match.MtchCnfd,
                SchoolName = child.SchoolName,
                EligibilityType = MapEligibilityType(match.StdntEligSts)
            });
        }

        return new EnrollmentCheckResult
        {
            Results = results,
            ResponseMessage = response.RespMsg
        };
    }

    private static EnrollmentStatus MapEnrollmentStatus(string? eligibilityStatus, double? confidence)
    {
        if (string.IsNullOrWhiteSpace(eligibilityStatus))
            return EnrollmentStatus.NonMatch;

        // Map CBMS eligibility status strings to our enum
        // Exact values TBD based on CBMS API documentation
        var normalized = eligibilityStatus.Trim().ToUpperInvariant();
        return normalized switch
        {
            "ELIGIBLE" or "ENROLLED" => EnrollmentStatus.Match,
            "PENDING" => EnrollmentStatus.PossibleMatch,
            "NOT_ELIGIBLE" or "NOT_FOUND" => EnrollmentStatus.NonMatch,
            _ when confidence.HasValue && confidence.Value >= 0.8 => EnrollmentStatus.Match,
            _ when confidence.HasValue && confidence.Value >= 0.5 => EnrollmentStatus.PossibleMatch,
            _ => EnrollmentStatus.NonMatch
        };
    }

    private static EligibilityType? MapEligibilityType(string? eligibilityStatus)
    {
        if (string.IsNullOrWhiteSpace(eligibilityStatus))
            return null;

        var normalized = eligibilityStatus.Trim().ToUpperInvariant();
        return normalized switch
        {
            var s when s.Contains("SNAP") => EligibilityType.Snap,
            var s when s.Contains("TANF") => EligibilityType.Tanf,
            var s when s.Contains("FRP") || s.Contains("FREE") || s.Contains("REDUCED") => EligibilityType.Frp,
            var s when s.Contains("DIRECT") => EligibilityType.DirectCert,
            _ => null
        };
    }

    private static CbmsSebtApiClient CreateApiClient(string baseUrl, string apiKey)
    {
        var authProvider = new ApiKeyAuthenticationProvider(
            apiKey, "x-api-key", ApiKeyAuthenticationProvider.KeyLocation.Header);
        var adapter = new HttpClientRequestAdapter(authProvider)
        {
            BaseUrl = baseUrl
        };
        return new CbmsSebtApiClient(adapter);
    }
}
```

**NOTE:** The CBMS API status string mappings (`ELIGIBLE`, `ENROLLED`, etc.) and API client construction may need adjustment based on actual CBMS API behavior. The `ApiKeyAuthenticationProvider` usage assumes the CO connector's existing auth pattern — verify against `CbmsSebtApiClientFactory` in the SocureApi project for the correct Kiota auth setup.

**Step 2: Run tests — verify they pass**

Run: `dotnet test test/SEBT.Portal.StatePlugins.CO.Tests/ --filter "FullyQualifiedName~ColoradoEnrollmentCheckServiceTests"`
Expected: 2 tests pass

**Step 3: Commit**

Stage `ColoradoEnrollmentCheckService.cs` and `ColoradoEnrollmentCheckServiceTests.cs`.
Commit message: `DC-172: Add ColoradoEnrollmentCheckService with CBMS API integration`

---

## Phase 4: Main Portal — Core Layer (sebt-self-service-portal repo)

Working directory: `~/Projects/SEBT/sebt-self-service-portal`

### Task 13: Restore updated state connector package

**Step 1: Restore NuGet packages**

Run: `dotnet restore`
Expected: Restore succeeded with updated Interfaces package

### Task 14: Register IEnrollmentCheckService in plugin loader

**Files:**
- Modify: `src/SEBT.Portal.Api/Composition/ServiceCollectionPluginExtensions.cs`

**Step 1: Add the convention**

In `CreateContainerConfiguration()`, after the existing `ISummerEbtCaseService` convention block, add:

```csharp
conventions
    .ForTypesDerivedFrom<IEnrollmentCheckService>()
    .Export<IEnrollmentCheckService>()
    .Shared();
```

Add the using: `using SEBT.Portal.StatesPlugins.Interfaces;` (should already be there for ISummerEbtCaseService).

**Step 2: Verify it builds**

Run: `dotnet build src/SEBT.Portal.Api/SEBT.Portal.Api.csproj`
Expected: Build succeeded

**Step 3: Commit**

Commit message: `DC-172: Register IEnrollmentCheckService in plugin loader`

### Task 15: Create de-identified persistence entity and EF migration

**Files:**
- Create: `src/SEBT.Portal.Infrastructure/Data/Entities/EnrollmentCheckSubmissionEntity.cs`
- Create: `src/SEBT.Portal.Infrastructure/Data/Entities/DeidentifiedChildResultEntity.cs`
- Modify: `src/SEBT.Portal.Infrastructure/Data/PortalDbContext.cs`

**Step 1: Create the entities**

```csharp
// EnrollmentCheckSubmissionEntity.cs
namespace SEBT.Portal.Infrastructure.Data.Entities;

public class EnrollmentCheckSubmissionEntity
{
    public Guid Id { get; set; }
    public DateTime CheckedAtUtc { get; set; }
    public int ChildrenChecked { get; set; }
    public string? IpAddressHash { get; set; }
    public ICollection<DeidentifiedChildResultEntity> ChildResults { get; set; } = new List<DeidentifiedChildResultEntity>();
}
```

```csharp
// DeidentifiedChildResultEntity.cs
namespace SEBT.Portal.Infrastructure.Data.Entities;

public class DeidentifiedChildResultEntity
{
    public Guid Id { get; set; }
    public Guid SubmissionId { get; set; }
    public int BirthYear { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? EligibilityType { get; set; }
    public string? SchoolName { get; set; }
    public EnrollmentCheckSubmissionEntity Submission { get; set; } = null!;
}
```

**Step 2: Register in PortalDbContext**

Add DbSets:

```csharp
public DbSet<EnrollmentCheckSubmissionEntity> EnrollmentCheckSubmissions { get; set; }
public DbSet<DeidentifiedChildResultEntity> DeidentifiedChildResults { get; set; }
```

Add entity configuration in `OnModelCreating`:

```csharp
modelBuilder.Entity<EnrollmentCheckSubmissionEntity>(entity =>
{
    entity.ToTable("EnrollmentCheckSubmissions");
    entity.HasKey(e => e.Id);
    entity.Property(e => e.CheckedAtUtc).IsRequired();
    entity.Property(e => e.IpAddressHash).HasMaxLength(128);
    entity.HasMany(e => e.ChildResults)
        .WithOne(e => e.Submission)
        .HasForeignKey(e => e.SubmissionId)
        .OnDelete(DeleteBehavior.Cascade);
});

modelBuilder.Entity<DeidentifiedChildResultEntity>(entity =>
{
    entity.ToTable("DeidentifiedChildResults");
    entity.HasKey(e => e.Id);
    entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
    entity.Property(e => e.EligibilityType).HasMaxLength(50);
    entity.Property(e => e.SchoolName).HasMaxLength(255);
});
```

**Step 3: Generate EF Core migration**

Run: `dotnet ef migrations add AddEnrollmentCheckSubmissions --project src/SEBT.Portal.Infrastructure/SEBT.Portal.Infrastructure.csproj --startup-project src/SEBT.Portal.Api/SEBT.Portal.Api.csproj`
Expected: Migration created successfully

**Step 4: Verify it builds**

Run: `dotnet build`
Expected: Build succeeded

**Step 5: Commit**

Commit message: `DC-172: Add EnrollmentCheckSubmission entity and EF migration`

### Task 16: Create IEnrollmentCheckSubmissionLogger and implementation

**Files:**
- Create: `src/SEBT.Portal.Core/Services/IEnrollmentCheckSubmissionLogger.cs`
- Create: `src/SEBT.Portal.Infrastructure/Services/EnrollmentCheckSubmissionLogger.cs`

**Step 1: Write the failing test**

Create: `test/SEBT.Portal.Tests/Unit/Services/EnrollmentCheckSubmissionLoggerTests.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using SEBT.Portal.Infrastructure.Data;
using SEBT.Portal.Infrastructure.Services;

namespace SEBT.Portal.Tests.Unit.Services;

public class EnrollmentCheckSubmissionLoggerTests
{
    private static PortalDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new PortalDbContext(options);
    }

    [Fact]
    public async Task LogSubmissionAsync_PersistsDeidentifiedData()
    {
        using var context = CreateInMemoryContext();
        var logger = new EnrollmentCheckSubmissionLogger(context);

        var submission = new Core.Models.EnrollmentCheck.EnrollmentCheckSubmission
        {
            SubmissionId = Guid.NewGuid(),
            CheckedAtUtc = DateTime.UtcNow,
            ChildrenChecked = 1,
            IpAddressHash = "abc123hash",
            ChildResults = new List<Core.Models.EnrollmentCheck.DeidentifiedChildResult>
            {
                new()
                {
                    BirthYear = 2015,
                    Status = "Match",
                    EligibilityType = "SNAP",
                    SchoolName = "Lincoln Elementary"
                }
            }
        };

        await logger.LogSubmissionAsync(submission);

        var stored = await context.EnrollmentCheckSubmissions
            .Include(s => s.ChildResults)
            .FirstOrDefaultAsync();

        Assert.NotNull(stored);
        Assert.Equal(1, stored!.ChildrenChecked);
        Assert.Equal("abc123hash", stored.IpAddressHash);
        Assert.Single(stored.ChildResults);
        Assert.Equal(2015, stored.ChildResults.First().BirthYear);
        Assert.Equal("Match", stored.ChildResults.First().Status);
    }
}
```

**Step 2: Run test — verify it fails**

Run: `dotnet test test/SEBT.Portal.Tests/ --filter "FullyQualifiedName~EnrollmentCheckSubmissionLoggerTests"`
Expected: FAIL — types don't exist yet

**Step 3: Create the Core models and interface**

```csharp
// src/SEBT.Portal.Core/Models/EnrollmentCheck/EnrollmentCheckSubmission.cs
namespace SEBT.Portal.Core.Models.EnrollmentCheck;

public class EnrollmentCheckSubmission
{
    public Guid SubmissionId { get; init; }
    public DateTime CheckedAtUtc { get; init; }
    public int ChildrenChecked { get; init; }
    public string? IpAddressHash { get; init; }
    public IList<DeidentifiedChildResult> ChildResults { get; init; } = new List<DeidentifiedChildResult>();
}

// src/SEBT.Portal.Core/Models/EnrollmentCheck/DeidentifiedChildResult.cs
namespace SEBT.Portal.Core.Models.EnrollmentCheck;

public class DeidentifiedChildResult
{
    public int BirthYear { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? EligibilityType { get; init; }
    public string? SchoolName { get; init; }
}

// src/SEBT.Portal.Core/Services/IEnrollmentCheckSubmissionLogger.cs
namespace SEBT.Portal.Core.Services;

public interface IEnrollmentCheckSubmissionLogger
{
    Task LogSubmissionAsync(
        Models.EnrollmentCheck.EnrollmentCheckSubmission submission,
        CancellationToken cancellationToken = default);
}
```

**Step 4: Create the Infrastructure implementation**

```csharp
// src/SEBT.Portal.Infrastructure/Services/EnrollmentCheckSubmissionLogger.cs
using SEBT.Portal.Core.Models.EnrollmentCheck;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Infrastructure.Data;
using SEBT.Portal.Infrastructure.Data.Entities;

namespace SEBT.Portal.Infrastructure.Services;

public class EnrollmentCheckSubmissionLogger(PortalDbContext dbContext) : IEnrollmentCheckSubmissionLogger
{
    public async Task LogSubmissionAsync(
        EnrollmentCheckSubmission submission,
        CancellationToken cancellationToken = default)
    {
        var entity = new EnrollmentCheckSubmissionEntity
        {
            Id = submission.SubmissionId,
            CheckedAtUtc = submission.CheckedAtUtc,
            ChildrenChecked = submission.ChildrenChecked,
            IpAddressHash = submission.IpAddressHash,
            ChildResults = submission.ChildResults.Select(cr => new DeidentifiedChildResultEntity
            {
                Id = Guid.NewGuid(),
                BirthYear = cr.BirthYear,
                Status = cr.Status,
                EligibilityType = cr.EligibilityType,
                SchoolName = cr.SchoolName
            }).ToList()
        };

        dbContext.EnrollmentCheckSubmissions.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
```

**Step 5: Run test — verify it passes**

Run: `dotnet test test/SEBT.Portal.Tests/ --filter "FullyQualifiedName~EnrollmentCheckSubmissionLoggerTests"`
Expected: PASS

**Step 6: Commit**

Commit message: `DC-172: Add enrollment check submission persistence (de-identified)`

### Task 17: Create the use case (command + handler)

**Files:**
- Create: `src/SEBT.Portal.UseCases/EnrollmentCheck/CheckEnrollmentCommand.cs`
- Create: `src/SEBT.Portal.UseCases/EnrollmentCheck/CheckEnrollmentCommandHandler.cs`
- Create: `test/SEBT.Portal.Tests/Unit/UseCases/EnrollmentCheck/CheckEnrollmentCommandHandlerTests.cs`

**Step 1: Write the failing tests**

```csharp
using NSubstitute;
using SEBT.Portal.Core.Services;
using SEBT.Portal.UseCases.EnrollmentCheck;
using SEBT.Portal.StatesPlugins.Interfaces;
using SEBT.Portal.StatesPlugins.Interfaces.Models.EnrollmentCheck;
using Microsoft.Extensions.Logging;

namespace SEBT.Portal.Tests.Unit.UseCases.EnrollmentCheck;

public class CheckEnrollmentCommandHandlerTests
{
    private readonly IEnrollmentCheckService _enrollmentCheckService = Substitute.For<IEnrollmentCheckService>();
    private readonly IEnrollmentCheckSubmissionLogger _submissionLogger = Substitute.For<IEnrollmentCheckSubmissionLogger>();
    private readonly ILogger<CheckEnrollmentCommandHandler> _logger = Substitute.For<ILogger<CheckEnrollmentCommandHandler>>();

    private CheckEnrollmentCommandHandler CreateHandler() =>
        new(_enrollmentCheckService, _submissionLogger, _logger);

    [Fact]
    public async Task Handle_WhenNoChildren_ReturnsValidationFailed()
    {
        var handler = CreateHandler();
        var command = new CheckEnrollmentCommand
        {
            Children = new List<CheckEnrollmentCommand.ChildInput>(),
            IpAddress = "127.0.0.1"
        };

        var result = await handler.Handle(command);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task Handle_WithValidChild_CallsPluginAndReturnsResults()
    {
        var handler = CreateHandler();
        var command = new CheckEnrollmentCommand
        {
            Children = new List<CheckEnrollmentCommand.ChildInput>
            {
                new()
                {
                    FirstName = "Jane",
                    LastName = "Doe",
                    DateOfBirth = new DateOnly(2015, 3, 12),
                    SchoolName = "Lincoln Elementary"
                }
            },
            IpAddress = "127.0.0.1"
        };

        _enrollmentCheckService
            .CheckEnrollmentAsync(Arg.Any<EnrollmentCheckRequest>(), Arg.Any<CancellationToken>())
            .Returns(new EnrollmentCheckResult
            {
                Results = new List<ChildCheckResult>
                {
                    new()
                    {
                        CheckId = Guid.NewGuid(),
                        FirstName = "Jane",
                        LastName = "Doe",
                        DateOfBirth = new DateOnly(2015, 3, 12),
                        Status = EnrollmentStatus.Match,
                        SchoolName = "Lincoln Elementary"
                    }
                }
            });

        var result = await handler.Handle(command);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Results);
        Assert.Equal(EnrollmentStatus.Match, result.Value.Results[0].Status);
    }

    [Fact]
    public async Task Handle_LogsDeidentifiedSubmission()
    {
        var handler = CreateHandler();
        var command = new CheckEnrollmentCommand
        {
            Children = new List<CheckEnrollmentCommand.ChildInput>
            {
                new()
                {
                    FirstName = "Jane",
                    LastName = "Doe",
                    DateOfBirth = new DateOnly(2015, 3, 12),
                    SchoolName = "Lincoln Elementary"
                }
            },
            IpAddress = "127.0.0.1"
        };

        _enrollmentCheckService
            .CheckEnrollmentAsync(Arg.Any<EnrollmentCheckRequest>(), Arg.Any<CancellationToken>())
            .Returns(new EnrollmentCheckResult
            {
                Results = new List<ChildCheckResult>
                {
                    new()
                    {
                        CheckId = Guid.NewGuid(),
                        FirstName = "Jane",
                        LastName = "Doe",
                        DateOfBirth = new DateOnly(2015, 3, 12),
                        Status = EnrollmentStatus.Match,
                        SchoolName = "Lincoln Elementary",
                        EligibilityType = StatesPlugins.Interfaces.Models.EnrollmentCheck.EligibilityType.Snap
                    }
                }
            });

        await handler.Handle(command);

        await _submissionLogger.Received(1).LogSubmissionAsync(
            Arg.Is<Core.Models.EnrollmentCheck.EnrollmentCheckSubmission>(s =>
                s.ChildrenChecked == 1 &&
                s.ChildResults[0].BirthYear == 2015 &&
                s.ChildResults[0].Status == "Match" &&
                s.ChildResults[0].SchoolName == "Lincoln Elementary"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenPluginThrows_ReturnsError()
    {
        var handler = CreateHandler();
        var command = new CheckEnrollmentCommand
        {
            Children = new List<CheckEnrollmentCommand.ChildInput>
            {
                new()
                {
                    FirstName = "Jane",
                    LastName = "Doe",
                    DateOfBirth = new DateOnly(2015, 3, 12),
                    SchoolName = "Lincoln Elementary"
                }
            },
            IpAddress = "127.0.0.1"
        };

        _enrollmentCheckService
            .CheckEnrollmentAsync(Arg.Any<EnrollmentCheckRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("Plugin error"));

        var result = await handler.Handle(command);

        Assert.False(result.IsSuccess);
    }
}
```

**Step 2: Run test — verify it fails**

Run: `dotnet test test/SEBT.Portal.Tests/ --filter "FullyQualifiedName~CheckEnrollmentCommandHandlerTests"`
Expected: FAIL — types don't exist yet

**Step 3: Create the command and handler**

```csharp
// src/SEBT.Portal.UseCases/EnrollmentCheck/CheckEnrollmentCommand.cs
using SEBT.Portal.Kernel;
using SEBT.Portal.StatesPlugins.Interfaces.Models.EnrollmentCheck;

namespace SEBT.Portal.UseCases.EnrollmentCheck;

public class CheckEnrollmentCommand : ICommand<EnrollmentCheckResult>
{
    public required IList<ChildInput> Children { get; init; }
    public string? IpAddress { get; init; }

    public class ChildInput
    {
        public required string FirstName { get; init; }
        public required string LastName { get; init; }
        public required DateOnly DateOfBirth { get; init; }
        public string? SchoolName { get; init; }
        public string? SchoolCode { get; init; }
        public IDictionary<string, string> AdditionalFields { get; init; } = new Dictionary<string, string>();
    }
}
```

```csharp
// src/SEBT.Portal.UseCases/EnrollmentCheck/CheckEnrollmentCommandHandler.cs
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using SEBT.Portal.Core.Models.EnrollmentCheck;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.Results;
using SEBT.Portal.StatesPlugins.Interfaces;
using SEBT.Portal.StatesPlugins.Interfaces.Models.EnrollmentCheck;

namespace SEBT.Portal.UseCases.EnrollmentCheck;

public class CheckEnrollmentCommandHandler(
    IEnrollmentCheckService enrollmentCheckService,
    IEnrollmentCheckSubmissionLogger submissionLogger,
    ILogger<CheckEnrollmentCommandHandler> logger)
    : ICommandHandler<CheckEnrollmentCommand, EnrollmentCheckResult>
{
    public async Task<Result<EnrollmentCheckResult>> Handle(
        CheckEnrollmentCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.Children.Count == 0)
        {
            return Result<EnrollmentCheckResult>.ValidationFailed(
                new[] { new ValidationError("Children", "At least one child is required.") });
        }

        var request = new EnrollmentCheckRequest
        {
            Children = command.Children.Select(c => new ChildCheckRequest
            {
                CheckId = Guid.NewGuid(),
                FirstName = c.FirstName,
                LastName = c.LastName,
                DateOfBirth = c.DateOfBirth,
                SchoolName = c.SchoolName,
                SchoolCode = c.SchoolCode,
                AdditionalFields = c.AdditionalFields
            }).ToList()
        };

        EnrollmentCheckResult result;
        try
        {
            result = await enrollmentCheckService.CheckEnrollmentAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Enrollment check plugin failed");
            return Result<EnrollmentCheckResult>.PreconditionFailed(
                PreconditionFailedReason.ServiceUnavailable,
                "Enrollment check service is temporarily unavailable.");
        }

        // Log de-identified submission (fire and forget, don't fail the request)
        try
        {
            var submission = new EnrollmentCheckSubmission
            {
                SubmissionId = Guid.NewGuid(),
                CheckedAtUtc = DateTime.UtcNow,
                ChildrenChecked = result.Results.Count,
                IpAddressHash = HashIpAddress(command.IpAddress),
                ChildResults = result.Results.Select(r => new DeidentifiedChildResult
                {
                    BirthYear = r.DateOfBirth.Year,
                    Status = r.Status.ToString(),
                    EligibilityType = r.EligibilityType?.ToString(),
                    SchoolName = r.SchoolName
                }).ToList()
            };

            await submissionLogger.LogSubmissionAsync(submission, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to log enrollment check submission (non-fatal)");
        }

        return Result<EnrollmentCheckResult>.Success(result);
    }

    private static string? HashIpAddress(string? ipAddress)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
            return null;

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(ipAddress));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
```

**NOTE:** The exact `Result<T>`, `ICommand<T>`, `ICommandHandler<T,R>`, `ValidationError`, and `PreconditionFailedReason` types must match the existing Kernel patterns. Check `src/SEBT.Portal.Kernel/` for the exact signatures. The `PreconditionFailedReason.ServiceUnavailable` value may need to be added to the enum if it doesn't exist — if so, use an appropriate existing value or add it.

**Step 4: Run tests — verify they pass**

Run: `dotnet test test/SEBT.Portal.Tests/ --filter "FullyQualifiedName~CheckEnrollmentCommandHandlerTests"`
Expected: 4 tests pass

**Step 5: Commit**

Commit message: `DC-172: Add CheckEnrollmentCommandHandler use case`

### Task 18: Create the API controller and models

**Files:**
- Create: `src/SEBT.Portal.Api/Models/EnrollmentCheck/EnrollmentCheckApiRequest.cs`
- Create: `src/SEBT.Portal.Api/Models/EnrollmentCheck/EnrollmentCheckApiResponse.cs`
- Create: `src/SEBT.Portal.Api/Controllers/EnrollmentCheck/EnrollmentCheckController.cs`
- Create: `test/SEBT.Portal.Tests/Unit/Controllers/EnrollmentCheckControllerTests.cs`

**Step 1: Write the failing controller test**

```csharp
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using SEBT.Portal.Api.Controllers.EnrollmentCheck;
using SEBT.Portal.Api.Models.EnrollmentCheck;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.Results;
using SEBT.Portal.StatesPlugins.Interfaces.Models.EnrollmentCheck;
using SEBT.Portal.UseCases.EnrollmentCheck;

namespace SEBT.Portal.Tests.Unit.Controllers;

public class EnrollmentCheckControllerTests
{
    private readonly ICommandHandler<CheckEnrollmentCommand, EnrollmentCheckResult> _handler =
        Substitute.For<ICommandHandler<CheckEnrollmentCommand, EnrollmentCheckResult>>();

    [Fact]
    public async Task CheckEnrollment_WithValidRequest_ReturnsOk()
    {
        var controller = new EnrollmentCheckController();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        _handler.Handle(Arg.Any<CheckEnrollmentCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<EnrollmentCheckResult>.Success(new EnrollmentCheckResult
            {
                Results = new List<ChildCheckResult>
                {
                    new()
                    {
                        CheckId = Guid.NewGuid(),
                        FirstName = "Jane",
                        LastName = "Doe",
                        DateOfBirth = new DateOnly(2015, 3, 12),
                        Status = EnrollmentStatus.Match,
                        SchoolName = "Lincoln Elementary"
                    }
                }
            }));

        var request = new EnrollmentCheckApiRequest
        {
            Children = new List<ChildCheckApiRequest>
            {
                new()
                {
                    FirstName = "Jane",
                    LastName = "Doe",
                    DateOfBirth = "2015-03-12",
                    SchoolName = "Lincoln Elementary"
                }
            }
        };

        var result = await controller.CheckEnrollment(_handler, request);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<EnrollmentCheckApiResponse>(okResult.Value);
        Assert.Single(response.Results);
        Assert.Equal("Match", response.Results[0].Status);
    }

    [Fact]
    public async Task CheckEnrollment_WithInvalidDateFormat_ReturnsBadRequest()
    {
        var controller = new EnrollmentCheckController();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        var request = new EnrollmentCheckApiRequest
        {
            Children = new List<ChildCheckApiRequest>
            {
                new()
                {
                    FirstName = "Jane",
                    LastName = "Doe",
                    DateOfBirth = "not-a-date",
                    SchoolName = "Lincoln Elementary"
                }
            }
        };

        var result = await controller.CheckEnrollment(_handler, request);

        Assert.IsType<BadRequestObjectResult>(result);
    }
}
```

**Step 2: Run tests — verify they fail**

Run: `dotnet test test/SEBT.Portal.Tests/ --filter "FullyQualifiedName~EnrollmentCheckControllerTests"`
Expected: FAIL — types don't exist yet

**Step 3: Create the API models**

```csharp
// src/SEBT.Portal.Api/Models/EnrollmentCheck/EnrollmentCheckApiRequest.cs
namespace SEBT.Portal.Api.Models.EnrollmentCheck;

public class EnrollmentCheckApiRequest
{
    public IList<ChildCheckApiRequest> Children { get; set; } = new List<ChildCheckApiRequest>();
}

public class ChildCheckApiRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string DateOfBirth { get; set; } = string.Empty;
    public string? SchoolName { get; set; }
    public string? SchoolCode { get; set; }
    public IDictionary<string, string> AdditionalFields { get; set; } = new Dictionary<string, string>();
}
```

```csharp
// src/SEBT.Portal.Api/Models/EnrollmentCheck/EnrollmentCheckApiResponse.cs
namespace SEBT.Portal.Api.Models.EnrollmentCheck;

public class EnrollmentCheckApiResponse
{
    public IList<ChildCheckApiResponse> Results { get; init; } = new List<ChildCheckApiResponse>();
    public string? Message { get; init; }
}

public class ChildCheckApiResponse
{
    public string CheckId { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string DateOfBirth { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public double? MatchConfidence { get; init; }
    public string? EligibilityType { get; init; }
    public string? SchoolName { get; init; }
    public string? StatusMessage { get; init; }
}
```

**Step 4: Create the controller**

```csharp
// src/SEBT.Portal.Api/Controllers/EnrollmentCheck/EnrollmentCheckController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SEBT.Portal.Api.Models.EnrollmentCheck;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.Results;
using SEBT.Portal.StatesPlugins.Interfaces.Models.EnrollmentCheck;
using SEBT.Portal.UseCases.EnrollmentCheck;

namespace SEBT.Portal.Api.Controllers.EnrollmentCheck;

[ApiController]
[Route("api/enrollment")]
public class EnrollmentCheckController : ControllerBase
{
    [HttpPost("check")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(EnrollmentCheckApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> CheckEnrollment(
        [FromServices] ICommandHandler<CheckEnrollmentCommand, EnrollmentCheckResult> handler,
        [FromBody] EnrollmentCheckApiRequest request,
        CancellationToken cancellationToken = default)
    {
        // Parse and validate date formats
        var children = new List<CheckEnrollmentCommand.ChildInput>();
        foreach (var child in request.Children)
        {
            if (!DateOnly.TryParse(child.DateOfBirth, out var dob))
            {
                return BadRequest(new ErrorResponse($"Invalid date format for child '{child.FirstName} {child.LastName}': '{child.DateOfBirth}'"));
            }

            children.Add(new CheckEnrollmentCommand.ChildInput
            {
                FirstName = child.FirstName,
                LastName = child.LastName,
                DateOfBirth = dob,
                SchoolName = child.SchoolName,
                SchoolCode = child.SchoolCode,
                AdditionalFields = child.AdditionalFields
            });
        }

        var command = new CheckEnrollmentCommand
        {
            Children = children,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        };

        var result = await handler.Handle(command, cancellationToken);

        return result.ToActionResult(
            successMap: data => Ok(MapToApiResponse(data)),
            failureMap: r => r switch
            {
                ValidationFailedResult<EnrollmentCheckResult> validation =>
                    BadRequest(new ErrorResponse("Validation failed.", validation.Errors)),
                PreconditionFailedResult<EnrollmentCheckResult> =>
                    StatusCode(StatusCodes.Status503ServiceUnavailable,
                        new ErrorResponse("Enrollment check service is temporarily unavailable.")),
                _ => StatusCode(StatusCodes.Status500InternalServerError,
                    new ErrorResponse("An unexpected error occurred."))
            });
    }

    private static EnrollmentCheckApiResponse MapToApiResponse(EnrollmentCheckResult result)
    {
        return new EnrollmentCheckApiResponse
        {
            Results = result.Results.Select(r => new ChildCheckApiResponse
            {
                CheckId = r.CheckId.ToString(),
                FirstName = r.FirstName,
                LastName = r.LastName,
                DateOfBirth = r.DateOfBirth.ToString("yyyy-MM-dd"),
                Status = r.Status.ToString(),
                MatchConfidence = r.MatchConfidence,
                EligibilityType = r.EligibilityType?.ToString(),
                SchoolName = r.SchoolName,
                StatusMessage = r.StatusMessage
            }).ToList(),
            Message = result.ResponseMessage
        };
    }
}
```

**Step 5: Run tests — verify they pass**

Run: `dotnet test test/SEBT.Portal.Tests/ --filter "FullyQualifiedName~EnrollmentCheckControllerTests"`
Expected: 2 tests pass

**Step 6: Commit**

Commit message: `DC-172: Add EnrollmentCheckController and API models`

### Task 19: Register services in DI and add rate limiting

**Files:**
- Modify: `src/SEBT.Portal.Api/Program.cs`
- Modify: `src/SEBT.Portal.Infrastructure/Dependencies.cs`
- Modify: `src/SEBT.Portal.Api/appsettings.json` (or equivalent example file)

**Step 1: Register the submission logger in Infrastructure DI**

In `Dependencies.cs` (the `AddPortalInfrastructureServices` method), add:

```csharp
services.AddScoped<IEnrollmentCheckSubmissionLogger, EnrollmentCheckSubmissionLogger>();
```

Add the required usings.

**Step 2: Add rate limit configuration**

In `appsettings.json` (or `appsettings.Development.example.json`), add:

```json
"EnrollmentCheckRateLimitSettings": {
    "PermitLimit": 10,
    "WindowMinutes": 1.0
}
```

**Step 3: Add rate limit policy in Program.cs**

Follow the existing OTP rate limit pattern. Add a new `enrollment-check-policy` alongside the existing `otp-policy` in the `AddRateLimiter` section.

**Step 4: Apply rate limit to controller**

Add `[EnableRateLimiting("enrollment-check-policy")]` attribute to the `CheckEnrollment` action method.

**Step 5: Verify it builds**

Run: `dotnet build`
Expected: Build succeeded

**Step 6: Commit**

Commit message: `DC-172: Register enrollment check services and rate limiting`

### Task 20: Add integration test for enrollment check endpoint

**Files:**
- Create: `test/SEBT.Portal.Tests/Integration/EnrollmentCheckEndpointTests.cs`

**Step 1: Write the integration test**

Use the existing `PortalWebApplicationFactory` pattern (created in our earlier work):

```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SEBT.Portal.Api.Models.EnrollmentCheck;

namespace SEBT.Portal.Tests.Integration;

public class EnrollmentCheckEndpointTests : IClassFixture<PortalWebApplicationFactory>
{
    private readonly HttpClient _client;

    public EnrollmentCheckEndpointTests(PortalWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PostEnrollmentCheck_WithEmptyChildren_ReturnsBadRequest()
    {
        var request = new EnrollmentCheckApiRequest
        {
            Children = new List<ChildCheckApiRequest>()
        };

        var response = await _client.PostAsJsonAsync("/api/enrollment/check", request);

        // Empty children list should fail validation in the handler
        // The exact status depends on whether the handler returns ValidationFailed
        // which maps to 400, or if the controller catches it differently
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostEnrollmentCheck_WithInvalidDate_ReturnsBadRequest()
    {
        var request = new EnrollmentCheckApiRequest
        {
            Children = new List<ChildCheckApiRequest>
            {
                new()
                {
                    FirstName = "Jane",
                    LastName = "Doe",
                    DateOfBirth = "not-a-date",
                    SchoolName = "Lincoln Elementary"
                }
            }
        };

        var response = await _client.PostAsJsonAsync("/api/enrollment/check", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostEnrollmentCheck_ReturnsJsonContentType()
    {
        var request = new EnrollmentCheckApiRequest
        {
            Children = new List<ChildCheckApiRequest>
            {
                new()
                {
                    FirstName = "Jane",
                    LastName = "Doe",
                    DateOfBirth = "2015-03-12",
                    SchoolName = "Lincoln Elementary"
                }
            }
        };

        var response = await _client.PostAsJsonAsync("/api/enrollment/check", request);

        // With no real plugin loaded, this will return 503 (service unavailable)
        // since IEnrollmentCheckService won't be registered.
        // The important thing is we get a JSON response back.
        Assert.Equal("application/json",
            response.Content.Headers.ContentType?.MediaType);
    }
}
```

**NOTE:** With `PortalWebApplicationFactory` mocking out database services but no enrollment check plugin loaded, the endpoint will likely return 503 or 500. If `IEnrollmentCheckService` is not registered (no plugin), the handler injection will fail. Consider adding a mock for `IEnrollmentCheckService` in the factory's `ConfigureServices`, or adding a default/fallback registration. Adjust the test expectations based on actual behavior during implementation.

**Step 2: Run tests**

Run: `dotnet test test/SEBT.Portal.Tests/ --filter "FullyQualifiedName~EnrollmentCheckEndpointTests"`
Expected: Tests pass (adjust expectations based on actual behavior)

**Step 3: Commit**

Commit message: `DC-172: Add enrollment check endpoint integration tests`

### Task 21: Run full test suite and verify

**Step 1: Run all backend tests (excluding SqlServer/Docker tests)**

Run: `dotnet test --filter "Category!=SqlServer"`
Expected: All tests pass

**Step 2: Verify build is clean**

Run: `dotnet build`
Expected: 0 warnings, 0 errors

---

## Summary of Commits

| Phase | Repo | Commit Message |
|-------|------|----------------|
| 1 | state-connector | `DC-172: Add IEnrollmentCheckService plugin interface and enrollment check models` |
| 2 | dc-connector | `DC-172: Add DcEnrollmentCheckService with stored procedure integration` |
| 3 | co-connector | `DC-172: Add ColoradoEnrollmentCheckService with CBMS API integration` |
| 4 | main portal | `DC-172: Register IEnrollmentCheckService in plugin loader` |
| 4 | main portal | `DC-172: Add EnrollmentCheckSubmission entity and EF migration` |
| 4 | main portal | `DC-172: Add enrollment check submission persistence (de-identified)` |
| 4 | main portal | `DC-172: Add CheckEnrollmentCommandHandler use case` |
| 4 | main portal | `DC-172: Add EnrollmentCheckController and API models` |
| 4 | main portal | `DC-172: Register enrollment check services and rate limiting` |
| 4 | main portal | `DC-172: Add enrollment check endpoint integration tests` |

## Notes for Implementation

1. **Build order matters.** The state connector NuGet package must be published before any consuming repo can restore. After Task 6, all other repos need `dotnet restore` to pick up the new interface.

2. **Stored procedure compatibility.** The DC `sp_CheckEligibility` implementation is based on the legacy dc-portal's proc signature. The exact parameter names and types may differ in the DC connector's database. Check `/scripts/sql/` in the dc-connector repo and adjust Task 9 accordingly.

3. **CO API status mappings.** The CBMS API eligibility status strings ("ELIGIBLE", "ENROLLED", etc.) are educated guesses. The actual values need to be confirmed against CBMS API documentation or sandbox testing. The `MapEnrollmentStatus` and `MapEligibilityType` methods in Task 12 should be updated once real values are known.

4. **Kernel types.** The exact `Result<T>`, `ICommand<T>`, `ICommandHandler<T,R>`, `ValidationError`, and `PreconditionFailedReason` types must match what's in `src/SEBT.Portal.Kernel/`. If `PreconditionFailedReason.ServiceUnavailable` doesn't exist, either add it or use an appropriate existing value.

5. **Rate limiting.** The enrollment check rate limit policy follows the OTP pattern but with its own settings. The per-IP partitioning doesn't need email extraction middleware since the endpoint is unauthenticated.

6. **Integration test factory.** The `PortalWebApplicationFactory` may need a mock for `IEnrollmentCheckService` added to its `ConfigureServices` to prevent DI failures when no plugin is loaded.
