namespace SEBT.Portal.StatesPlugins.Interfaces.Models.Household;

/// <summary>
/// Reference to a specific case for write-path operations (card replacement).
/// State-agnostic: each connector populates whichever subset of identifiers its upstream provides
/// and reads whichever subset it needs.
/// </summary>
public class CaseRef
{
    /// <summary>
    /// Primary case identifier (from <see cref="Data.Cases.SummerEbtCase.SummerEBTCaseID"/>).
    /// Always present.
    /// </summary>
    public required string SummerEbtCaseId { get; init; }

    /// <summary>
    /// Application identifier when the case is application-based; null for auto-eligible cases.
    /// CO populates from CBMS <c>sebtAppId</c> when <c>eligSrc ∈ {CBMS, PK}</c>.
    /// </summary>
    public string? ApplicationId { get; init; }

    /// <summary>
    /// Per-(case, child) identifier when the case is application-based; null for auto-eligible cases.
    /// CO populates from CBMS <c>sebtChldId</c> when <c>eligSrc ∈ {CBMS, PK}</c>.
    /// </summary>
    public string? ApplicationStudentId { get; init; }
}
