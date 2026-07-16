namespace SEBT.Portal.StatesPlugins.Interfaces.Models.Household;

/// <summary>
/// Request to issue a replacement card for one or more cases in a household.
/// </summary>
public class CardReplacementRequest
{
    /// <summary>The household identifier value (e.g., guardian email) resolved by the portal.</summary>
    public required string HouseholdIdentifierValue { get; init; }

    /// <summary>
    /// Case references the replacement applies to. Carries the unique-key triple
    /// (<see cref="CaseRef.SummerEbtCaseId"/>, <see cref="CaseRef.ApplicationId"/>,
    /// <see cref="CaseRef.ApplicationStudentId"/>) so connectors can resolve cases
    /// unambiguously even when multiple upstream rows share a per-child identifier.
    /// </summary>
    public required IReadOnlyList<CaseRef> CaseRefs { get; init; }

    /// <summary>Reason for the replacement request. <see cref="CardReplacementReason.Unspecified"/> when the UI does not collect one.</summary>
    public required CardReplacementReason Reason { get; init; }
}
