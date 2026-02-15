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

    public async Task<BillViewModel?> CreateBillAsync(BillViewModel bill, string token)
    {
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        
        var content = new StringContent(JsonSerializer.Serialize(bill, _jsonOptions), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync("bills", content);
        
        if (!response.IsSuccessStatusCode)
            return null;

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<BillViewModel>(json, _jsonOptions);
    }

    public async Task<bool> UpdateBillAsync(int id, BillViewModel bill, string token)
    {
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        
        var content = new StringContent(JsonSerializer.Serialize(bill, _jsonOptions), Encoding.UTF8, "application/json");
        var response = await _httpClient.PutAsync($"bills/{id}", content);
        
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteBillAsync(int id, string token)
    {
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        
        var response = await _httpClient.DeleteAsync($"bills/{id}");
        return response.IsSuccessStatusCode;
    }

    public async Task<HouseholdViewModel?> GetMyHouseholdAsync(string token)
    {
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        
        var response = await _httpClient.GetAsync("households/my-household");
        if (!response.IsSuccessStatusCode)
            return null;

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<HouseholdViewModel>(json, _jsonOptions);
    }

    public async Task<bool> CreateHouseholdAsync(string name, string token)
    {
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        
        var request = new { name };
        var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync("households", content);
        
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> InviteToHouseholdAsync(string email, string token)
    {
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        
        var request = new { email };
        var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync("households/invite", content);
        
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
}
