using System.Security.Claims;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SEBT.Portal.Core.Models;
using SEBT.Portal.Core.Models.AddressUpdate;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Infrastructure.Repositories;
using SEBT.Portal.Infrastructure.StateBackendAdapters;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.Results;
using SEBT.Portal.StatesPlugins.Interfaces;
using SEBT.Portal.UseCases.Household;
using ICoreAddressUpdateService = SEBT.Portal.Core.Services.IAddressUpdateService;
using IStateAddressUpdateService = SEBT.Portal.StatesPlugins.Interfaces.IAddressUpdateService;
using PluginAddressUpdateRequest = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.AddressUpdateRequest;
using PluginAddressUpdateResult = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.AddressUpdateResult;
using PluginHouseholdData = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.HouseholdData;
using PluginHouseholdIdentifierType = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.HouseholdIdentifierType;
using PluginIdentityAssuranceLevel = SEBT.Portal.StatesPlugins.Interfaces.Models.IdentityAssuranceLevel;
using PluginPiiVisibility = SEBT.Portal.StatesPlugins.Interfaces.Models.PiiVisibility;
using PluginSummerEbtCase = SEBT.Portal.StatesPlugins.Interfaces.Data.Cases.SummerEbtCase;

namespace SEBT.Portal.Tests.Unit.UseCases.Household;

/// <summary>
/// Pins the plugin-observable address-update contract across the composed portal
/// path: the real plugin read path serves the household (and its opaque case
/// tokens), and the handler's dispatch reaches the contract service as ONE call
/// carrying the resolved household identifier and the five persisted address
/// scalars — exactly what the state connector has always seen. Address updates
/// are household-routed: a household with zero cases still dispatches.
/// </summary>
public class AddressUpdatePluginCompositionTests
{
    private const string GuardianEmail = "user@example.com";
    private const string RawCaseId1 = "STATE-CASE-1";
    private const string RawCaseId2 = "STATE-CASE-2";

    private readonly IHouseholdIdentifierResolver _resolver =
        Substitute.For<IHouseholdIdentifierResolver>();
    private readonly ICoreAddressUpdateService _smartyAddressService =
        Substitute.For<ICoreAddressUpdateService>();
    private readonly IAddressValidationService _addressValidationService =
        Substitute.For<IAddressValidationService>();
    private readonly IPiiVisibilityService _piiVisibilityService =
        Substitute.For<IPiiVisibilityService>();
    private readonly IIdProofingService _idProofingService =
        Substitute.For<IIdProofingService>();
    private readonly ISelfServiceEvaluator _evaluator =
        Substitute.For<ISelfServiceEvaluator>();
    private readonly IStateAddressUpdateService _contractService =
        Substitute.For<IStateAddressUpdateService>();

    public AddressUpdatePluginCompositionTests()
    {
        _resolver.ResolveAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>())
            .Returns(HouseholdIdentifier.Email(GuardianEmail));

        // Smarty pass-through: the normalized address equals the entered address,
        // so the handler persists these exact five scalars.
        _smartyAddressService
            .ValidateAndNormalizeAsync(Arg.Any<AddressUpdateOperationRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result<AddressUpdateSuccess>.Success(
                new AddressUpdateSuccess
                {
                    NormalizedAddress = new Address
                    {
                        StreetAddress1 = "123 Main St NW",
                        StreetAddress2 = "Apt 4B",
                        City = "Washington",
                        State = "District of Columbia",
                        PostalCode = "20001"
                    },
                    WasCorrected = false,
                    IsGeneralDelivery = false
                }));

        _addressValidationService.ValidateAsync(Arg.Any<Address>(), Arg.Any<CancellationToken>())
            .Returns(AddressValidationResult.Valid());

        _piiVisibilityService.GetVisibility(Arg.Any<UserIalLevel>())
            .Returns(new PiiVisibility(IncludeAddress: false, IncludeEmail: false, IncludePhone: false));

