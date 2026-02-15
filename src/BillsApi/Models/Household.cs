namespace BillsApi.Models;

/// <summary>
/// Represents a household that contains multiple users and shared bills.
/// </summary>
public class Household
{
    /// <summary>
    /// Unique identifier for the household.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Name of the household (e.g., "Smith Family").
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Date when the household was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Navigation property for users in this household.
    /// </summary>
    public ICollection<User> Members { get; set; } = new List<User>();

    /// <summary>
    /// Navigation property for bills belonging to this household.
    /// </summary>
    public ICollection<Bill> Bills { get; set; } = new List<Bill>();
}