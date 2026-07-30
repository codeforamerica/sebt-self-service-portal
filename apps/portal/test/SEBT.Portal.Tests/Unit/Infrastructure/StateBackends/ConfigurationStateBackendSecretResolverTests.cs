using Microsoft.Extensions.Configuration;
using SEBT.Portal.Infrastructure.StateBackends.Auth;

namespace SEBT.Portal.Tests.Unit.Infrastructure.StateBackends;

public class ConfigurationStateBackendSecretResolverTests
{
    private static ConfigurationStateBackendSecretResolver BuildResolver(
        Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();
        return new ConfigurationStateBackendSecretResolver(configuration);
    }

    [Fact]
    public void Resolve_WithConfiguredKey_ReturnsValue()
    {
        var resolver = BuildResolver(new Dictionary<string, string?>
        {
            ["StateBackend:Auth:ApiKey"] = "super-secret-value",
        });

        var value = resolver.Resolve("StateBackend:Auth:ApiKey");

        Assert.Equal("super-secret-value", value);
    }

    [Fact]
    public void Resolve_WithMissingKey_ThrowsNamingTheReference()
    {
        var resolver = BuildResolver([]);

        var ex = Assert.Throws<InvalidOperationException>(
            () => resolver.Resolve("StateBackend:Auth:ApiKey"));

        Assert.Contains("StateBackend:Auth:ApiKey", ex.Message);
    }

    [Fact]
    public void Resolve_WithEmptyValue_ThrowsNamingTheReference()
    {
        var resolver = BuildResolver(new Dictionary<string, string?>
        {
            ["StateBackend:Auth:ApiKey"] = "",
        });

        var ex = Assert.Throws<InvalidOperationException>(
            () => resolver.Resolve("StateBackend:Auth:ApiKey"));

        Assert.Contains("StateBackend:Auth:ApiKey", ex.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_WithBlankReference_ThrowsArgumentException(string reference)
    {
        var resolver = BuildResolver([]);

        Assert.Throws<ArgumentException>(() => resolver.Resolve(reference));
    }
}
