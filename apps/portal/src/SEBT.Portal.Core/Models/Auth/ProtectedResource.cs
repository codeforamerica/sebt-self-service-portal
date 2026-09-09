namespace SEBT.Portal.Core.Models.Auth;

/// <summary>
/// A resource protected by identity assurance requirements.
/// Each resource may have view and/or write requirements configured.
/// </summary>
public enum ProtectedResource
{
    Address,
    Email,
    Phone,
    Household,
    Card
}
