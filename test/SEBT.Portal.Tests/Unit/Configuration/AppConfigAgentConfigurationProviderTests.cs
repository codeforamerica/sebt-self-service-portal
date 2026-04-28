using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RichardSzalay.MockHttp;
using SEBT.Portal.Infrastructure.Configuration;
using Xunit;

namespace SEBT.Portal.Tests.Unit.Configuration;

public class AppConfigAgentConfigurationProviderTests : IDisposable
{
    private readonly MockHttpMessageHandler _mockHttpHandler;
    private readonly HttpClient _httpClient;
    private readonly ILogger<AppConfigAgentConfigurationProvider> _logger;

    public AppConfigAgentConfigurationProviderTests()
    {
        _mockHttpHandler = new MockHttpMessageHandler();
        _httpClient = new HttpClient(_mockHttpHandler)
        {
            BaseAddress = new Uri("http://localhost:2772")
        };
        _logger = NullLogger<AppConfigAgentConfigurationProvider>.Instance;
    }

    [Fact]
    public void Load_WithFeatureFlagObjectFormat_ShouldParseCorrectly()
    {
        // Arrange
        var profile = new AppConfigAgentProfile
        {
            BaseUrl = "http://localhost:2772",
            ApplicationId = "test-app",
            EnvironmentId = "test-env",
            ProfileId = "test-profile",
            IsFeatureFlag = true
        };

        var featureFlagsJson = new
        {
            feature1 = new { enabled = true },
            feature2 = new { enabled = false },
            feature3 = new { enabled = true }
        };

        _mockHttpHandler
            .When("http://localhost:2772/applications/test-app/environments/test-env/configurations/test-profile")
            .Respond(HttpStatusCode.OK, "application/json", JsonSerializer.Serialize(featureFlagsJson));

        var provider = new AppConfigAgentConfigurationProvider(_httpClient, profile, _logger, ownsHttpClient: false);

        // Act
        provider.Load();

        // Assert
        Assert.True(provider.TryGet("FeatureManagement:feature1", out var value1));
        Assert.Equal("true", value1);
        Assert.True(provider.TryGet("FeatureManagement:feature2", out var value2));
        Assert.Equal("false", value2);
        Assert.True(provider.TryGet("FeatureManagement:feature3", out var value3));
        Assert.Equal("true", value3);
    }

    [Fact]
    public void Load_WithFeatureFlagSimpleBooleanFormat_ShouldParseCorrectly()
    {
        // Arrange
        var profile = new AppConfigAgentProfile
        {
            BaseUrl = "http://localhost:2772",
            ApplicationId = "test-app",
            EnvironmentId = "test-env",
            ProfileId = "test-profile",
            IsFeatureFlag = true
        };

        var featureFlagsJson = new
        {
            feature1 = true,
            feature2 = false,
            feature3 = true
        };

        _mockHttpHandler
            .When("http://localhost:2772/applications/test-app/environments/test-env/configurations/test-profile")
            .Respond(HttpStatusCode.OK, "application/json", JsonSerializer.Serialize(featureFlagsJson));

        var provider = new AppConfigAgentConfigurationProvider(_httpClient, profile, _logger, ownsHttpClient: false);

        // Act
        provider.Load();

        // Assert
        Assert.True(provider.TryGet("FeatureManagement:feature1", out var value1));
        Assert.Equal("true", value1);
        Assert.True(provider.TryGet("FeatureManagement:feature2", out var value2));
        Assert.Equal("false", value2);
        Assert.True(provider.TryGet("FeatureManagement:feature3", out var value3));
        Assert.Equal("true", value3);
    }

