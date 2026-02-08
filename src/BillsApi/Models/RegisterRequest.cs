namespace BillsApi.Models;

/// <summary>
/// Request model for user registration.
/// </summary>
public class RegisterRequest
{
    /// <summary>
    /// The desired username.
    /// </summary>
    public required string Username { get; set; }

    /// <summary>
    /// The password.
    /// </summary>
    public required string Password { get; set; }

    /// <summary>
    /// The email address.
    /// </summary>
    public required string Email { get; set; }
}
