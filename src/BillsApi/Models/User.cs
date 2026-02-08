namespace BillsApi.Models;

/// <summary>
/// Represents a user in the system.
/// </summary>
public class User
{
    /// <summary>
    /// Unique identifier for the user.
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// The username (must be unique).
    /// </summary>
    public required string Username { get; set; }

    /// <summary>
    /// <summary>
    /// The email address for the user (used for password reset and notifications).
    /// </summary>
    public required string Email { get; set; }

    /// <summary>
    /// The hashed password.
    /// </summary>
    public required string PasswordHash { get; set; }
}
