namespace BillsApi.Models;

/// <summary>
/// Represents a refresh token for extended authentication.
/// </summary>
public class RefreshToken
{
    public required string Id { get; set; }
    public required string Token { get; set; }
    public required string UserId { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsRevoked { get; set; }
    
    // Navigation property
    public User? User { get; set; }
}