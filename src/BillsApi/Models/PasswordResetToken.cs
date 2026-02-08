using System;

namespace BillsApi.Models;

/// <summary>
/// A single-use password reset token stored as a hash in the database.
/// </summary>
public class PasswordResetToken
{
    public required string Id { get; set; } = Guid.NewGuid().ToString();

    // FK to User.Id
    public required string UserId { get; set; }

    // Store only the hash of the token sent via email
    public required string TokenHash { get; set; }

    // UTC expiry timestamp
    public required DateTime ExpiresAt { get; set; }

    // Mark token as used after successful reset
    public bool Used { get; set; }

    // Navigation property
    public User? User { get; set; }
}
