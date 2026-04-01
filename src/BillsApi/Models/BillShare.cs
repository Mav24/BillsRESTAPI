namespace BillsApi.Models;

/// <summary>
/// Represents a bill shared with a specific user.
/// </summary>
public class BillShare
{
    /// <summary>
    /// Unique identifier for the share record.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The ID of the bill being shared.
    /// </summary>
    public int BillId { get; set; }

    /// <summary>
    /// The ID of the user who shared the bill (the bill owner).
    /// </summary>
    public required string SharedByUserId { get; set; }

    /// <summary>
    /// The ID of the user the bill is shared with.
    /// </summary>
    public required string SharedWithUserId { get; set; }

    /// <summary>
    /// When the bill was shared.
    /// </summary>
    public DateTime SharedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Navigation property for the bill.
    /// </summary>
    public Bill? Bill { get; set; }

    /// <summary>
    /// Navigation property for the user who shared the bill.
    /// </summary>
    public User? SharedByUser { get; set; }

    /// <summary>
    /// Navigation property for the user the bill is shared with.
    /// </summary>
    public User? SharedWithUser { get; set; }
}
