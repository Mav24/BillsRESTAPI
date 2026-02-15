namespace BillsApi.Models;

/// <summary>
/// Request model for updating user email.
/// </summary>
public class UpdateEmailRequest
{
    /// <summary>
    /// The new email address.
    /// </summary>
    public required string Email { get; set; }

    /// <summary>
    /// The user's current password for verification.
    /// </summary>
    public required string CurrentPassword { get; set; }
}
