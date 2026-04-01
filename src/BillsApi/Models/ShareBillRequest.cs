namespace BillsApi.Models;

/// <summary>
/// Request DTO for sharing a bill with another user.
/// </summary>
public class ShareBillRequest
{
    /// <summary>
    /// The email address of the user to share the bill with.
    /// </summary>
    public required string Email { get; set; }
}
