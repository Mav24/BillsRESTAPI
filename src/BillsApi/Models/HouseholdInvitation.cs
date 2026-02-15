namespace BillsApi.Models;

/// <summary>
/// Represents a pending invitation to join a household.
/// </summary>
public class HouseholdInvitation
{
    /// <summary>
    /// Unique identifier for the invitation.
    /// </summary>
    public string Id { get; set; } = null!;

    /// <summary>
    /// The ID of the household the user is being invited to.
    /// </summary>
    public Guid HouseholdId { get; set; }

    /// <summary>
    /// Email address of the invited user.
    /// </summary>
    public required string Email { get; set; }

    /// <summary>
    /// Hashed token for security (similar to password reset).
    /// </summary>
    public required string TokenHash { get; set; }

    /// <summary>
    /// ID of the user who sent the invitation.
    /// </summary>
    public required string InvitedByUserId { get; set; }

    /// <summary>
    /// When the invitation was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the invitation expires (typically 7 days).
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Whether the invitation has been accepted.
    /// </summary>
    public bool Accepted { get; set; }

    /// <summary>
    /// When the invitation was accepted (null if not yet accepted).
    /// </summary>
    public DateTime? AcceptedAt { get; set; }

    /// <summary>
    /// Navigation property for the household.
    /// </summary>
    public Household? Household { get; set; }

    /// <summary>
    /// Navigation property for the user who sent the invitation.
    /// </summary>
    public User? InvitedByUser { get; set; }
}
