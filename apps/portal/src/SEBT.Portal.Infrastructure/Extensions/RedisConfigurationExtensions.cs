using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using SEBT.Portal.Core.AppSettings;
using StackExchange.Redis;

namespace SEBT.Portal.Infrastructure.Extensions;

internal static class RedisConfigurationExtensions
{
    extension(IConfiguration? configuration)
    {
        /// <summary>
        /// Resolves Redis configuration from settings. Structured Redis:* settings take
        /// precedence; falls back to the legacy ConnectionStrings:Redis connection string.
        /// Returns null when neither is configured.
        /// </summary>
        internal ConfigurationOptions? ResolveRedisConfigurationOptions(IHostEnvironment environment)
        {
            var settings = configuration?.GetSection(RedisSettings.SectionName).Get<RedisSettings>();
            if (settings?.IsConfigured == true)
            {
                if (settings.AcceptSelfSignedCertificates && !environment.IsDevelopment())
                {
                    throw new InvalidOperationException(
                        "Redis:AcceptSelfSignedCertificates must only be true " +
                        "when ASPNETCORE_ENVIRONMENT == Development. " +
                        "Remove it from configuration — Elasticache presents an AWS-signed cert " +
                        "that .NET trusts natively.");
                }

                var options = new ConfigurationOptions();
                options.EndPoints.Add(settings.Host!, settings.Port);
                if (!string.IsNullOrEmpty(settings.Password))
                {
                    options.Password = settings.Password;
                }

                options.Ssl = settings.Ssl;
                if (!string.IsNullOrEmpty(settings.SslHost))
                {
                    options.SslHost = settings.SslHost;
                }

                if (settings.AcceptSelfSignedCertificates)
                {
                    // Bypasses TLS cert validation for local dev with self-signed certs.
                    // In production, Elasticache presents an AWS-signed cert that .NET
                    // trusts without this — AcceptSelfSignedCertificates must be false.
                    options.CertificateValidation += (_, _, _, _) => true;
                }

                return options;
            }

            var legacyConnectionString = configuration?.GetConnectionString("Redis");

            if (!string.IsNullOrEmpty(legacyConnectionString))
            {
                return ConfigurationOptions.Parse(legacyConnectionString);
            }

            return null;
        }
    }
}