    [Fact]
    public void Load_WithGeneralJson_ShouldFlattenCorrectly()
    {
        // Arrange
        var profile = new AppConfigAgentProfile
        {
            BaseUrl = "http://localhost:2772",
            ApplicationId = "test-app",
            EnvironmentId = "test-env",
            ProfileId = "test-profile",
            IsFeatureFlag = false
        };

        var configJson = new
        {
            Section1 = new
            {
                Key1 = "value1",
                Key2 = 42,
                Key3 = true
            },
            Section2 = new
            {
                Nested = new
                {
                    Key = "nested-value"
                }
            }
        };

        _mockHttpHandler
            .When("http://localhost:2772/applications/test-app/environments/test-env/configurations/test-profile")
            .Respond(HttpStatusCode.OK, "application/json", JsonSerializer.Serialize(configJson));

        var provider = new AppConfigAgentConfigurationProvider(_httpClient, profile, _logger, ownsHttpClient: false);

        // Act
        provider.Load();

        // Assert
        Assert.True(provider.TryGet("Section1:Key1", out var key1));
        Assert.Equal("value1", key1);
        Assert.True(provider.TryGet("Section1:Key2", out var key2));
        Assert.Equal("42", key2);
        Assert.True(provider.TryGet("Section1:Key3", out var key3));
        Assert.Equal("true", key3);
        Assert.True(provider.TryGet("Section2:Nested:Key", out var nestedKey));
        Assert.Equal("nested-value", nestedKey);
    }

    [Fact]
    public void Load_WithHttpError_ShouldNotUpdateConfiguration()
    {
        // Arrange
        var profile = new AppConfigAgentProfile
        {
            BaseUrl = "http://localhost:2772",
            ApplicationId = "test-app",
            EnvironmentId = "test-env",
            ProfileId = "test-profile",
            IsFeatureFlag = true
        };

        _mockHttpHandler
            .When("http://localhost:2772/applications/test-app/environments/test-env/configurations/test-profile")
            .Respond(HttpStatusCode.NotFound);

        var provider = new AppConfigAgentConfigurationProvider(_httpClient, profile, _logger, ownsHttpClient: false);

        // Act
        provider.Load();

        // Assert
        // Should not throw, and configuration should remain empty/default
        Assert.False(provider.TryGet("FeatureManagement:feature1", out _));
    }

    [Fact]
    public void Load_WithNetworkError_ShouldNotThrow()
    {
        // Arrange
        var profile = new AppConfigAgentProfile
        {
            BaseUrl = "http://localhost:2772",
            ApplicationId = "test-app",
            EnvironmentId = "test-env",
            ProfileId = "test-profile",
            IsFeatureFlag = true
        };

        _mockHttpHandler
            .When("http://localhost:2772/applications/test-app/environments/test-env/configurations/test-profile")
            .Throw(new HttpRequestException("Network error"));

        var provider = new AppConfigAgentConfigurationProvider(_httpClient, profile, _logger, ownsHttpClient: false);

        // Act & Assert
        // Should not throw - provider should handle errors gracefully
        var exception = Record.Exception(() => provider.Load());
        Assert.Null(exception);
    }

    [Fact]
    public void Load_WithInvalidJson_ShouldNotThrow()
    {
        // Arrange
        var profile = new AppConfigAgentProfile
        {
            BaseUrl = "http://localhost:2772",
            ApplicationId = "test-app",
            EnvironmentId = "test-env",
            ProfileId = "test-profile",
            IsFeatureFlag = true
        };

        _mockHttpHandler
            .When("http://localhost:2772/applications/test-app/environments/test-env/configurations/test-profile")
            .Respond(HttpStatusCode.OK, "application/json", "invalid json {");

        var provider = new AppConfigAgentConfigurationProvider(_httpClient, profile, _logger, ownsHttpClient: false);

        // Act & Assert
        // Should not throw - provider should handle parsing errors gracefully
        var exception = Record.Exception(() => provider.Load());
        Assert.Null(exception);
    }

    [Fact]
    public void Load_WithUnsupportedContentType_ShouldNotUpdateConfiguration()
    {
        // Arrange
        var profile = new AppConfigAgentProfile
        {
            BaseUrl = "http://localhost:2772",
            ApplicationId = "test-app",
            EnvironmentId = "test-env",
            ProfileId = "test-profile",
            IsFeatureFlag = true
        };

        _mockHttpHandler
            .When("http://localhost:2772/applications/test-app/environments/test-env/configurations/test-profile")
            .Respond(HttpStatusCode.OK, "text/xml", "<xml>data</xml>");

        var provider = new AppConfigAgentConfigurationProvider(_httpClient, profile, _logger, ownsHttpClient: false);

        // Act
        // Provider catches FormatException and logs it, doesn't throw
        var exception = Record.Exception(() => provider.Load());

        // Assert
        Assert.Null(exception);
        // Configuration should not be updated due to unsupported content type
        Assert.False(provider.TryGet("FeatureManagement:anykey", out _));
    }

