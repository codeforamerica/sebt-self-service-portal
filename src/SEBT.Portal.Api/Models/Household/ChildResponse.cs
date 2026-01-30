namespace SEBT.Portal.Api.Models.Household;

/// <summary>
/// API response model for a child on a benefit application.
/// </summary>
public record ChildResponse
{
    /// <summary>
    /// The case number associated with this child (this is distinct from application case number)
    /// </summary>
    public int? CaseNumber { get; init; }

    /// <summary>
    /// The child's first name.
    /// </summary>
    public string FirstName { get; init; } = string.Empty;

    /// <summary>
    /// The child's last name.
    /// </summary>
    public string LastName { get; init; } = string.Empty;
}
