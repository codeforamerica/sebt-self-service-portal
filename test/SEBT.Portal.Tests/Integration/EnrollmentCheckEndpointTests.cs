using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SEBT.Portal.StatesPlugins.Interfaces;
using SEBT.Portal.StatesPlugins.Interfaces.Models.EnrollmentCheck;

namespace SEBT.Portal.Tests.Integration;

/// <summary>
/// Integration tests for POST /api/enrollment/check.
/// These tests exercise the full HTTP pipeline — routing, rate limiting,
/// controller, use case handler, and response serialization — with a
/// mock IEnrollmentCheckService standing in for the real state plugin.
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public class EnrollmentCheckEndpointTests : IClassFixture<PortalWebApplicationFactory>
{
    private readonly PortalWebApplicationFactory _factory;

    public EnrollmentCheckEndpointTests(PortalWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PostCheck_WithValidChild_Returns200WithResults()
    {
        // Arrange: register a mock plugin that returns a Match
        var mockPlugin = Substitute.For<IEnrollmentCheckService>();
        mockPlugin.CheckEnrollmentAsync(
                Arg.Any<EnrollmentCheckRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var request = callInfo.Arg<EnrollmentCheckRequest>();
                return new EnrollmentCheckResult
                {
                    Results = request.Children.Select(c => new ChildCheckResult
                    {
                        CheckId = c.CheckId,
                        FirstName = c.FirstName,
                        LastName = c.LastName,
                        DateOfBirth = c.DateOfBirth,
                        Status = EnrollmentStatus.Match,
                        EligibilityType = EligibilityType.Snap,
                        SchoolName = c.SchoolName
                    }).ToList()
                };
            });

        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<IEnrollmentCheckService>(mockPlugin);
            });
        }).CreateClient();

        var requestBody = new
        {
            children = new[]
            {
                new
                {
                    firstName = "Jane",
                    lastName = "Doe",
                    dateOfBirth = "2015-03-12",
                    schoolName = "Lincoln Elementary"
                }
            }
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/enrollment/check", requestBody);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var results = json.GetProperty("results");
        Assert.Equal(1, results.GetArrayLength());

        var first = results[0];
        Assert.Equal("Jane", first.GetProperty("firstName").GetString());
        Assert.Equal("Doe", first.GetProperty("lastName").GetString());
        Assert.Equal("2015-03-12", first.GetProperty("dateOfBirth").GetString());
        Assert.Equal("Match", first.GetProperty("status").GetString());
        Assert.Equal("Snap", first.GetProperty("eligibilityType").GetString());
        Assert.Equal("Lincoln Elementary", first.GetProperty("schoolName").GetString());

        // checkId should be a valid GUID (server-generated)
        var checkId = first.GetProperty("checkId").GetString();
        Assert.True(Guid.TryParse(checkId, out _));
    }

    [Fact]
    public async Task PostCheck_WithInvalidDateFormat_Returns400()
    {
        var client = _factory.CreateClient();

        var requestBody = new
        {
            children = new[]
            {
                new
                {
                    firstName = "Jane",
                    lastName = "Doe",
                    dateOfBirth = "not-a-date",
                    schoolName = "Lincoln Elementary"
                }
            }
        };

        var response = await client.PostAsJsonAsync("/api/enrollment/check", requestBody);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostCheck_WithEmptyChildren_Returns400()
    {
        var client = _factory.CreateClient();

        var requestBody = new
        {
            children = Array.Empty<object>()
        };

        var response = await client.PostAsJsonAsync("/api/enrollment/check", requestBody);

        // Empty children triggers validation failure in the handler → 400
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostCheck_WhenPluginThrows_Returns503()
    {
        var mockPlugin = Substitute.For<IEnrollmentCheckService>();
        mockPlugin.CheckEnrollmentAsync(
                Arg.Any<EnrollmentCheckRequest>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("Plugin service unavailable"));

        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<IEnrollmentCheckService>(mockPlugin);
            });
        }).CreateClient();

        var requestBody = new
        {
            children = new[]
            {
                new
                {
                    firstName = "Jane",
                    lastName = "Doe",
                    dateOfBirth = "2015-03-12",
                    schoolName = "Lincoln Elementary"
                }
            }
        };

        var response = await client.PostAsJsonAsync("/api/enrollment/check", requestBody);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task PostCheck_IsAccessibleWithoutAuthentication()
    {
        // This test verifies the endpoint is truly public (AllowAnonymous).
        // The client has no auth token — this should still succeed, not 401.
        var mockPlugin = Substitute.For<IEnrollmentCheckService>();
        mockPlugin.CheckEnrollmentAsync(
                Arg.Any<EnrollmentCheckRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(new EnrollmentCheckResult { Results = new List<ChildCheckResult>() });

        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<IEnrollmentCheckService>(mockPlugin);
            });
        }).CreateClient();

        var requestBody = new
        {
            children = new[]
            {
                new
                {
                    firstName = "Jane",
                    lastName = "Doe",
                    dateOfBirth = "2015-03-12",
                    schoolName = "Lincoln Elementary"
                }
            }
        };

        var response = await client.PostAsJsonAsync("/api/enrollment/check", requestBody);

        // Should not be 401 Unauthorized or 403 Forbidden
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