    [Fact]
    public void Load_WithEmptyResponse_ShouldNotUpdateConfiguration()
    {
        // Arrange
        var profile = new AppConfigAgentProfile
        {
            BaseUrl = "http://localhost:2772",
            ApplicationId = "test-app",
            EnvironmentId = "test-env",
            ProfileId = "test-profile",
            IsFeatureFlag = true
        };

        _mockHttpHandler
            .When("http://localhost:2772/applications/test-app/environments/test-env/configurations/test-profile")
            .Respond(HttpStatusCode.OK, "application/json", "{}");

        var provider = new AppConfigAgentConfigurationProvider(_httpClient, profile, _logger, ownsHttpClient: false);

        // Act
        provider.Load();

        // Assert
        Assert.False(provider.TryGet("FeatureManagement:feature1", out _));
    }

    [Fact]
    public void Load_WithNullValues_ShouldHandleCorrectly()
    {
        // Arrange
        var profile = new AppConfigAgentProfile
        {
            BaseUrl = "http://localhost:2772",
            ApplicationId = "test-app",
            EnvironmentId = "test-env",
            ProfileId = "test-profile",
            IsFeatureFlag = false
        };

        var configJson = new
        {
            Key1 = (string?)null,
            Key2 = "value2"
        };

        _mockHttpHandler
            .When("http://localhost:2772/applications/test-app/environments/test-env/configurations/test-profile")
            .Respond(HttpStatusCode.OK, "application/json", JsonSerializer.Serialize(configJson));

        var provider = new AppConfigAgentConfigurationProvider(_httpClient, profile, _logger, ownsHttpClient: false);

        // Act
        provider.Load();

        // Assert
        Assert.True(provider.TryGet("Key1", out var value1));
        Assert.Null(value1);
        Assert.True(provider.TryGet("Key2", out var value2));
        Assert.Equal("value2", value2);
    }

    [Fact]
    public void Load_WithStringArray_ShouldFlattenWithIndexedKeys()
    {
        // Arrange
        var profile = new AppConfigAgentProfile
        {
            BaseUrl = "http://localhost:2772",
            ApplicationId = "test-app",
            EnvironmentId = "test-env",
            ProfileId = "test-profile",
            IsFeatureFlag = false
        };

        var configJson = new
        {
            Key1 = "value1",
            ArrayKey = new[] { "item1", "item2", "item3" }
        };

        _mockHttpHandler
            .When("http://localhost:2772/applications/test-app/environments/test-env/configurations/test-profile")
            .Respond(HttpStatusCode.OK, "application/json", JsonSerializer.Serialize(configJson));

        var provider = new AppConfigAgentConfigurationProvider(_httpClient, profile, _logger, ownsHttpClient: false);

        // Act
        provider.Load();

        // Assert
        Assert.True(provider.TryGet("Key1", out var key1));
        Assert.Equal("value1", key1);
        Assert.True(provider.TryGet("ArrayKey:0", out var item0));
        Assert.Equal("item1", item0);
        Assert.True(provider.TryGet("ArrayKey:1", out var item1));
        Assert.Equal("item2", item1);
        Assert.True(provider.TryGet("ArrayKey:2", out var item2));
        Assert.Equal("item3", item2);
    }

    [Fact]
    public void Load_WithNestedConfigArray_ShouldFlattenWithSectionAndIndex()
    {
        // Arrange — mirrors the real StateHouseholdId config structure
        var profile = new AppConfigAgentProfile
        {
            BaseUrl = "http://localhost:2772",
            ApplicationId = "test-app",
            EnvironmentId = "test-env",
            ProfileId = "test-profile",
            IsFeatureFlag = false
        };

        var configJson = new
        {
            StateHouseholdId = new
            {
                PreferredHouseholdIdTypes = new[] { "Phone" }
            }
        };

        _mockHttpHandler
            .When("http://localhost:2772/applications/test-app/environments/test-env/configurations/test-profile")
            .Respond(HttpStatusCode.OK, "application/json", JsonSerializer.Serialize(configJson));

        var provider = new AppConfigAgentConfigurationProvider(_httpClient, profile, _logger, ownsHttpClient: false);

        // Act
        provider.Load();

        // Assert
        Assert.True(provider.TryGet("StateHouseholdId:PreferredHouseholdIdTypes:0", out var type0));
        Assert.Equal("Phone", type0);
    }

