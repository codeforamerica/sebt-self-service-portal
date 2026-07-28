using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.StateBackends.Configuration;

namespace SEBT.Portal.Core.StateBackends;

public interface IStateBackend
{
    StateBackendCapabilities Capabilities { get; }

    Task<HouseholdLookupResult> LookupHouseholdAsync(
        HouseholdLookupRequest request, CancellationToken cancellationToken = default);

    Task<CardReplacementResult> RequestCardReplacementAsync(
        CardReplacementRequest request, CancellationToken cancellationToken = default);

    // single backend call → single result (no per-case success channel)
    Task<AddressUpdateResult> UpdateAddressAsync(
        AddressUpdateRequest request, CancellationToken cancellationToken = default);

    Task<EnrollmentCheckResult> CheckEnrollmentAsync(
        EnrollmentCheckRequest request, CancellationToken cancellationToken = default);

    Task<StateBackendHealth> GetHealthAsync(CancellationToken cancellationToken = default);
}

public sealed record IdentitySignal(string Type, string Value, bool Verified);
public enum HouseholdLookupStatus { Found, NotFound, Ambiguous }

/// <summary>
/// A household lookup request. <see cref="Signals"/> are household-search keys (email, phone,
/// etc.). <see cref="IsProofed"/> and <see cref="PortalUuid"/> are caller context — facts about
/// the authenticated user, not household search keys.
/// </summary>
/// <remarks>
/// <see cref="IsProofed"/> is supplied by the portal; the portal owns the IAL→proofed threshold.
/// The request binder only passes this bool through to the state backend — it never computes an
/// authorization decision. The DC backend gates its email-lookup branch on this flag, so it must
/// reflect the caller's real proofing status, never a hardcoded value.
/// </remarks>
public sealed record HouseholdLookupRequest(IReadOnlyList<IdentitySignal> Signals)
{
    public bool IsProofed { get; init; }
    public string? PortalUuid { get; init; }
}
public sealed record HouseholdLookupResult(HouseholdLookupStatus Status, HouseholdData? Household);

public sealed record CardDetails();

/// <summary>
/// A mailing-address update for a household, routed by a BATCH of opaque, self-describing
/// <see cref="CaseIds"/> tokens (same token shape card replacement uses — packed on a prior read,
/// decoded here). Address update is household-level, so the write may span every case the
/// household owns: DC resolves one household identifier shared across the batch; CO collects each
/// case's per-case write-id into a PATCH array. The driver decodes the tokens and feeds the
/// decoded routing fields (plus the address scalars) into the request binding.
/// </summary>
/// <remarks>
/// Transport-free: this carries only what the driver needs. There is no per-case success channel —
/// both backends perform a single call and report a single outcome (see <see cref="AddressUpdateResult"/>).
/// </remarks>
public sealed record AddressUpdateRequest(IReadOnlyList<string> CaseIds, AddressUpdateAddress Address);

/// <summary>The validated mailing-address scalars to persist. Transport-free.</summary>
public sealed record AddressUpdateAddress
{
    public string? Line1 { get; init; }
    public string? Line2 { get; init; }
    public string? City { get; init; }
    public string? State { get; init; }
    public string? Zip { get; init; }
}

/// <summary>
/// Canonical address-update outcome. Mirrors the state-connector contract's result shape:
/// success, a policy rejection (household not eligible), or a backend error. No per-case failure
/// channel — a single backend call yields a single result.
/// </summary>
public sealed record AddressUpdateResult
{
    /// <summary>Whether the address was successfully persisted.</summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Whether the failure is a policy rejection (household not eligible for portal address
    /// updates) rather than a technical backend error.
    /// </summary>
    public bool IsPolicyRejection { get; init; }

    /// <summary>Machine-readable error code for frontend/analytics consumption.</summary>
    public string? ErrorCode { get; init; }

    /// <summary>Human-readable error message.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>The address was persisted successfully.</summary>
    public static AddressUpdateResult Success() =>
        new() { IsSuccess = true };

    /// <summary>The household is not eligible to update their address via the portal.</summary>
    public static AddressUpdateResult PolicyRejected(string code, string message) =>
        new() { IsSuccess = false, IsPolicyRejection = true, ErrorCode = code, ErrorMessage = message };

    /// <summary>The state backend returned an error.</summary>
    public static AddressUpdateResult BackendError(string code, string message) =>
        new() { IsSuccess = false, IsPolicyRejection = false, ErrorCode = code, ErrorMessage = message };
}

/// <summary>
/// A card-replacement request routed by an OPAQUE, self-describing <see cref="CaseId"/> token.
/// The token was composed on a prior read (see the response mapping's caseId composition): it
/// packs the routing fields a write needs. The driver decodes it and feeds the decoded fields
/// into the request binding — the portal never has to understand the token's shape.
/// </summary>
/// <remarks>
/// Cooldown, persistence, and hashing stay PORTAL-side. This request carries only what the
/// driver needs to perform the backend call.
/// </remarks>
public sealed record CardReplacementRequest(string CaseId)
{
    /// <summary>Optional reason the UI collected. Null when unspecified.</summary>
    public string? Reason { get; init; }
}

/// <summary>
/// Canonical card-replacement outcome. Mirrors the state-connector contract's result shape:
/// success, a policy rejection (household not eligible), or a backend error.
/// </summary>
public sealed record CardReplacementResult
{
    /// <summary>Whether the replacement was successfully initiated.</summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Whether the failure is a policy rejection (household not eligible for portal card
    /// replacement) rather than a technical backend error.
    /// </summary>
    public bool IsPolicyRejection { get; init; }

    /// <summary>Machine-readable error code for frontend/analytics consumption.</summary>
    public string? ErrorCode { get; init; }

    /// <summary>Human-readable error message.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>The replacement was initiated successfully.</summary>
    public static CardReplacementResult Success() =>
        new() { IsSuccess = true };

    /// <summary>The household is not eligible to request a replacement via the portal.</summary>
    public static CardReplacementResult PolicyRejected(string code, string message) =>
        new() { IsSuccess = false, IsPolicyRejection = true, ErrorCode = code, ErrorMessage = message };

    /// <summary>The state backend returned an error.</summary>
    public static CardReplacementResult BackendError(string code, string message) =>
        new() { IsSuccess = false, IsPolicyRejection = false, ErrorCode = code, ErrorMessage = message };
}

/// <summary>
/// An enrollment-eligibility check for a batch of children. Each child carries the identity fields
/// a backend match reads (name + date of birth) plus a caller-supplied <see cref="EnrollmentChild.CheckId"/>
/// the result echoes back. Transport-free: the driver turns each child into one or more backend
/// request rows (see the enrollment op's <c>expand</c> strategy) and correlates the response back
/// to the originating child.
/// </summary>
public sealed record EnrollmentCheckRequest(IReadOnlyList<EnrollmentChild> Children);

/// <summary>One child to check. <see cref="CheckId"/> is an opaque caller correlation id echoed on the result.</summary>
public sealed record EnrollmentChild(string CheckId, string FirstName, string LastName, DateOnly DateOfBirth);

/// <summary>The per-child enrollment outcomes, one entry per requested child, in request order.</summary>
public sealed record EnrollmentCheckResult(IReadOnlyList<EnrollmentChildResult> Results);

/// <summary>
/// One child's enrollment outcome. <see cref="IsMatch"/> is the fan-in verdict: true when ANY of the
/// child's candidate request rows produced a matching backend row.
/// </summary>
public sealed record EnrollmentChildResult(string CheckId, bool IsMatch);

public sealed record StateBackendHealth(bool IsHealthy);
