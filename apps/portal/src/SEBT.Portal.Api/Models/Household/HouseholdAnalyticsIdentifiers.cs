extern alias Core;

namespace SEBT.Portal.Api.Models.Household;

using HouseholdData = Core::SEBT.Portal.Core.Models.Household.HouseholdData;
using IIdentifierHasher = Core::SEBT.Portal.Core.Services.IIdentifierHasher;

/// <summary>
/// Keyed digests of a household's state identifiers, which analytics uses to join vendor
/// exports back to state program data without exposing the raw identifiers.
/// </summary>
/// <param name="HashedAppId">The full digest of one application id, chosen the same way on every load.</param>
/// <param name="HashedAppIds">Sorted, comma-joined digest prefixes of every application id in the household.</param>
/// <param name="HashedCaseIds">Sorted, comma-joined digest prefixes of every child's case id in the household.</param>
public sealed record HouseholdAnalyticsIdentifiers(string? HashedAppId, string? HashedAppIds, string? HashedCaseIds)
{
    // Mixpanel truncates strings at 255 bytes; 16-character prefixes keep 15 ids under it.
    private const int ListDigestLength = 16;

    /// <summary>
    /// Hashes the household's application and case ids. Each field is null when the household has none.
    /// </summary>
    public static HouseholdAnalyticsIdentifiers Resolve(HouseholdData household, IIdentifierHasher hasher)
    {
        var applicationNumbers = household.Applications.Select(a => a.ApplicationNumber).ToList();
        var caseApplicationIds = household.SummerEbtCases.Select(c => c.SourceApplicationId).ToList();
        var childIds = household.SummerEbtCases.Select(c => c.SourceChildId)
            .Concat(household.Applications.SelectMany(a => a.Children).Select(c => c.SourceChildId));

        // Applications come first so a household that already had a digest keeps it.
        var primaryAppId = LowestId(applicationNumbers) ?? LowestId(caseApplicationIds);

        return new HouseholdAnalyticsIdentifiers(
            hasher.HashForAnalytics(primaryAppId),
            JoinDigestPrefixes(applicationNumbers.Concat(caseApplicationIds), hasher),
            JoinDigestPrefixes(childIds, hasher));
    }

    // Ordinal sort picks the same id however the connector orders its rows, keeping the digest stable across loads.
    private static string? LowestId(IEnumerable<string?> ids) =>
        ids.Where(id => !string.IsNullOrWhiteSpace(id)).Order(StringComparer.Ordinal).FirstOrDefault();

    private static string? JoinDigestPrefixes(IEnumerable<string?> ids, IIdentifierHasher hasher)
    {
        var prefixes = ids
            .Select(hasher.HashForAnalytics)
            .OfType<string>()
            .Select(digest => digest[..ListDigestLength])
            .Distinct()
            .Order(StringComparer.Ordinal)
            .ToList();
        return prefixes.Count == 0 ? null : string.Join(',', prefixes);
    }
}
