namespace SEBT.Portal.Core.Repositories;

/// <summary>
/// Repository for tracking card replacement requests.
/// Used to enforce the 14-day cooldown between replacement requests per case.
/// All identifier values are pre-hashed by callers via <see cref="Services.IIdentifierHasher"/>.
/// </summary>
public interface ICardReplacementRequestRepository
{
    /// <summary>
    /// Checks whether a card replacement request exists for the given household+case
    /// within the specified cooldown period.
    /// </summary>
    /// <param name="householdIdentifierHash">HMAC hash of the household identifier.</param>
    /// <param name="caseIdHash">HMAC hash of the case ID.</param>
    /// <param name="cooldownPeriod">The minimum time that must elapse between requests.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if a request exists within the cooldown window.</returns>
    Task<bool> HasRecentRequestAsync(
        string householdIdentifierHash,
        string caseIdHash,
        TimeSpan cooldownPeriod,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the most recent request timestamp for a given household+case,
    /// or null if no request has ever been recorded.
    /// Used to hydrate <see cref="Models.Household.SummerEbtCase.CardRequestedAt"/>
    /// so the frontend can enforce cooldown UI.
    /// </summary>
    Task<DateTime?> GetMostRecentRequestDateAsync(
        string householdIdentifierHash,
        string caseIdHash,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a new card replacement request.
    /// </summary>
    /// <param name="householdIdentifierHash">HMAC hash of the household identifier.</param>
    /// <param name="caseIdHash">HMAC hash of the case ID.</param>
    /// <param name="requestedByUserId">The ID of the user making the request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task CreateAsync(
        string householdIdentifierHash,
        string caseIdHash,
        Guid requestedByUserId,
        CancellationToken cancellationToken = default);
}