    [Fact]
    public void Load_WithArrayOfObjects_ShouldFlattenWithIndexAndPropertyKeys()
    {
        // Arrange
        var profile = new AppConfigAgentProfile
        {
            BaseUrl = "http://localhost:2772",
            ApplicationId = "test-app",
            EnvironmentId = "test-env",
            ProfileId = "test-profile",
            IsFeatureFlag = false
        };

        var configJson = new
        {
            Items = new[]
            {
                new { Name = "first", Value = 1 },
                new { Name = "second", Value = 2 }
            }
        };

        _mockHttpHandler
            .When("http://localhost:2772/applications/test-app/environments/test-env/configurations/test-profile")
            .Respond(HttpStatusCode.OK, "application/json", JsonSerializer.Serialize(configJson));

        var provider = new AppConfigAgentConfigurationProvider(_httpClient, profile, _logger, ownsHttpClient: false);

        // Act
        provider.Load();

        // Assert
        Assert.True(provider.TryGet("Items:0:Name", out var name0));
        Assert.Equal("first", name0);
        Assert.True(provider.TryGet("Items:0:Value", out var value0));
        Assert.Equal("1", value0);
        Assert.True(provider.TryGet("Items:1:Name", out var name1));
        Assert.Equal("second", name1);
        Assert.True(provider.TryGet("Items:1:Value", out var value1));
        Assert.Equal("2", value1);
    }

    [Fact]
    public void Load_WithEmptyArray_ShouldProduceNoKeys()
    {
        // Arrange
        var profile = new AppConfigAgentProfile
        {
            BaseUrl = "http://localhost:2772",
            ApplicationId = "test-app",
            EnvironmentId = "test-env",
            ProfileId = "test-profile",
            IsFeatureFlag = false
        };

        // Use raw JSON since anonymous types can't express empty typed arrays cleanly
        var configJson = """{"Key1": "value1", "EmptyArray": []}""";

        _mockHttpHandler
            .When("http://localhost:2772/applications/test-app/environments/test-env/configurations/test-profile")
            .Respond(HttpStatusCode.OK, "application/json", configJson);

        var provider = new AppConfigAgentConfigurationProvider(_httpClient, profile, _logger, ownsHttpClient: false);

        // Act
        provider.Load();

        // Assert
        Assert.True(provider.TryGet("Key1", out var key1));
        Assert.Equal("value1", key1);
        Assert.False(provider.TryGet("EmptyArray:0", out _));
    }

    [Fact]
    public void Load_WithMixedTypeArray_ShouldFlattenAllElementTypes()
    {
        // Arrange
        var profile = new AppConfigAgentProfile
        {
            BaseUrl = "http://localhost:2772",
            ApplicationId = "test-app",
            EnvironmentId = "test-env",
            ProfileId = "test-profile",
            IsFeatureFlag = false
        };

        // Mixed types require raw JSON
        var configJson = """{"Mixed": ["text", 42, true, false, null]}""";

        _mockHttpHandler
            .When("http://localhost:2772/applications/test-app/environments/test-env/configurations/test-profile")
            .Respond(HttpStatusCode.OK, "application/json", configJson);

        var provider = new AppConfigAgentConfigurationProvider(_httpClient, profile, _logger, ownsHttpClient: false);

        // Act
        provider.Load();

        // Assert
        Assert.True(provider.TryGet("Mixed:0", out var v0));
        Assert.Equal("text", v0);
        Assert.True(provider.TryGet("Mixed:1", out var v1));
        Assert.Equal("42", v1);
        Assert.True(provider.TryGet("Mixed:2", out var v2));
        Assert.Equal("true", v2);
        Assert.True(provider.TryGet("Mixed:3", out var v3));
        Assert.Equal("false", v3);
        Assert.True(provider.TryGet("Mixed:4", out var v4));
        Assert.Null(v4);
    }

