using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Infrastructure.Configuration.Validators;
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
            if (settings is not null)
            {
                // Runs while services are being registered, before validate-on-start, so apply the same
                // rules here rather than build a connection from a section they would reject.
                var validation = new RedisSettingsValidator(environment).Validate(Options.DefaultName, settings);
                if (validation.Failed)
                {
                    throw new OptionsValidationException(
                        Options.DefaultName, typeof(RedisSettings), validation.Failures ?? []);
                }
            }

            if (settings?.IsConfigured == true)
            {
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
