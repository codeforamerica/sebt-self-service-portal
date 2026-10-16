using Microsoft.Extensions.Logging;
using SEBT.Portal.Api.Startup;
using SEBT.Portal.Core.AppSettings;
using Xunit;

namespace SEBT.Portal.Tests.Unit.Startup;

public class OidcVerificationClaimStartupLogTests
{
    private static readonly OidcSettings OidcEnabled = new() { DiscoveryEndpoint = "https://idp.example/.well-known" };

    [Fact]
    public void Write_WhenOidcEnabled_LogsAllClaimNames()
    {
        var logger = new CapturingLogger();

        OidcVerificationClaimStartupLog.Write(
            OidcEnabled,
            new OidcVerificationClaimSettings
            {
                LevelClaimName = "level",
                DateClaimName = "date",
                FallbackLevelClaimName = "fallbackLevel",
                FallbackDateClaimName = "fallbackDate"
            },
            logger);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal(
            "OIDC verification claims: level level (fallback fallbackLevel), date date (fallback fallbackDate)",
            entry.Message);
    }

    [Fact]
    public void Write_WhenFallbacksUnset_LogsThemAsNone()
    {
        var logger = new CapturingLogger();

        OidcVerificationClaimStartupLog.Write(
            OidcEnabled,
            new OidcVerificationClaimSettings { LevelClaimName = "level", DateClaimName = "date" },
            logger);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(
            "OIDC verification claims: level level (fallback (none)), date date (fallback (none))",
            entry.Message);
    }

    [Fact]
    public void Write_WhenOidcDisabled_LogsNothing()
    {
        var logger = new CapturingLogger();

        OidcVerificationClaimStartupLog.Write(
            new OidcSettings(),
            new OidcVerificationClaimSettings(),
            logger);

        Assert.Empty(logger.Entries);
    }

    private sealed class CapturingLogger : ILogger
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }
}
