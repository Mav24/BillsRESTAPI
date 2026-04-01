using BillsWeb.Models;

namespace BillsWeb.Services;

public interface IBillsApiClient
{
    Task<AuthResponse?> LoginAsync(string username, string password);
    Task<AuthResponse?> RegisterAsync(string username, string email, string password);
    Task<List<BillViewModel>> GetBillsAsync(string token);
    Task<BillViewModel?> GetBillAsync(int id, string token);
    Task<(BillViewModel? Bill, string? ErrorMessage)> CreateBillAsync(BillViewModel bill, string token);
    Task<(bool Success, string? ErrorMessage)> UpdateBillAsync(int id, BillViewModel bill, string token);
    Task<bool> ToggleBillPaidAsync(int id, bool isPaid, string token);
    Task<bool> DeleteBillAsync(int id, string token);
    Task<bool> ShareBillAsync(int billId, string email, string token);
    Task<bool> UnshareBillAsync(int billId, string email, string token);
    Task<List<BillShareViewModel>> GetBillSharesAsync(int billId, string token);
    Task<HouseholdViewModel?> GetMyHouseholdAsync(string token);
    Task<bool> CreateHouseholdAsync(string name, string token);
    Task<bool> InviteToHouseholdAsync(string email, bool shareExistingBills, string token);
    Task<bool> LeaveHouseholdAsync(string token);
    Task<bool> UpdateEmailAsync(string email, string currentPassword, string token);
    Task<bool> DeleteAccountAsync(string token);
    Task<AuthResponse?> RefreshTokenAsync(string refreshToken);
}

public class AuthResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public int ExpiresIn { get; set; }
}