    [Fact]
    public void Load_WithContentTypeWithCharset_ShouldParseCorrectly()
    {
        // Arrange
        var profile = new AppConfigAgentProfile
        {
            BaseUrl = "http://localhost:2772",
            ApplicationId = "test-app",
            EnvironmentId = "test-env",
            ProfileId = "test-profile",
            IsFeatureFlag = true
        };

        var featureFlagsJson = new
        {
            feature1 = new { enabled = true }
        };

        var content = new StringContent(JsonSerializer.Serialize(featureFlagsJson), Encoding.UTF8, "application/json");
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json")
        {
            CharSet = "utf-8"
        };

        _mockHttpHandler
            .When("http://localhost:2772/applications/test-app/environments/test-env/configurations/test-profile")
            .Respond(HttpStatusCode.OK, content);

        var provider = new AppConfigAgentConfigurationProvider(_httpClient, profile, _logger, ownsHttpClient: false);

        // Act
        provider.Load();

        // Assert
        Assert.True(provider.TryGet("FeatureManagement:feature1", out var value1));
        Assert.Equal("true", value1);
    }

    [Fact]
    public void GetEndpointUrl_ShouldConstructCorrectUrl()
    {
        // Arrange
        var profile = new AppConfigAgentProfile
        {
            BaseUrl = "http://localhost:2772",
            ApplicationId = "test-app",
            EnvironmentId = "test-env",
            ProfileId = "test-profile"
        };

        // Act
        var url = profile.GetEndpointUrl();

        // Assert
        Assert.Equal("http://localhost:2772/applications/test-app/environments/test-env/configurations/test-profile", url);
    }

    [Fact]
    public void GetEndpointUrl_WithTrailingSlash_ShouldTrimCorrectly()
    {
        // Arrange
        var profile = new AppConfigAgentProfile
        {
            BaseUrl = "http://localhost:2772/",
            ApplicationId = "test-app",
            EnvironmentId = "test-env",
            ProfileId = "test-profile"
        };

        // Act
        var url = profile.GetEndpointUrl();

        // Assert
        Assert.Equal("http://localhost:2772/applications/test-app/environments/test-env/configurations/test-profile", url);
    }

    [Fact]
    public void Dispose_ShouldDisposeResources()
    {
        // Arrange
        var profile = new AppConfigAgentProfile
        {
            BaseUrl = "http://localhost:2772",
            ApplicationId = "test-app",
            EnvironmentId = "test-env",
            ProfileId = "test-profile",
            ReloadAfterSeconds = 90
        };

        var provider = new AppConfigAgentConfigurationProvider(_httpClient, profile, _logger, ownsHttpClient: false);
        provider.Load();

        // Act & Assert
        var exception = Record.Exception(() => provider.Dispose());
        Assert.Null(exception);
    }

    [Fact]
    public void ToString_ShouldReturnDescriptiveString()
    {
        // Arrange
        var profile = new AppConfigAgentProfile
        {
            BaseUrl = "http://localhost:2772",
            ApplicationId = "test-app",
            EnvironmentId = "test-env",
            ProfileId = "test-profile",
            ReloadAfterSeconds = 90,
            IsFeatureFlag = true
        };

        var provider = new AppConfigAgentConfigurationProvider(_httpClient, profile, _logger, ownsHttpClient: false);

        // Act
        var result = provider.ToString();

        // Assert
        Assert.Contains("AppConfigAgentConfigurationProvider", result);
        Assert.Contains("test-app:test-env:test-profile:90", result);
        Assert.Contains("Feature Flag", result);
    }

    [Fact]
    public void Load_WithReloadAfterSeconds_ShouldSetupReloadMechanism()
    {
        // Arrange
        var profile = new AppConfigAgentProfile
        {
            BaseUrl = "http://localhost:2772",
            ApplicationId = "test-app",
            EnvironmentId = "test-env",
            ProfileId = "test-profile",
            ReloadAfterSeconds = 1
        };

        var featureFlagsJson = new
        {
            feature1 = new { enabled = true }
        };

        _mockHttpHandler
            .When("http://localhost:2772/applications/test-app/environments/test-env/configurations/test-profile")
            .Respond(HttpStatusCode.OK, "application/json", JsonSerializer.Serialize(featureFlagsJson));

        var provider = new AppConfigAgentConfigurationProvider(_httpClient, profile, _logger, ownsHttpClient: false);

        // Act
        provider.Load();

        // Assert
        Assert.True(provider.TryGet("FeatureManagement:feature1", out var value));
        Assert.Equal("true", value);

        // Calling Load() again should not throw 
        var exception = Record.Exception(() => provider.Load());
        Assert.Null(exception);

        provider.Dispose();
    }

