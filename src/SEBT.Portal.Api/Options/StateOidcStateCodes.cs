namespace SEBT.Portal.Api.Options;

/// <summary>
/// Holds the list of state codes that have an OIDC login plugin loaded.
/// Used to conditionally map /api/auth/oidc/{state}/login and callback routes.
/// </summary>
public sealed class StateOidcStateCodes
{
    /// <summary>State codes (e.g. "CO") that provide an OIDC login flow.</summary>
    public IReadOnlyList<string> StateCodes { get; }

    /// <summary>Creates the list of state codes with OIDC login support.</summary>
    /// <param name="stateCodes">The state codes to expose OIDC routes for.</param>
    public StateOidcStateCodes(IReadOnlyList<string> stateCodes)
    {
        StateCodes = stateCodes ?? Array.Empty<string>();
    }
}
