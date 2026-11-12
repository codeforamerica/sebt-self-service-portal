namespace SEBT.Portal.Core.Models.UserActions;

/// <summary>
/// The outcome of a user action in the portal.
/// </summary>
public enum UserActionOutcome
{
    /// <summary>The state's system accepted the request.</summary>
    Succeeded = 1,

    /// <summary>The request errored or the state's system could not be reached.</summary>
    Failed = 2,

    /// <summary>The state's system deliberately declined the request.</summary>
    Rejected = 3
}
