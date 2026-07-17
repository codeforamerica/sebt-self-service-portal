namespace SEBT.Portal.Core.Models.Auth;

/// <summary>
/// Result of an identity proofing evaluation: whether the user is allowed
/// and what level would be required if they are not.
/// </summary>
public readonly record struct IdProofingDecision(
    bool IsAllowed,
    UserIalLevel RequiredLevel);
