using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.StateBackends.Configuration;

namespace SEBT.Portal.Core.StateBackends;

public enum HouseholdLookupStatus { Found, NotFound }

public interface IStateBackend
{
    StateBackendCapabilities Capabilities { get; }

    Task<HouseholdLookupResult> LookupHouseholdAsync(
        HouseholdLookupRequest request, CancellationToken cancellationToken = default);

    Task<WriteResult> RequestCardReplacementAsync(
        CardReplacementRequest request, CancellationToken cancellationToken = default);

    Task<WriteResult> UpdateAddressAsync(
        AddressUpdateRequest request, CancellationToken cancellationToken = default);

    Task<EnrollmentCheckResult> CheckEnrollmentAsync(
        EnrollmentCheckRequest request, CancellationToken cancellationToken = default);

    Task<StateBackendHealth> GetHealthAsync(CancellationToken cancellationToken = default);
}

public sealed record IdentitySignal(string Type, string Value);

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
/// Canonical write outcome (card replacement, address update): success, a policy rejection
/// (household not eligible), or a backend error.
/// </summary>
public sealed record WriteResult
{
    public bool IsSuccess { get; init; }

    /// <summary>The failure is a policy rejection rather than a technical backend error.</summary>
    public bool IsPolicyRejection { get; init; }

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public static WriteResult Success() =>
        new() { IsSuccess = true };

    public static WriteResult PolicyRejected(string code, string message) =>
        new() { IsSuccess = false, IsPolicyRejection = true, ErrorCode = code, ErrorMessage = message };

    public static WriteResult BackendError(string code, string message) =>
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

/// <summary>An enrollment-eligibility check for a batch of children.</summary>
public sealed record EnrollmentCheckRequest(IReadOnlyList<EnrollmentChild> Children);

/// <summary>
/// One child to check. <see cref="CheckId"/> is an opaque caller correlation id echoed on the result.
/// <see cref="SchoolName"/> is the state's school identifier for enrollment matching — optional
/// because the portal doesn't always have it.
/// </summary>
public sealed record EnrollmentChild(
    string CheckId, string FirstName, string LastName, DateOnly DateOfBirth, string? SchoolName = null);

/// <summary>
/// The per-child enrollment outcomes, one entry per requested child, in request order.
/// <see cref="Message"/> is the backend's result-level status text (e.g. CBMS's response-root
/// message), populated only when the response mapping configures a <c>messageField</c>.
/// </summary>
public sealed record EnrollmentCheckResult(
    IReadOnlyList<EnrollmentChildResult> Results, string? Message = null);

/// <summary>
/// One child's enrollment outcome. <see cref="IsMatch"/> is the fan-in verdict: true when any of the
/// child's candidate request rows produced a matching backend row. <see cref="MatchConfidence"/> is
/// the winning row's score under the confidenceThreshold strategy — populated even below the
/// threshold so callers can surface the score that was computed; null under other strategies.
/// <see cref="StatusMessage"/> is the winning row's status text when a <c>statusMessageField</c>
/// is configured.
/// </summary>
public sealed record EnrollmentChildResult(
    string CheckId, bool IsMatch, double? MatchConfidence = null, string? StatusMessage = null);

public sealed record StateBackendHealth(bool IsHealthy);
