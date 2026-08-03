using SEBT.Portal.Core.StateBackends.Configuration;
using SEBT.Portal.Core.StateBackends.Configuration.Auth;
using SEBT.Portal.Core.StateBackends.Configuration.Operations;

namespace SEBT.Portal.Tests.Unit.Infrastructure.StateBackends;

/// <summary>
/// Shared invariant boilerplate for driver-test configurations: the test envelope (base URL +
/// API-key auth) plus <c>With*</c> helpers that swap one operation. Per-test VARIATION — the
/// operation configs themselves — stays visible at the call site.
/// </summary>
internal static class StateBackendTestConfig
{
    /// <summary>The test envelope: base URL + API-key auth, no operations.</summary>
    public static StateBackendConfiguration Base() =>
        new()
        {
            BaseUrl = new Uri("http://backend.test"),
            Auth = new StateBackendApiKeyAuthScheme { Header = "X-Api-Key", KeyRef = "test-api-key" },
            Operations = new StateBackendOperations(),
        };

    /// <summary>DC-shaped lookup base mirroring the sample: multi-result-set root, original column names.</summary>
    public static StateBackendConfiguration DcLookup() =>
        Base().WithLookup(new HouseholdLookupOperationConfig
        {
            Method = StateBackendHttpMethod.Post,
            Path = "/households/lookup",
            Response = new StateBackendResponseMapping
            {
                Root = "$.resultSets[0]",
                Fields = new Dictionary<string, FieldMapping>
                {
                    ["summerEBTCaseID"] = new() { From = "SummerEBTCaseID" },
                    ["childFirstName"] = new() { From = "ChildFirstName" },
                    ["childLastName"] = new() { From = "ChildLastName" },
                },
            },
        });

    public static StateBackendConfiguration WithLookup(
        this StateBackendConfiguration config, HouseholdLookupOperationConfig lookup) =>
        config with { Operations = config.Operations with { HouseholdLookup = lookup } };

    public static StateBackendConfiguration WithLookupResponse(
        this StateBackendConfiguration config, StateBackendResponseMapping response) =>
        config.WithLookup(config.Operations.HouseholdLookup! with { Response = response });

    public static StateBackendConfiguration WithLookupRequest(
        this StateBackendConfiguration config, RequestBinding binding) =>
        config.WithLookup(config.Operations.HouseholdLookup! with { Request = binding });

    public static StateBackendConfiguration WithCardReplacement(
        this StateBackendConfiguration config, CardReplacementOperationConfig cardReplacement) =>
        config with { Operations = config.Operations with { CardReplacement = cardReplacement } };

    public static StateBackendConfiguration WithAddressUpdate(
        this StateBackendConfiguration config, AddressUpdateOperationConfig addressUpdate) =>
        config with { Operations = config.Operations with { AddressUpdate = addressUpdate } };

    public static StateBackendConfiguration WithEnrollment(
        this StateBackendConfiguration config, EnrollmentCheckOperationConfig enrollment) =>
        config with { Operations = config.Operations with { EnrollmentCheck = enrollment } };

    public static StateBackendConfiguration WithEnrollmentRequest(
        this StateBackendConfiguration config, EnrollmentRequestBinding request) =>
        config.WithEnrollment(config.Operations.EnrollmentCheck! with { Request = request });

    public static StateBackendConfiguration WithEnrollmentMatch(
        this StateBackendConfiguration config, EnrollmentMatch match)
    {
        EnrollmentCheckOperationConfig operation = config.Operations.EnrollmentCheck!;
        return config.WithEnrollment(operation with
        {
            Response = operation.Response! with { Match = match },
        });
    }

    public static StateBackendConfiguration WithHealth(
        this StateBackendConfiguration config, HealthOperationConfig health) =>
        config with { Operations = config.Operations with { Health = health } };
}
