namespace SEBT.Portal.Core.Models.UserActions;

/// <summary>
/// The type of action a user took in the portal.
/// </summary>
public enum UserActionType
{
    /// <summary>The user submitted an address update.</summary>
    UpdateAddress = 1,

    /// <summary>The user requested a card replacement.</summary>
    RequestCardReplacement = 2
}
