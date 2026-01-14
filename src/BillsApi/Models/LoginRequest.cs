namespace BillsApi.Models;

/// <summary>
/// Request model for user login.
/// </summary>
public class LoginRequest
{
    /// <summary>
    /// The username.
    /// </summary>
    public required string Username { get; set; }

    /// <summary>
    /// The password.
    /// </summary>
    public required string Password { get; set; }
}
