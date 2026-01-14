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
}