    [Fact]
    public void Load_WithReloadAfterSecondsNull_ShouldNotSetupReload()
    {
        // Arrange
        var profile = new AppConfigAgentProfile
        {
            BaseUrl = "http://localhost:2772",
            ApplicationId = "test-app",
            EnvironmentId = "test-env",
            ProfileId = "test-profile",
            ReloadAfterSeconds = null // This disables the reload mechanism
        };

        var featureFlagsJson = new
        {
            feature1 = new { enabled = true }
        };

        _mockHttpHandler
            .When("http://localhost:2772/applications/test-app/environments/test-env/configurations/test-profile")
            .Respond(HttpStatusCode.OK, "application/json", JsonSerializer.Serialize(featureFlagsJson));

        var provider = new AppConfigAgentConfigurationProvider(_httpClient, profile, _logger, ownsHttpClient: false);

        // Act
        provider.Load();

        // Assert
        Assert.True(provider.TryGet("FeatureManagement:feature1", out var value));
        Assert.Equal("true", value);

        // Calling Load() again should work fine for this case
        var exception = Record.Exception(() => provider.Load());
        Assert.Null(exception);
    }

    [Fact]
    public void Load_MultipleTimes_ShouldDisposePreviousReloadToken()
    {
        // Arrange
        var profile = new AppConfigAgentProfile
        {
            BaseUrl = "http://localhost:2772",
            ApplicationId = "test-app",
            EnvironmentId = "test-env",
            ProfileId = "test-profile",
            ReloadAfterSeconds = 1
        };

        var featureFlagsJson = new
        {
            feature1 = new { enabled = true }
        };

        _mockHttpHandler
            .When("http://localhost:2772/applications/test-app/environments/test-env/configurations/test-profile")
            .Respond(HttpStatusCode.OK, "application/json", JsonSerializer.Serialize(featureFlagsJson));

        var provider = new AppConfigAgentConfigurationProvider(_httpClient, profile, _logger, ownsHttpClient: false);

        // Act 
        provider.Load();

        // Act & Assert
        var exception1 = Record.Exception(() => provider.Load());
        Assert.Null(exception1);

        // Act & Assert part 2
        var exception2 = Record.Exception(() => provider.Load());
        Assert.Null(exception2);

        // Assert
        // Configuration should still be accessible after multiple loads
        Assert.True(provider.TryGet("FeatureManagement:feature1", out var value));
        Assert.Equal("true", value);

        provider.Dispose();
    }

    [Fact]
    public void Dispose_WithReloadMechanism_ShouldDisposeReloadToken()
    {
        // Arrange
        var profile = new AppConfigAgentProfile
        {
            BaseUrl = "http://localhost:2772",
            ApplicationId = "test-app",
            EnvironmentId = "test-env",
            ProfileId = "test-profile",
            ReloadAfterSeconds = 90
        };

        var featureFlagsJson = new
        {
            feature1 = new { enabled = true }
        };

        _mockHttpHandler
            .When("http://localhost:2772/applications/test-app/environments/test-env/configurations/test-profile")
            .Respond(HttpStatusCode.OK, "application/json", JsonSerializer.Serialize(featureFlagsJson));

        var provider = new AppConfigAgentConfigurationProvider(_httpClient, profile, _logger, ownsHttpClient: false);
        provider.Load();

        // Act & Assert
        var exception = Record.Exception(() => provider.Dispose());
        Assert.Null(exception);

        var disposeException = Record.Exception(() => provider.Dispose());
        Assert.Null(disposeException);
    }

    [Fact]
    public async Task Dispose_WithOwnsHttpClientTrue_ShouldDisposeHttpClient()
    {
        // Arrange
        var profile = new AppConfigAgentProfile
        {
            BaseUrl = "http://localhost:2772",
            ApplicationId = "test-app",
            EnvironmentId = "test-env",
            ProfileId = "test-profile"
        };

        var featureFlagsJson = new
        {
            feature1 = new { enabled = true }
        };

        var testHttpClient = new HttpClient(_mockHttpHandler)
        {
            BaseAddress = new Uri("http://localhost:2772")
        };

        _mockHttpHandler
            .When("http://localhost:2772/applications/test-app/environments/test-env/configurations/test-profile")
            .Respond(HttpStatusCode.OK, "application/json", JsonSerializer.Serialize(featureFlagsJson));

        var provider = new AppConfigAgentConfigurationProvider(testHttpClient, profile, _logger, ownsHttpClient: true);
        provider.Load();

        // Act
        provider.Dispose();

        // Assert
        var exception = await Record.ExceptionAsync(async () => await testHttpClient.GetAsync("http://localhost:2772/test"));
        Assert.NotNull(exception);
        Assert.IsType<ObjectDisposedException>(exception);
    }

