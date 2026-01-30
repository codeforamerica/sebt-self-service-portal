namespace SEBT.Portal.Core.Models.Household;

/// <summary>
/// Represents a child on a benefit application.
/// </summary>
public class Child
{
    /// <summary>
    /// The case number associated with this child (this is distinct from application case number)
    /// </summary>
    public int? CaseNumber { get; set; }

    /// <summary>
    /// The child's first name.
    /// </summary>
    public string FirstName { get; set; } = string.Empty;

    /// <summary>
    /// The child's last name.
    /// </summary>
    public string LastName { get; set; } = string.Empty;
}