        _idProofingService.Evaluate(
            Arg.Any<ProtectedResource>(), Arg.Any<ProtectedAction>(),
            Arg.Any<UserIalLevel>(), Arg.Any<IReadOnlyList<SummerEbtCase>>())
            .Returns(new IdProofingDecision(IsAllowed: true, RequiredLevel: UserIalLevel.None));

        _evaluator.EvaluateHousehold(Arg.Any<IReadOnlyList<SummerEbtCase>>())
            .Returns(new AllowedActions { CanUpdateAddress = true, CanRequestReplacementCard = true });

        _contractService
            .UpdateAddressAsync(Arg.Any<PluginAddressUpdateRequest>(), Arg.Any<CancellationToken>())
            .Returns(PluginAddressUpdateResult.Success());
    }

    private static PluginHouseholdData CreatePluginHouseholdWithCases() => new()
    {
        Email = GuardianEmail,
        SummerEbtCases = new List<PluginSummerEbtCase>
        {
            new PluginSummerEbtCase
            {
                SummerEBTCaseID = RawCaseId1,
                ApplicationId = "APP-1",
                ApplicationStudentId = "STU-1",
                ChildFirstName = "Maria",
                ChildLastName = "Garcia",
                ChildDateOfBirth = new DateOnly(2015, 5, 15),
                HouseholdType = "DirectCert",
                EligibilityType = "SNAP"
            },
            new PluginSummerEbtCase
            {
                SummerEBTCaseID = RawCaseId2,
                ApplicationId = "APP-2",
                ApplicationStudentId = "STU-2",
                ChildFirstName = "Luis",
                ChildLastName = "Garcia",
                ChildDateOfBirth = new DateOnly(2017, 3, 2),
                HouseholdType = "DirectCert",
                EligibilityType = "SNAP"
            }
        }
    };

    private static PluginHouseholdData CreateZeroCasePluginHousehold() => new()
    {
        Email = GuardianEmail,
        SummerEbtCases = new List<PluginSummerEbtCase>()
    };

    /// <summary>
    /// Real plugin read path (repository + mapper + tokenizer) so the household
    /// arrives at the handler exactly as production serves it.
    /// </summary>
    private static HouseholdRepository CreateRepository(PluginHouseholdData pluginHousehold)
    {
        var pluginService = Substitute.For<ISummerEbtCaseService>();
        pluginService
            .GetHouseholdByIdentifierAsync(
                Arg.Any<PluginHouseholdIdentifierType>(),
                Arg.Any<string>(),
                Arg.Any<PluginPiiVisibility>(),
                Arg.Any<PluginIdentityAssuranceLevel>(),
                Arg.Any<Guid?>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => pluginHousehold);
        return new HouseholdRepository(pluginService, NullLogger<HouseholdRepository>.Instance);
    }

    private static ClaimsPrincipal CreateUser()
    {
        var claims = new List<Claim> { new(ClaimTypes.Email, GuardianEmail) };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }

    /// <summary>Handler wired to the REAL adapter over the mocked contract service.</summary>
    private UpdateAddressCommandHandler CreateHandler(HouseholdRepository repository) =>
        new(new DataAnnotationsValidator<UpdateAddressCommand>(null!),
            _smartyAddressService, _addressValidationService, _resolver, repository,
            _piiVisibilityService, _idProofingService, _evaluator,
            new PluginAddressUpdateBackend(_contractService),
            NullLogger<UpdateAddressCommandHandler>.Instance);

    private static UpdateAddressCommand CreateCommand() => new()
    {
        User = CreateUser(),
        StreetAddress1 = "123 Main St NW",
        StreetAddress2 = "Apt 4B",
        City = "Washington",
        State = "District of Columbia",
        PostalCode = "20001"
    };

    [Fact]
    public async Task UpdateAddress_HouseholdWithCases_ContractReceivesOneCallWithIdentifierAndAddress()
    {
        var handler = CreateHandler(CreateRepository(CreatePluginHouseholdWithCases()));

        var result = await handler.Handle(CreateCommand());

        var success = Assert.IsType<SuccessResult<AddressValidationResult>>(result);
        Assert.True(success.Value.IsValid);
        // The contract sees exactly what it saw before the Core port existed:
        // the resolved household identifier and the five persisted address
        // scalars, in ONE call.
        await _contractService.Received(1).UpdateAddressAsync(
            Arg.Is<PluginAddressUpdateRequest>(r =>
                r.HouseholdIdentifierValue == GuardianEmail &&
                r.Address.StreetAddress1 == "123 Main St NW" &&
                r.Address.StreetAddress2 == "Apt 4B" &&
                r.Address.City == "Washington" &&
                r.Address.State == "District of Columbia" &&
                r.Address.PostalCode == "20001"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAddress_ZeroCaseHousehold_ContractReceivesTheSameCall()
    {
        // Address updates are household-routed: no cases on file must not block
        // the dispatch or change what the contract receives.
        var handler = CreateHandler(CreateRepository(CreateZeroCasePluginHousehold()));

        var result = await handler.Handle(CreateCommand());

        var success = Assert.IsType<SuccessResult<AddressValidationResult>>(result);
        Assert.True(success.Value.IsValid);
        await _contractService.Received(1).UpdateAddressAsync(
            Arg.Is<PluginAddressUpdateRequest>(r =>
                r.HouseholdIdentifierValue == GuardianEmail &&
                r.Address.StreetAddress1 == "123 Main St NW" &&
                r.Address.StreetAddress2 == "Apt 4B" &&
                r.Address.City == "Washington" &&
                r.Address.State == "District of Columbia" &&
                r.Address.PostalCode == "20001"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAddress_PolicyRejection_RoundTripsAsConflictWithMessage()
    {
        _contractService
            .UpdateAddressAsync(Arg.Any<PluginAddressUpdateRequest>(), Arg.Any<CancellationToken>())
            .Returns(PluginAddressUpdateResult.PolicyRejected("HOUSEHOLD_NOT_ELIGIBLE", "Not eligible."));
        var handler = CreateHandler(CreateRepository(CreatePluginHouseholdWithCases()));

        var result = await handler.Handle(CreateCommand());

        // The contract's message reaches the API response unchanged.
        var preconditionFailed = Assert.IsType<PreconditionFailedResult<AddressValidationResult>>(result);
        Assert.Equal(PreconditionFailedReason.Conflict, preconditionFailed.Reason);
        Assert.Equal("Not eligible.", preconditionFailed.Message);
    }

    [Fact]
    public async Task UpdateAddress_BackendError_RoundTripsAsDependencyFailedWithMessage()
    {
        _contractService
            .UpdateAddressAsync(Arg.Any<PluginAddressUpdateRequest>(), Arg.Any<CancellationToken>())
            .Returns(PluginAddressUpdateResult.BackendError("UPSTREAM_500", "Downstream broke."));
        var handler = CreateHandler(CreateRepository(CreatePluginHouseholdWithCases()));

        var result = await handler.Handle(CreateCommand());

        var dependencyFailed = Assert.IsType<DependencyFailedResult<AddressValidationResult>>(result);
        Assert.Equal(DependencyFailedReason.ConnectionFailed, dependencyFailed.Reason);
        Assert.Equal("Downstream broke.", dependencyFailed.Message);
    }

    [Fact]
    public async Task UpdateAddress_ContractThrows_RoundTripsAsTemporarilyUnavailable()
    {
        _contractService
            .UpdateAddressAsync(Arg.Any<PluginAddressUpdateRequest>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("Connection failed"));
        var handler = CreateHandler(CreateRepository(CreatePluginHouseholdWithCases()));

        var result = await handler.Handle(CreateCommand());

        var dependencyFailed = Assert.IsType<DependencyFailedResult<AddressValidationResult>>(result);
        Assert.Equal(DependencyFailedReason.ConnectionFailed, dependencyFailed.Reason);
        Assert.Equal("Address update service is temporarily unavailable.", dependencyFailed.Message);
    }
}
