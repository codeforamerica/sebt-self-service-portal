using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;

namespace SEBT.Portal.Infrastructure.Configuration.Validators;

/// <summary>
/// Rejects a Redis section that is incomplete, contradictory, or unsafe for the environment.
///
/// Redis is optional: without <c>Redis:Host</c> the portal falls back to in-memory caching and SQL
/// Server locks. Blanking the host over a state file that configures TLS Redis is how a
/// single-instance stack turns Redis off, so a section without a host is not checked. Once a host is
/// set, the TLS values have to agree with each other, and certificate validation may only be skipped
/// in Development.
///
/// <c>ResolveRedisConfigurationOptions</c> runs this too: it reads the section while services are still
/// being registered, before validate-on-start can see it.
/// </summary>
public class RedisSettingsValidator(IHostEnvironment environment) : IValidateOptions<RedisSettings>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, RedisSettings options)
    {
        if (!options.IsConfigured)
        {
            return ValidateOptionsResult.Success;
        }

        // Collected rather than returned one at a time so a single boot tells an operator everything to fix.
        var problems = new List<string>();

        if (options.Port is < 1 or > 65535)
        {
            problems.Add($"Redis:Port must be between 1 and 65535 (got {options.Port}).");
        }

        if (!options.Ssl && !string.IsNullOrWhiteSpace(options.SslHost))
        {
            problems.Add(
                "Redis:SslHost only applies over TLS, so the connection would be unencrypted. " +
                "Set Redis:Ssl to true, or remove Redis:SslHost.");
        }

        if (options.AcceptSelfSignedCertificates)
        {
            if (!options.Ssl)
            {
                problems.Add(
                    "Redis:AcceptSelfSignedCertificates only applies over TLS. " +
                    "Set Redis:Ssl to true, or remove Redis:AcceptSelfSignedCertificates.");
            }

            if (!environment.IsDevelopment())
            {
                problems.Add(
                    "Redis:AcceptSelfSignedCertificates must only be true when ASPNETCORE_ENVIRONMENT is " +
                    "Development. Remove it: Elasticache presents an AWS-signed certificate that .NET trusts natively.");
            }
        }

        return problems.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(problems);
    }
}
