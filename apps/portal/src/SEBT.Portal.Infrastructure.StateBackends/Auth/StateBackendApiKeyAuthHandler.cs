using SEBT.Portal.Core.StateBackends;
using SEBT.Portal.Core.StateBackends.Configuration.Auth;

namespace SEBT.Portal.Infrastructure.StateBackends.Auth;

/// <summary>
/// Applies an API-key scheme by setting the configured header to the resolved key. The key is
/// resolved via <see cref="IStateBackendSecretResolver"/> — never inlined.
/// </summary>
public sealed class StateBackendApiKeyAuthHandler : DelegatingHandler
{
    private readonly StateBackendApiKeyAuthScheme _scheme;
    private readonly IStateBackendSecretResolver _secretResolver;

    public StateBackendApiKeyAuthHandler(
        StateBackendApiKeyAuthScheme scheme,
        IStateBackendSecretResolver secretResolver)
    {
        ArgumentNullException.ThrowIfNull(scheme);
        ArgumentNullException.ThrowIfNull(secretResolver);

        _scheme = scheme;
        _secretResolver = secretResolver;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string key = _secretResolver.Resolve(_scheme.KeyRef);
        request.Headers.Remove(_scheme.Header);
        request.Headers.Add(_scheme.Header, key);

        return base.SendAsync(request, cancellationToken);
    }
}
