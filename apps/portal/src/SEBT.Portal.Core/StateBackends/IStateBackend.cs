using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.StateBackends.Configuration;

namespace SEBT.Portal.Core.StateBackends;

public enum HouseholdLookupStatus { Found, NotFound, Ambiguous }

public interface IStateBackend
{
    StateBackendCapabilities Capabilities { get; }

    Task<HouseholdLookupResult> LookupHouseholdAsync(
        HouseholdLookupRequest request, CancellationToken cancellationToken = default);

    Task<CardReplacementResult> RequestCardReplacementAsync(
        CardReplacementRequest request, CancellationToken cancellationToken = default);

    Task<AddressUpdateResult> UpdateAddressAsync(
        AddressUpdateRequest request, CancellationToken cancellationToken = default);

    Task<EnrollmentCheckResult> CheckEnrollmentAsync(
        EnrollmentCheckRequest request, CancellationToken cancellationToken = default);

    Task<StateBackendHealth> GetHealthAsync(CancellationToken cancellationToken = default);
}

public sealed record IdentitySignal(string Type, string Value, bool Verified);

/// <summary>
/// A household lookup request. <see cref="Signals"/> are household-search keys; <see cref="IsProofed"/>
/// and <see cref="PortalUuid"/> are caller context about the authenticated user.
/// </summary>
/// <remarks>
/// <see cref="IsProofed"/> is passed through to the backend, never used for an authorization decision
/// here; DC gates its email-lookup branch on it, so it must reflect the caller's real proofing status.
/// </remarks>
public sealed record HouseholdLookupRequest(IReadOnlyList<IdentitySignal> Signals)
{
    public bool IsProofed { get; init; }
    public string? PortalUuid { get; init; }
}
public sealed record HouseholdLookupResult(HouseholdLookupStatus Status, HouseholdData? Household);

public sealed record CardDetails();

/// <summary>
/// A household-level mailing-address update, routed by a batch of opaque <see cref="CaseIds"/> tokens
/// spanning every case the household owns. No per-case success channel — a single backend call yields
/// a single <see cref="AddressUpdateResult"/>.
/// </summary>
public sealed record AddressUpdateRequest(IReadOnlyList<string> CaseIds, AddressUpdateAddress Address);

/// <summary>The validated mailing-address scalars to persist.</summary>
public sealed record AddressUpdateAddress
{
    public string? Line1 { get; init; }
    public string? Line2 { get; init; }
    public string? City { get; init; }
    public string? State { get; init; }
    public string? Zip { get; init; }
}

/// <summary>
/// Canonical address-update outcome: success, a policy rejection (household not eligible), or a
/// backend error.
/// </summary>
public sealed record AddressUpdateResult
{
    public bool IsSuccess { get; init; }

    /// <summary>The failure is a policy rejection rather than a technical backend error.</summary>
    public bool IsPolicyRejection { get; init; }

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public static AddressUpdateResult Success() =>
        new() { IsSuccess = true };

    public static AddressUpdateResult PolicyRejected(string code, string message) =>
        new() { IsSuccess = false, IsPolicyRejection = true, ErrorCode = code, ErrorMessage = message };

    public static AddressUpdateResult BackendError(string code, string message) =>
        new() { IsSuccess = false, IsPolicyRejection = false, ErrorCode = code, ErrorMessage = message };
}

/// <summary>
/// A card-replacement request routed by an opaque <see cref="CaseId"/> token; the driver decodes it
/// into the routing fields a write needs. Cooldown, persistence, and hashing stay portal-side.
/// </summary>
public sealed record CardReplacementRequest(string CaseId)
{
    public string? Reason { get; init; }
}

/// <summary>
/// Canonical card-replacement outcome: success, a policy rejection (household not eligible), or a
/// backend error.
/// </summary>
public sealed record CardReplacementResult
{
    public bool IsSuccess { get; init; }

    /// <summary>The failure is a policy rejection rather than a technical backend error.</summary>
    public bool IsPolicyRejection { get; init; }

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public static CardReplacementResult Success() =>
        new() { IsSuccess = true };

    public static CardReplacementResult PolicyRejected(string code, string message) =>
        new() { IsSuccess = false, IsPolicyRejection = true, ErrorCode = code, ErrorMessage = message };

    public static CardReplacementResult BackendError(string code, string message) =>
        new() { IsSuccess = false, IsPolicyRejection = false, ErrorCode = code, ErrorMessage = message };
}

/// <summary>An enrollment-eligibility check for a batch of children.</summary>
public sealed record EnrollmentCheckRequest(IReadOnlyList<EnrollmentChild> Children);

/// <summary>One child to check. <see cref="CheckId"/> is an opaque caller correlation id echoed on the result.</summary>
public sealed record EnrollmentChild(string CheckId, string FirstName, string LastName, DateOnly DateOfBirth);

/// <summary>The per-child enrollment outcomes, one entry per requested child, in request order.</summary>
public sealed record EnrollmentCheckResult(IReadOnlyList<EnrollmentChildResult> Results);

/// <summary>
/// One child's enrollment outcome. <see cref="IsMatch"/> is the fan-in verdict: true when any of the
/// child's candidate request rows produced a matching backend row.
/// </summary>
public sealed record EnrollmentChildResult(string CheckId, bool IsMatch);

public sealed record StateBackendHealth(bool IsHealthy);
