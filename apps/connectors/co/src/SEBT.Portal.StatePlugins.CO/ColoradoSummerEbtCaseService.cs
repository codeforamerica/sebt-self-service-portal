using System.Composition;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SEBT.Portal.StatePlugins.CO.Cbms;
using SEBT.Portal.StatePlugins.CO.CbmsApi.Models;
using SEBT.Portal.StatesPlugins.Interfaces;
using SEBT.Portal.StatesPlugins.Interfaces.Models;
using SEBT.Portal.StatesPlugins.Interfaces.Models.Household;

namespace SEBT.Portal.StatePlugins.CO;

[Export(typeof(IStatePlugin))]
[ExportMetadata("StateCode", "CO")]
public class ColoradoSummerEbtCaseService : ColoradoCbmsServiceBase, ISummerEbtCaseService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ColoradoSummerEbtCaseService> _logger;

    [ImportingConstructor]
    public ColoradoSummerEbtCaseService(
        [Import] IServiceProvider hostProvider,
        [Import] IConfiguration configuration,
        [Import] ILoggerFactory loggerFactory,
        HybridCache? cache = null)
        : base(hostProvider, configuration, cache, loggerFactory.CreateLogger<ColoradoSummerEbtCaseService>())
    {
        ArgumentNullException.ThrowIfNull(hostProvider);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        _configuration = configuration;
        _logger = loggerFactory.CreateLogger<ColoradoSummerEbtCaseService>();
    }

    /// <inheritdoc />
    public async Task<HouseholdData?> GetHouseholdByIdentifierAsync(
        HouseholdIdentifierType identifierType,
        string identifierValue,
        PiiVisibility piiVisibility,
        IdentityAssuranceLevel identityAssuranceLevel,
        Guid? portalUserId = null,
        bool includeCardService = true,
        CancellationToken cancellationToken = default)
    {
        if (identifierType == HouseholdIdentifierType.Email)
        {
            return await GetHouseholdByGuardianEmailAsync(
                identifierValue,
                piiVisibility,
                identityAssuranceLevel,
                portalUserId,
                includeCardService,
                cancellationToken).ConfigureAwait(false);
        }

        if (identifierType == HouseholdIdentifierType.Phone)
            return await GetHouseholdByPhoneAsync(identifierValue, piiVisibility, includeCardService, cancellationToken).ConfigureAwait(false);

        return null;
    }

    /// <inheritdoc />
    public Task<HouseholdData?> GetHouseholdByGuardianEmailAsync(
        string guardianEmail,
        PiiVisibility piiVisibility,
        IdentityAssuranceLevel identityAssuranceLevel,
        Guid? portalUserId = null,
        bool includeCardService = true,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<HouseholdData?>(null);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Warehouse IC+DOB verification via <c>GetHouseholdByGuardian</c> is implemented only for DC.
    /// Colorado co-loaded SNAP/TANF matching uses on-file / connector data only (see portal use case).
    /// </remarks>
    public Task<bool> TryMatchCoLoadedGuardianByBenefitIdAndDobAsync(
        string benefitIdentifierIc,
        DateOnly guardianDateOfBirth,
        Guid portalUserId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }

    /// <inheritdoc />
    public Task<HouseholdData?> GetHouseholdByBenefitIdentifierAndDobAsync(
        string benefitIdentifierIc,
        DateOnly guardianDateOfBirth,
        string guardianLoginEmail,
        PiiVisibility piiVisibility,
        IdentityAssuranceLevel identityAssuranceLevel,
        Guid portalUserId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<HouseholdData?>(null);
    }

    private async Task<HouseholdData?> GetHouseholdByPhoneAsync(
        string phoneNumber,
        PiiVisibility piiVisibility,
        bool includeCardService,
        CancellationToken cancellationToken)
    {
        var options = CbmsOptionsHelper.GetCbmsOptions(_configuration);
        if (!options.IsConfigured)
        {
            return null;
        }

        var normalizedPhone = PhoneNormalizer.Normalize(phoneNumber);
        if (string.IsNullOrEmpty(normalizedPhone))
        {
            return null;
        }

        GetAccountDetailsResponse? response;
        try
        {
            response = await HouseholdCache!.GetAsync(normalizedPhone, includeCardService, cancellationToken).ConfigureAwait(false);
        }
        catch (ErrorResponse ex) when (ex.ResponseStatusCode == 404)
        {
            _logger.LogInformation(
                "{Dependency} GetAccountDetails (via cache): 404 (no household found)",
                "CBMS");
            return null;
        }
        catch (ErrorResponse ex)
        {
            _logger.LogError(ex,
                "{Dependency} GetAccountDetails (via cache) failed StatusCode={StatusCode}; AdditionalData={@AdditionalData}; ErrorDetails={@ErrorDetails}",
                "CBMS", ex.ResponseStatusCode, ex.AdditionalData, ex.ErrorDetails);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "{Dependency} GetAccountDetails (via cache) failed for phone lookup.",
                "CBMS");
            throw;
        }

        if (response is null || response.StdntEnrollDtls is null || response.StdntEnrollDtls.Count == 0)
            return null;

        return CbmsResponseMapper.MapToHouseholdData(response, normalizedPhone, piiVisibility, _logger);
    }
}
