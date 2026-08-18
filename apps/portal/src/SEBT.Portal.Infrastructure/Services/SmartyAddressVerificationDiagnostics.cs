using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.AddressUpdate;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.Results;

namespace SEBT.Portal.Infrastructure.Services;

/// <summary>
/// Exercises <see cref="SmartyAddressVerificationService"/> against canned HTTP outcomes.
/// Constructs the real service with a fake <see cref="HttpMessageHandler"/> that replays the
/// requested response (or throws a transport failure), so the service's error handling and
/// logging run without any network activity.
/// </summary>
public sealed class SmartyAddressVerificationDiagnostics(
    IOptionsSnapshot<SmartySettings> smartySettingsSnapshot,
    IOptionsSnapshot<AddressValidationPolicySettings> policySettingsSnapshot,
    ILogger<SmartyAddressVerificationService> logger) : IAddressVerificationDiagnostics
{
    public Task<Result<AddressUpdateSuccess>> ValidateAgainstCannedSuccessAsync(
        string responseBody,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(new FixedResponseHandler(HttpStatusCode.OK, responseBody), cancellationToken);
    }

    public Task<Result<AddressUpdateSuccess>> ValidateAgainstCannedServerErrorAsync(
        CancellationToken cancellationToken = default)
    {
        return RunAsync(new FixedResponseHandler(HttpStatusCode.InternalServerError), cancellationToken);
    }

    public Task<Result<AddressUpdateSuccess>> ValidateAgainstTransportFailureAsync(
        CancellationToken cancellationToken = default)
    {
        return RunAsync(new FixedResponseHandler(transportFailure: true), cancellationToken);
    }

    private Task<Result<AddressUpdateSuccess>> RunAsync(
        FixedResponseHandler handler,
        CancellationToken cancellationToken)
    {
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://us-street.api.smarty.com/")
        };
        var factory = new SingleClientFactory(client);
        var service = new SmartyAddressVerificationService(
            factory, smartySettingsSnapshot, policySettingsSnapshot, logger);

        return service.ValidateAndNormalizeAsync(new AddressUpdateOperationRequest
        {
            StreetAddress1 = "123 Main St",
            City = "Denver",
            State = "CO",
            PostalCode = "80203"
        }, cancellationToken);
    }

    private sealed class FixedResponseHandler(
        HttpStatusCode statusCode = HttpStatusCode.OK,
        string? responseBody = null,
        bool transportFailure = false) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (transportFailure)
            {
                throw new HttpRequestException("Simulated transport failure: connection refused.");
            }

            var response = new HttpResponseMessage(statusCode);
            if (responseBody != null)
            {
                response.Content = new StringContent(responseBody, Encoding.UTF8, "application/json");
            }

            return Task.FromResult(response);
        }
    }

    private sealed class SingleClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }
}