    [Fact]
    public async Task Dispose_WithOwnsHttpClientFalse_ShouldNotDisposeHttpClient()
    {
        // Arrange
        var profile = new AppConfigAgentProfile
        {
            BaseUrl = "http://localhost:2772",
            ApplicationId = "test-app",
            EnvironmentId = "test-env",
            ProfileId = "test-profile"
        };

        var featureFlagsJson = new
        {
            feature1 = new { enabled = true }
        };

        var testHttpClient = new HttpClient(_mockHttpHandler)
        {
            BaseAddress = new Uri("http://localhost:2772")
        };

        _mockHttpHandler
            .When("http://localhost:2772/applications/test-app/environments/test-env/configurations/test-profile")
            .Respond(HttpStatusCode.OK, "application/json", JsonSerializer.Serialize(featureFlagsJson));

        _mockHttpHandler
            .When("http://localhost:2772/test")
            .Respond(HttpStatusCode.OK, "application/json", "{}");

        var provider = new AppConfigAgentConfigurationProvider(testHttpClient, profile, _logger, ownsHttpClient: false);
        provider.Load();

        // Act
        provider.Dispose();

        // Assert
        var response = await testHttpClient.GetAsync("http://localhost:2772/test");
        Assert.NotNull(response);
        Assert.True(response.IsSuccessStatusCode);

        // Clean up
        testHttpClient.Dispose();
    }

    [Fact]
    public async Task AppConfigAgentConfigurationSource_Build_WithHttpClientProvided_ShouldSetOwnsHttpClientFalse()
    {
        // Arrange
        var source = new AppConfigAgentConfigurationSource
        {
            HttpClient = new HttpClient(_mockHttpHandler),
            Profile = new AppConfigAgentProfile
            {
                BaseUrl = "http://localhost:2772",
                ApplicationId = "test-app",
                EnvironmentId = "test-env",
                ProfileId = "test-profile"
            }
        };

        _mockHttpHandler
            .When("http://localhost:2772/test")
            .Respond(HttpStatusCode.OK, "application/json", "{}");

        var builder = new ConfigurationBuilder();

        // Act
        var provider = source.Build(builder) as AppConfigAgentConfigurationProvider;

        // Assert
        Assert.NotNull(provider);

        provider.Dispose();

        // HttpClient should still be usable (not disposed)
        var testResponse = await source.HttpClient!.GetAsync("http://localhost:2772/test");
        Assert.NotNull(testResponse);

        // Clean up in case of test pollution/leakage
        source.HttpClient.Dispose();
    }

    [Fact]
    public void AppConfigAgentConfigurationSource_Build_WithoutHttpClient_ShouldCreateAndOwnHttpClient()
    {
        // Arrange
        var source = new AppConfigAgentConfigurationSource
        {
            HttpClient = null,
            Profile = new AppConfigAgentProfile
            {
                BaseUrl = "http://localhost:2772",
                ApplicationId = "test-app",
                EnvironmentId = "test-env",
                ProfileId = "test-profile"
            }
        };

        var builder = new ConfigurationBuilder();

        // Act
        var provider = source.Build(builder) as AppConfigAgentConfigurationProvider;

        // Assert
        Assert.NotNull(provider);

        // Provider should own the HttpClient since one was created
        var exception = Record.Exception(() => provider.Dispose());
        Assert.Null(exception);
    }

    [Fact]
    public void Load_InitialLoad_ShouldRetryOnConnectionRefused_ThenSucceed()
    {
        // Arrange — simulate AppConfig Agent sidecar not ready for the first 3 attempts
        var profile = new AppConfigAgentProfile
        {
            BaseUrl = "http://localhost:2772",
            ApplicationId = "test-app",
            EnvironmentId = "test-env",
            ProfileId = "test-profile",
            IsFeatureFlag = false,
            ReloadAfterSeconds = null // Disable reload timer for test isolation
        };

        var configJson = """{"Cbms": {"UseMockResponses": true}}""";
        var handler = new FailThenSucceedHandler(
            failCount: 3,
            successResponse: new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(configJson, Encoding.UTF8, "application/json")
            });

