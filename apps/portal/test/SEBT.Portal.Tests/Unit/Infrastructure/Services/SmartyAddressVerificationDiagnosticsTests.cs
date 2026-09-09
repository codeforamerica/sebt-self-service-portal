using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.AddressUpdate;
using SEBT.Portal.Infrastructure.Services;
using SEBT.Portal.Kernel.Results;

namespace SEBT.Portal.Tests.Unit.Infrastructure.Services;

public class SmartyAddressVerificationDiagnosticsTests
{
    private static SmartyAddressVerificationDiagnostics CreateDiagnostics()
    {
        var smartySnapshot = Substitute.For<IOptionsSnapshot<SmartySettings>>();
        smartySnapshot.Value.Returns(new SmartySettings
        {
            Enabled = true,
            AuthId = "test-auth-id",
            AuthToken = "test-token",
            BaseUrl = "https://us-street.api.smarty.com"
        });

        var policySnapshot = Substitute.For<IOptionsSnapshot<AddressValidationPolicySettings>>();
        policySnapshot.Value.Returns(new AddressValidationPolicySettings
        {
            AllowGeneralDelivery = true
        });

        return new SmartyAddressVerificationDiagnostics(
            smartySnapshot,
            policySnapshot,
            NullLogger<SmartyAddressVerificationService>.Instance);
    }

    [Fact]
    public async Task ValidateAgainstCannedSuccessAsync_ReturnsSuccess_WhenBodyContainsVerifiableCandidate()
    {
        var json =
            """
            [{
              "input_index": 0,
              "candidate_index": 0,
              "delivery_line_1": "123 Main St",
              "delivery_line_2": null,
              "components": {
                "primary_number": "123",
                "street_name": "Main",
                "street_suffix": "St",
                "city_name": "Denver",
                "state_abbreviation": "CO",
                "zipcode": "80203",
                "plus4_code": "1234"
              },
              "metadata": { "record_type": "S" },
              "analysis": { "dpv_match_code": "Y" }
            }]
            """;

        var diagnostics = CreateDiagnostics();

        var result = await diagnostics.ValidateAgainstCannedSuccessAsync(json);

        var success = Assert.IsType<SuccessResult<AddressUpdateSuccess>>(result);
        Assert.Equal("123 Main St", success.Value.NormalizedAddress.StreetAddress1);
        Assert.Equal("CO", success.Value.NormalizedAddress.State);
    }

    [Fact]
    public async Task ValidateAgainstCannedServerErrorAsync_ReturnsDependencyFailed()
    {
        var diagnostics = CreateDiagnostics();

        var result = await diagnostics.ValidateAgainstCannedServerErrorAsync();

        var failure = Assert.IsType<DependencyFailedResult<AddressUpdateSuccess>>(result);
        Assert.Equal(DependencyFailedReason.ConnectionFailed, failure.Reason);
    }

    [Fact]
    public async Task ValidateAgainstTransportFailureAsync_ReturnsDependencyFailed()
    {
        var diagnostics = CreateDiagnostics();

        var result = await diagnostics.ValidateAgainstTransportFailureAsync();

        var failure = Assert.IsType<DependencyFailedResult<AddressUpdateSuccess>>(result);
        Assert.Equal(DependencyFailedReason.ConnectionFailed, failure.Reason);
    }
}
