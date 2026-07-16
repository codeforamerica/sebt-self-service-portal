using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SEBT.Portal.Api.Services;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Kernel;

namespace SEBT.Portal.Tests.Integration;

/// <summary>
/// Integration tests for POST /api/auth/otp/request and POST /api/auth/otp/validate.
/// Exercises the real HTTP pipeline (routing, rate limiting, controller, handlers,
/// cookie issuance) with mocked user persistence and email delivery.
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public class OtpEndpointIntegrationTests : IClassFixture<PortalWebApplicationFactory>
{
    private const string FixedOtpCode = "654321";

    private readonly PortalWebApplicationFactory _factory;

    public OtpEndpointIntegrationTests(PortalWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task OtpRequestAndValidate_SetsSessionCookie_AndStatusReturnsOk()
    {
        var email = $"otp-integration-{Guid.NewGuid():N}@example.com";
        var user = new User
        {
            Email = email,
            IalLevel = UserIalLevel.None,
            IdProofingStatus = IdProofingStatus.NotStarted
        };

        var userRepository = Substitute.For<IUserRepository>();
        userRepository
            .GetOrCreateUserAsync(email, Arg.Any<CancellationToken>())
            .Returns((user, true));
        userRepository
            .UpdateUserAsync(Arg.Any<User>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var otpGenerator = Substitute.For<IOtpGeneratorService>();
        otpGenerator.GenerateOtp().Returns(FixedOtpCode);

        var otpSender = Substitute.For<IOtpSenderService>();
        otpSender
            .SendOtpAsync(email, FixedOtpCode, Arg.Any<string>())
            .Returns(Result.Success());

        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                ReplaceWithMock(services, userRepository);
                ReplaceWithMock(services, otpGenerator);
                ReplaceWithMock(services, otpSender);
            });
        });

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
            AllowAutoRedirect = false
        });

        using var requestResponse = await client.PostAsJsonAsync(
            "/api/auth/otp/request",
            new { email, locale = "en" });
        Assert.Equal(HttpStatusCode.Created, requestResponse.StatusCode);

        using var validateResponse = await client.PostAsJsonAsync(
            "/api/auth/otp/validate",
            new { email, otp = FixedOtpCode });
        Assert.Equal(HttpStatusCode.NoContent, validateResponse.StatusCode);
        var sessionToken = ExtractSessionCookieValue(validateResponse);
        Assert.False(string.IsNullOrWhiteSpace(sessionToken));

        using var statusRequest = new HttpRequestMessage(HttpMethod.Get, "/api/auth/status");
        statusRequest.Headers.Add("Cookie", $"{AuthCookies.AuthCookieName}={sessionToken}");
        using var statusResponse = await client.SendAsync(statusRequest);
        Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);
    }

    [Fact]
    public async Task OtpValidate_WithWrongCode_ReturnsBadRequest()
    {
        var email = $"otp-integration-{Guid.NewGuid():N}@example.com";

        var otpGenerator = Substitute.For<IOtpGeneratorService>();
        otpGenerator.GenerateOtp().Returns(FixedOtpCode);

        var otpSender = Substitute.For<IOtpSenderService>();
        otpSender
            .SendOtpAsync(email, FixedOtpCode, Arg.Any<string>())
            .Returns(Result.Success());

        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                ReplaceWithMock(services, otpGenerator);
                ReplaceWithMock(services, otpSender);
            });
        });

        var client = factory.CreateClient();

        using var requestResponse = await client.PostAsJsonAsync(
            "/api/auth/otp/request",
            new { email, locale = "en" });
        Assert.Equal(HttpStatusCode.Created, requestResponse.StatusCode);

        using var validateResponse = await client.PostAsJsonAsync(
            "/api/auth/otp/validate",
            new { email, otp = "000000" });
        Assert.Equal(HttpStatusCode.BadRequest, validateResponse.StatusCode);
    }

    private static void ReplaceWithMock<TService>(IServiceCollection services, TService implementation)
        where TService : class
    {
        var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(TService));
        if (descriptor != null)
        {
            services.Remove(descriptor);
        }

        services.AddSingleton(implementation);
    }

    private static string ExtractSessionCookieValue(HttpResponseMessage response)
    {
        var setCookie = response.Headers.GetValues("Set-Cookie").First();
        var prefix = $"{AuthCookies.AuthCookieName}=";
        var start = setCookie.IndexOf(prefix, StringComparison.Ordinal);
        Assert.True(start >= 0);
        var valueStart = start + prefix.Length;
        var end = setCookie.IndexOf(';', valueStart);
        return end >= 0 ? setCookie[valueStart..end] : setCookie[valueStart..];
    }
}
