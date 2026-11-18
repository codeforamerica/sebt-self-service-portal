namespace SEBT.Portal.Core.Models.UserActions;

/// <summary>
/// The type of action a user took in the portal.
/// </summary>
public enum UserActionType
{
    /// <summary>A new mailing address sent to the state, recorded for every case in the household.</summary>
    UpdateAddress = 1,

    /// <summary>A replacement card request sent to the state, recorded for each case the user selected.</summary>
    RequestCardReplacement = 2
}
