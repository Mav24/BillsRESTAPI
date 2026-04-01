using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using BillsWeb.Models;

namespace BillsWeb.Services;

public class BillsApiClient : IBillsApiClient
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;

    public BillsApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public async Task<AuthResponse?> LoginAsync(string username, string password)
    {
        try
        {
            var request = new { username, password };
            var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync("auth/login", content);
            var json = await response.Content.ReadAsStringAsync();
            
            if (!response.IsSuccessStatusCode)
            {
                // Log the error for debugging
                throw new HttpRequestException($"API returned {response.StatusCode}: {json}");
            }

            var result = JsonSerializer.Deserialize<AuthResponse>(json, _jsonOptions);
            
            if (result == null || string.IsNullOrEmpty(result.AccessToken))
            {
                throw new InvalidOperationException($"Failed to deserialize response. JSON: {json}");
            }
            
            return result;
        }
        catch (HttpRequestException)
        {
            throw; // Re-throw to be caught by Login page
        }
        catch (Exception ex)
        {
            throw new Exception($"Login failed: {ex.Message}", ex);
        }
    }

    public async Task<AuthResponse?> RegisterAsync(string username, string email, string password)
    {
        var request = new { username, email, password };
        var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
        
        var response = await _httpClient.PostAsync("auth/register", content);
        if (!response.IsSuccessStatusCode)
            return null;

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<AuthResponse>(json, _jsonOptions);
    }

    public async Task<List<BillViewModel>> GetBillsAsync(string token)
    {
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        
        var response = await _httpClient.GetAsync("bills");
        if (!response.IsSuccessStatusCode)
            return new List<BillViewModel>();

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<List<BillViewModel>>(json, _jsonOptions) ?? new List<BillViewModel>();
    }

    public async Task<BillViewModel?> GetBillAsync(int id, string token)
    {
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        
        var response = await _httpClient.GetAsync($"bills/{id}");
        if (!response.IsSuccessStatusCode)
            return null;

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<BillViewModel>(json, _jsonOptions);
    }

    public async Task<(BillViewModel? Bill, string? ErrorMessage)> CreateBillAsync(BillViewModel bill, string token)
    {
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var content = new StringContent(JsonSerializer.Serialize(bill, _jsonOptions), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync("bills", content);

        var json = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            // Try to extract error message from API response
            try
            {
                var errorObj = JsonSerializer.Deserialize<JsonElement>(json);
                if (errorObj.TryGetProperty("error", out var errorProp))
                    return (null, errorProp.GetString());
            }
            catch { }
            return (null, $"API returned {(int)response.StatusCode}");
        }

        var result = JsonSerializer.Deserialize<BillViewModel>(json, _jsonOptions);
        return (result, null);
    }

    public async Task<(bool Success, string? ErrorMessage)> UpdateBillAsync(int id, BillViewModel bill, string token)
    {
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var content = new StringContent(JsonSerializer.Serialize(bill, _jsonOptions), Encoding.UTF8, "application/json");
        var response = await _httpClient.PutAsync($"bills/{id}", content);

        if (!response.IsSuccessStatusCode)
        {
            var json = await response.Content.ReadAsStringAsync();
            try
            {
                var errorObj = JsonSerializer.Deserialize<JsonElement>(json);
                if (errorObj.TryGetProperty("error", out var errorProp))
                    return (false, errorProp.GetString());
            }
            catch { }
            return (false, $"API returned {(int)response.StatusCode}");
        }

        return (true, null);
    }

    public async Task<bool> ToggleBillPaidAsync(int id, bool isPaid, string token)
    {
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // First get the bill to preserve other fields
        var bill = await GetBillAsync(id, token);
        if (bill == null)
            return false;

        // Update only paid status and paid date
        bill.IsPaid = isPaid;
        bill.PaidDate = isPaid ? DateTime.Now : null;

        // Use the existing PUT endpoint to update the bill
        var (success, _) = await UpdateBillAsync(id, bill, token);
        return success;
    }

    public async Task<bool> DeleteBillAsync(int id, string token)
    {
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _httpClient.DeleteAsync($"bills/{id}");
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> ShareBillAsync(int billId, string email, string token)
    {
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var request = new { email };
        var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync($"bills/{billId}/share", content);

        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UnshareBillAsync(int billId, string email, string token)
    {
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _httpClient.DeleteAsync($"bills/{billId}/share/{Uri.EscapeDataString(email)}");
        return response.IsSuccessStatusCode;
    }

    public async Task<List<BillShareViewModel>> GetBillSharesAsync(int billId, string token)
    {
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _httpClient.GetAsync($"bills/{billId}/shares");
        if (!response.IsSuccessStatusCode)
            return new List<BillShareViewModel>();

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<List<BillShareViewModel>>(json, _jsonOptions) ?? new List<BillShareViewModel>();
    }

    public async Task<HouseholdViewModel?> GetMyHouseholdAsync(string token)
    {
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        
        var response = await _httpClient.GetAsync("households/my-household");
        if (!response.IsSuccessStatusCode)
            return null;

        var json = await response.Content.ReadAsStringAsync();
        
        // API returns { "household": { ... } }, so we need to deserialize the wrapper first
        var wrapper = JsonSerializer.Deserialize<HouseholdResponseWrapper>(json, _jsonOptions);
        return wrapper?.Household;
    }

    public async Task<bool> CreateHouseholdAsync(string name, string token)
    {
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        
        var request = new { name };
        var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync("households", content);
        
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> InviteToHouseholdAsync(string email, bool shareExistingBills, string token)
    {
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var request = new { email, shareExistingBills };
        var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync("households/invite", content);

        return response.IsSuccessStatusCode;
    }

    public async Task<bool> LeaveHouseholdAsync(string token)
    {
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        
        var response = await _httpClient.PostAsync("households/leave", null);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateEmailAsync(string email, string currentPassword, string token)
    {
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        
        var request = new { email, currentPassword };
        var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
        var response = await _httpClient.PatchAsync("auth/email", content);
        
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteAccountAsync(string token)
    {
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _httpClient.DeleteAsync("auth/account");
        return response.IsSuccessStatusCode;
    }

    public async Task<AuthResponse?> RefreshTokenAsync(string refreshToken)
    {
        try
        {
            var request = new { refreshToken };
            var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("auth/refresh", content);

            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<AuthResponse>(json, _jsonOptions);
        }
        catch
        {
            return null;
        }
    }
}

// Helper class to deserialize the household response wrapper
internal class HouseholdResponseWrapper
{
    public HouseholdViewModel? Household { get; set; }
}

