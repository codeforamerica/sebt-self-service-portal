using NSubstitute;
using SEBT.Portal.Core.Models;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.StateBackends;
using SEBT.Portal.Infrastructure.StateBackends;

namespace SEBT.Portal.Tests.Unit.Infrastructure.StateBackends;

/// <summary>
/// Unit tests for <see cref="StateBackendHouseholdRepository"/>: the household read path over
/// the config-driven state backend's lookup port. Signal construction mirrors the inputs the
/// plugin path sends its contract service; PII masking pins the same literals as
/// <c>HouseholdRepositoryTests</c> because both paths share <c>HouseholdPiiFilter</c>.
/// </summary>
public class StateBackendHouseholdRepositoryTests
{
    private static readonly PiiVisibility FullPii =
        new(IncludeAddress: true, IncludeEmail: true, IncludePhone: true);

    private readonly IHouseholdLookupBackend _lookupBackend;
    private readonly StateBackendHouseholdRepository _repository;

    private HouseholdLookupRequest? _capturedRequest;

    public StateBackendHouseholdRepositoryTests()
    {
        _lookupBackend = Substitute.For<IHouseholdLookupBackend>();
        _repository = new StateBackendHouseholdRepository(_lookupBackend);
    }

    private void BackendReturns(HouseholdLookupResult result)
    {
        _lookupBackend
            .LookupHouseholdAsync(Arg.Any<HouseholdLookupRequest>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                _capturedRequest = callInfo.Arg<HouseholdLookupRequest>();
                return result;
            });
    }

    private void BackendReturnsFound(HouseholdData household) =>
        BackendReturns(new HouseholdLookupResult(HouseholdLookupStatus.Found, household));

    private void BackendReturnsNotFound() =>
        BackendReturns(new HouseholdLookupResult(HouseholdLookupStatus.NotFound, Household: null));

    // Same PII fixture values HouseholdRepositoryTests pins for the plugin path, so the
    // masked literals asserted below prove parity between the two read paths.
    private static HouseholdData PiiFixtureHousehold() => new()
    {
        Email = "u@e.com",
        Phone = "303-555-0100",
        AddressOnFile = new Address
        {
            StreetAddress1 = "123 Main St",
            StreetAddress2 = "Apt 4B",
            City = "Denver",
            State = "CO",
            PostalCode = "80202",
        },
        SummerEbtCases = [new SummerEbtCase { SummerEBTCaseID = "token-1" }],
    };

    // ---------------------------------------------------------------------------
    // GetHouseholdByIdentifierAsync — signal construction
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetHouseholdByIdentifierAsync_BuildsEmailSignal_WithNormalizedValue()
    {
        BackendReturnsNotFound();

        await _repository.GetHouseholdByIdentifierAsync(
            HouseholdIdentifier.Email("  USER@EXAMPLE.COM  "), FullPii, UserIalLevel.IAL1plus);

        Assert.NotNull(_capturedRequest);
        var signal = Assert.Single(_capturedRequest.Signals);
        Assert.Equal("email", signal.Type);
        Assert.Equal("user@example.com", signal.Value);
    }

    [Theory]
    [InlineData(PreferredHouseholdIdType.Phone, "phone")]
    [InlineData(PreferredHouseholdIdType.SnapId, "snapId")]
    [InlineData(PreferredHouseholdIdType.TanfId, "tanfId")]
    [InlineData(PreferredHouseholdIdType.Ssn, "ssn")]
    public async Task GetHouseholdByIdentifierAsync_MapsIdentifierTypeToSignalType_AndTrims(
        PreferredHouseholdIdType type, string expectedSignalType)
    {
        BackendReturnsNotFound();

        await _repository.GetHouseholdByIdentifierAsync(
            new HouseholdIdentifier(type, "  ID-42  "), FullPii, UserIalLevel.IAL1plus);

        Assert.NotNull(_capturedRequest);
        var signal = Assert.Single(_capturedRequest.Signals);
        Assert.Equal(expectedSignalType, signal.Type);
        Assert.Equal("ID-42", signal.Value);
    }

    // SECURITY: IsProofed feeds the backend's proofing gate (DC's email-lookup branch), so it
    // must mirror the plugin path's derivation exactly: proofed means IAL1+ or better.
    [Theory]
    [InlineData(UserIalLevel.None, false)]
    [InlineData(UserIalLevel.IAL1, false)]
    [InlineData(UserIalLevel.IAL1plus, true)]
    [InlineData(UserIalLevel.IAL2, true)]
    public async Task GetHouseholdByIdentifierAsync_DerivesIsProofedFromIal(
        UserIalLevel ial, bool expectedIsProofed)
    {
        BackendReturnsNotFound();

        await _repository.GetHouseholdByIdentifierAsync(
            HouseholdIdentifier.Email("u@e.com"), FullPii, ial);

        Assert.NotNull(_capturedRequest);
        Assert.Equal(expectedIsProofed, _capturedRequest.IsProofed);
    }

    [Fact]
    public async Task GetHouseholdByIdentifierAsync_SetsPortalUuidFromPortalUserId()
    {
        BackendReturnsNotFound();
        var portalUserId = Guid.Parse("a1111111-1111-4111-8111-111111111111");

        await _repository.GetHouseholdByIdentifierAsync(
            HouseholdIdentifier.Email("u@e.com"), FullPii, UserIalLevel.IAL1plus, portalUserId);

        Assert.NotNull(_capturedRequest);
        Assert.Equal("a1111111-1111-4111-8111-111111111111", _capturedRequest.PortalUuid);
    }

    [Fact]
    public async Task GetHouseholdByIdentifierAsync_WhenPortalUserIdNull_LeavesPortalUuidNull()
    {
        BackendReturnsNotFound();

        await _repository.GetHouseholdByIdentifierAsync(
            HouseholdIdentifier.Email("u@e.com"), FullPii, UserIalLevel.IAL1plus);

        Assert.NotNull(_capturedRequest);
        Assert.Null(_capturedRequest.PortalUuid);
    }

    // ---------------------------------------------------------------------------
    // GetHouseholdByIdentifierAsync — results and PII filtering
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetHouseholdByIdentifierAsync_WhenFound_ReturnsHouseholdUnmaskedWithFullPii()
    {
        BackendReturnsFound(PiiFixtureHousehold());

        var result = await _repository.GetHouseholdByIdentifierAsync(
            HouseholdIdentifier.Email("u@e.com"), FullPii, UserIalLevel.IAL1plus);

        Assert.NotNull(result);
        Assert.Equal("u@e.com", result.Email);
        Assert.Equal("303-555-0100", result.Phone);
        Assert.Equal("123 Main St", result.AddressOnFile!.StreetAddress1);
        Assert.Equal("Apt 4B", result.AddressOnFile.StreetAddress2);
    }

    [Fact]
    public async Task GetHouseholdByIdentifierAsync_WhenPiiExcludesEmail_ReturnsMaskedEmail()
    {
        BackendReturnsFound(PiiFixtureHousehold());
        var noEmailPii = new PiiVisibility(IncludeAddress: true, IncludeEmail: false, IncludePhone: true);

        var result = await _repository.GetHouseholdByIdentifierAsync(
            HouseholdIdentifier.Email("u@e.com"), noEmailPii, UserIalLevel.IAL1plus);

        Assert.NotNull(result);
        // Same masked literal the plugin path pins — both paths share HouseholdPiiFilter.
        Assert.Equal("u***@e.com", result.Email);
        Assert.Equal("303-555-0100", result.Phone);
    }

    [Fact]
    public async Task GetHouseholdByIdentifierAsync_WhenPiiExcludesPhone_ReturnsMaskedPhone()
    {
        BackendReturnsFound(PiiFixtureHousehold());
        var noPhonePii = new PiiVisibility(IncludeAddress: true, IncludeEmail: true, IncludePhone: false);

        var result = await _repository.GetHouseholdByIdentifierAsync(
            HouseholdIdentifier.Email("u@e.com"), noPhonePii, UserIalLevel.IAL1plus);

        Assert.NotNull(result);
        Assert.Equal("u@e.com", result.Email);
        Assert.Equal("***-***-0100", result.Phone);
    }

    [Fact]
    public async Task GetHouseholdByIdentifierAsync_WhenPiiExcludesAddress_ReturnsMaskedAddress()
    {
        BackendReturnsFound(PiiFixtureHousehold());
        var noAddressPii = new PiiVisibility(IncludeAddress: false, IncludeEmail: true, IncludePhone: true);

        var result = await _repository.GetHouseholdByIdentifierAsync(
            HouseholdIdentifier.Email("u@e.com"), noAddressPii, UserIalLevel.IAL1plus);

        Assert.NotNull(result);
        Assert.NotNull(result.AddressOnFile);
        Assert.Equal("****", result.AddressOnFile.StreetAddress1);
        Assert.Null(result.AddressOnFile.StreetAddress2);
        Assert.Equal("Denver", result.AddressOnFile.City);
        Assert.Equal("CO", result.AddressOnFile.State);
        Assert.Equal("80202", result.AddressOnFile.PostalCode);
    }

    [Fact]
    public async Task GetHouseholdByIdentifierAsync_WhenNotFound_ReturnsNull()
    {
        BackendReturnsNotFound();

        var result = await _repository.GetHouseholdByIdentifierAsync(
            HouseholdIdentifier.Email("nobody@example.com"), FullPii, UserIalLevel.IAL1plus);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetHouseholdByIdentifierAsync_WhenValueWhitespace_ReturnsNullWithoutCallingBackend()
    {
        var result = await _repository.GetHouseholdByIdentifierAsync(
            HouseholdIdentifier.Email("   "), FullPii, UserIalLevel.IAL1plus);

        Assert.Null(result);
        await _lookupBackend.DidNotReceive().LookupHouseholdAsync(
            Arg.Any<HouseholdLookupRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetHouseholdByIdentifierAsync_WhenPiiVisibilityNull_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _repository.GetHouseholdByIdentifierAsync(
                HouseholdIdentifier.Email("u@e.com"), null!, UserIalLevel.None));
    }

    // ---------------------------------------------------------------------------
    // GetHouseholdByEmailAsync — routes through the email signal
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetHouseholdByEmailAsync_BuildsEmailSignal_WithNormalizedValue()
    {
        BackendReturnsNotFound();

        await _repository.GetHouseholdByEmailAsync(
            "  USER@EXAMPLE.COM  ", FullPii, UserIalLevel.IAL1plus);

        Assert.NotNull(_capturedRequest);
        var signal = Assert.Single(_capturedRequest.Signals);
        Assert.Equal("email", signal.Type);
        Assert.Equal("user@example.com", signal.Value);
    }

    [Fact]
    public async Task GetHouseholdByEmailAsync_WhenEmailNull_ReturnsNullWithoutCallingBackend()
    {
        var result = await _repository.GetHouseholdByEmailAsync(
            null!, FullPii, UserIalLevel.IAL1plus);

        Assert.Null(result);
        await _lookupBackend.DidNotReceive().LookupHouseholdAsync(
            Arg.Any<HouseholdLookupRequest>(), Arg.Any<CancellationToken>());
    }

    // ---------------------------------------------------------------------------
    // TryMatchCoLoadedGuardianByBenefitIdAndDobAsync — Found reduces to a bool
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task TryMatch_BuildsIcAndDobSignals_WithPortalUuidAndUnproofedContext()
    {
        BackendReturnsFound(new HouseholdData());
        var portalUserId = Guid.Parse("b2222222-2222-4222-8222-222222222222");

        var result = await _repository.TryMatchCoLoadedGuardianByBenefitIdAndDobAsync(
            "  IC000001  ", new DateOnly(1984, 3, 5), portalUserId);

        Assert.True(result);
        Assert.NotNull(_capturedRequest);
        Assert.Equal(2, _capturedRequest.Signals.Count);
        Assert.Contains(_capturedRequest.Signals, s => s.Type == "ic" && s.Value == "IC000001");
        // ISO date, matching the format the DC connector sends and the sample configs bind.
        Assert.Contains(_capturedRequest.Signals, s => s.Type == "dob" && s.Value == "1984-03-05");
        Assert.Equal("b2222222-2222-4222-8222-222222222222", _capturedRequest.PortalUuid);
        // The plugin path sends isIdentityProofed=false here — the match runs before proofing.
        Assert.False(_capturedRequest.IsProofed);
    }

    [Fact]
    public async Task TryMatch_WhenNotFound_ReturnsFalse()
    {
        BackendReturnsNotFound();

        var result = await _repository.TryMatchCoLoadedGuardianByBenefitIdAndDobAsync(
            "IC000001", new DateOnly(1984, 3, 5), Guid.NewGuid());

        Assert.False(result);
    }

    [Fact]
    public async Task TryMatch_WhenIcWhitespace_Throws()
    {
        // The plugin path throws too (the DC connector guards the IC); mirrored here.
        await Assert.ThrowsAsync<ArgumentException>(
            () => _repository.TryMatchCoLoadedGuardianByBenefitIdAndDobAsync(
                "   ", new DateOnly(1984, 3, 5), Guid.NewGuid()));
    }

    // ---------------------------------------------------------------------------
    // GetHouseholdByBenefitIdentifierAndGuardianDobAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetByBenefitIdAndDob_BuildsSignals_IncludingSocureUuidWhenPresent()
    {
        BackendReturnsNotFound();
        var portalUserId = Guid.Parse("c3333333-3333-4333-8333-333333333333");

        await _repository.GetHouseholdByBenefitIdentifierAndGuardianDobAsync(
            "  GUARDIAN@EXAMPLE.COM  ",
            "  IC000001  ",
            new DateOnly(1984, 3, 5),
            FullPii,
            UserIalLevel.IAL1plus,
            portalUserId,
            socureReferenceId: "  socure-ref-1234  ");

        Assert.NotNull(_capturedRequest);
        Assert.Equal(4, _capturedRequest.Signals.Count);
        Assert.Contains(_capturedRequest.Signals, s => s.Type == "ic" && s.Value == "IC000001");
        Assert.Contains(_capturedRequest.Signals, s => s.Type == "dob" && s.Value == "1984-03-05");
        Assert.Contains(_capturedRequest.Signals, s => s.Type == "email" && s.Value == "guardian@example.com");
        Assert.Contains(_capturedRequest.Signals, s => s.Type == "socureUuid" && s.Value == "socure-ref-1234");
        Assert.Equal("c3333333-3333-4333-8333-333333333333", _capturedRequest.PortalUuid);
        Assert.True(_capturedRequest.IsProofed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task GetByBenefitIdAndDob_OmitsSocureUuidSignal_WhenAbsent(string? socureReferenceId)
    {
        BackendReturnsNotFound();

        await _repository.GetHouseholdByBenefitIdentifierAndGuardianDobAsync(
            "guardian@example.com",
            "IC000001",
            new DateOnly(1984, 3, 5),
            FullPii,
            UserIalLevel.IAL1plus,
            Guid.NewGuid(),
            socureReferenceId);

        Assert.NotNull(_capturedRequest);
        // Omitted entirely so a mapOptional binding drops the field instead of sending "".
        Assert.DoesNotContain(_capturedRequest.Signals, s => s.Type == "socureUuid");
        Assert.Equal(3, _capturedRequest.Signals.Count);
    }

    [Fact]
    public async Task GetByBenefitIdAndDob_StampsEnvelopeEmail_WithNormalizedLoginEmail()
    {
        // The backend's response carries no envelope email — this lookup is keyed by IC+DOB.
        BackendReturnsFound(new HouseholdData
        {
            SummerEbtCases = [new SummerEbtCase { SummerEBTCaseID = "token-1" }],
        });

        var result = await _repository.GetHouseholdByBenefitIdentifierAndGuardianDobAsync(
            "  GUARDIAN@EXAMPLE.COM  ",
            "IC000001",
            new DateOnly(1984, 3, 5),
            FullPii,
            UserIalLevel.IAL1plus,
            Guid.NewGuid());

        Assert.NotNull(result);
        Assert.Equal("guardian@example.com", result.Email);
    }

    [Fact]
    public async Task GetByBenefitIdAndDob_AppliesPiiFilter_AfterEmailStamping()
    {
        BackendReturnsFound(new HouseholdData());
        var noEmailPii = new PiiVisibility(IncludeAddress: true, IncludeEmail: false, IncludePhone: true);

        var result = await _repository.GetHouseholdByBenefitIdentifierAndGuardianDobAsync(
            "u@e.com",
            "IC000001",
            new DateOnly(1984, 3, 5),
            noEmailPii,
            UserIalLevel.IAL1plus,
            Guid.NewGuid());

        Assert.NotNull(result);
        // The stamped login email is masked like any other envelope email.
        Assert.Equal("u***@e.com", result.Email);
    }

    [Fact]
    public async Task GetByBenefitIdAndDob_WhenNotFound_ReturnsNull()
    {
        BackendReturnsNotFound();

        var result = await _repository.GetHouseholdByBenefitIdentifierAndGuardianDobAsync(
            "u@e.com", "IC000001", new DateOnly(1984, 3, 5),
            FullPii, UserIalLevel.IAL1plus, Guid.NewGuid());

        Assert.Null(result);
    }

    [Theory]
    [InlineData("   ", "IC000001")]
    [InlineData("u@e.com", "   ")]
    public async Task GetByBenefitIdAndDob_WhenEmailOrIcWhitespace_ReturnsNullWithoutCallingBackend(
        string loginEmail, string ic)
    {
        var result = await _repository.GetHouseholdByBenefitIdentifierAndGuardianDobAsync(
            loginEmail, ic, new DateOnly(1984, 3, 5),
            FullPii, UserIalLevel.IAL1plus, Guid.NewGuid());

        Assert.Null(result);
        await _lookupBackend.DidNotReceive().LookupHouseholdAsync(
            Arg.Any<HouseholdLookupRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetByBenefitIdAndDob_WhenPiiVisibilityNull_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _repository.GetHouseholdByBenefitIdentifierAndGuardianDobAsync(
                "u@e.com", "IC000001", new DateOnly(1984, 3, 5),
                null!, UserIalLevel.IAL1plus, Guid.NewGuid()));
    }

    // ---------------------------------------------------------------------------
    // Writes are not supported — reads only, like the plugin repository
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task UpsertHouseholdAsync_ThrowsNotSupportedException()
    {
        var ex = await Assert.ThrowsAsync<NotSupportedException>(
            () => _repository.UpsertHouseholdAsync(new HouseholdData()));

        Assert.Contains("read-only", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
