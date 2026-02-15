namespace BillsApi.Models;

/// <summary>
/// Request to invite a user to a household by email.
/// </summary>
public record InviteRequest(string Email);
