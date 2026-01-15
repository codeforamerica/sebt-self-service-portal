namespace SEBT.Portal.Infrastructure.Data.Entities;

/// <summary>
/// Entity model for storing child information on a benefit application.
/// </summary>
public class ChildEntity
{
    /// <summary>
    /// Primary key identifier.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The ID of the household (foreign key to HouseholdEntity).
    /// </summary>
    public int HouseholdId { get; set; }

    /// <summary>
    /// The child's first name.
    /// </summary>
    public string FirstName { get; set; } = string.Empty;

    /// <summary>
    /// The child's last name.
    /// </summary>
    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// Navigation property to the household.
    /// </summary>
    public HouseholdEntity? Household { get; set; }
}