        using var httpClient = new HttpClient(handler);
        var provider = new AppConfigAgentConfigurationProvider(httpClient, profile, _logger, ownsHttpClient: false);

        // Act
        provider.Load();

        // Assert — config should be loaded after retries
        Assert.True(provider.TryGet("Cbms:UseMockResponses", out var value));
        Assert.Equal("true", value);
        Assert.Equal(4, handler.CallCount); // 3 failures + 1 success

        provider.Dispose();
    }

    [Fact]
    public void Load_InitialLoad_ShouldGiveUpAfterMaxRetries()
    {
        // Arrange — agent never becomes available
        var profile = new AppConfigAgentProfile
        {
            BaseUrl = "http://localhost:2772",
            ApplicationId = "test-app",
            EnvironmentId = "test-env",
            ProfileId = "test-profile",
            IsFeatureFlag = false,
            ReloadAfterSeconds = null
        };

        var handler = new FailThenSucceedHandler(
            failCount: 100, // More than max retries
            successResponse: new HttpResponseMessage(HttpStatusCode.OK));

        using var httpClient = new HttpClient(handler);
        var provider = new AppConfigAgentConfigurationProvider(httpClient, profile, _logger, ownsHttpClient: false);

        // Act — should not throw
        var exception = Record.Exception(() => provider.Load());

        // Assert
        Assert.Null(exception);
        Assert.False(provider.TryGet("any-key", out _)); // No config loaded
        Assert.Equal(10, handler.CallCount); // Should have tried exactly 10 times (max retries)

        provider.Dispose();
    }

    [Fact]
    public void Load_SubsequentReloads_ShouldNotRetryOnFailure()
    {
        // Arrange — first load succeeds, second load (simulating a reload) should not retry
        var profile = new AppConfigAgentProfile
        {
            BaseUrl = "http://localhost:2772",
            ApplicationId = "test-app",
            EnvironmentId = "test-env",
            ProfileId = "test-profile",
            IsFeatureFlag = false,
            ReloadAfterSeconds = null
        };

        var configJson = """{"Key1": "value1"}""";
        // First call succeeds, then all subsequent calls fail
        var handler = new FailThenSucceedHandler(
            failCount: 0,
            successResponse: new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(configJson, Encoding.UTF8, "application/json")
            },
            failAfterSuccess: true);

        using var httpClient = new HttpClient(handler);
        var provider = new AppConfigAgentConfigurationProvider(httpClient, profile, _logger, ownsHttpClient: false);

        // Act — initial load succeeds
        provider.Load();
        Assert.True(provider.TryGet("Key1", out var value));
        Assert.Equal("value1", value);
        Assert.Equal(1, handler.CallCount);

        // Act — subsequent load (reload) fails, should only try once (no retry)
        provider.Load();

        // Assert — only 2 total calls (1 initial + 1 reload), no retries on reload
        Assert.Equal(2, handler.CallCount);

        provider.Dispose();
    }

    /// <summary>
    /// Test handler that throws HttpRequestException for the first N requests,
    /// then returns a success response. Simulates the AppConfig Agent sidecar
    /// startup race condition.
    /// </summary>
    private sealed class FailThenSucceedHandler : HttpMessageHandler
    {
        private readonly int _failCount;
        private readonly HttpResponseMessage _successResponse;
        private readonly bool _failAfterSuccess;
        private int _callCount;

        public int CallCount => _callCount;

        public FailThenSucceedHandler(
            int failCount,
            HttpResponseMessage successResponse,
            bool failAfterSuccess = false)
        {
            _failCount = failCount;
            _successResponse = successResponse;
            _failAfterSuccess = failAfterSuccess;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var currentCall = Interlocked.Increment(ref _callCount);

            if (currentCall <= _failCount)
            {
                throw new HttpRequestException(
                    "Connection refused (localhost:2772)",
                    new System.Net.Sockets.SocketException(111));
            }

            if (_failAfterSuccess && currentCall > _failCount + 1)
            {
                throw new HttpRequestException(
                    "Connection refused (localhost:2772)",
                    new System.Net.Sockets.SocketException(111));
            }

            return Task.FromResult(_successResponse);
        }
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
        _mockHttpHandler?.Dispose();
    }
}
