namespace SEBT.Portal.Core.Models.Household;

/// <summary>
/// State-issued identifiers for one Summer EBT case displayed to a portal user.
/// </summary>
public sealed record UserCaseIdentifiers(
    string CaseId,
    string? ApplicationId,
    string? ApplicationStudentId);
