using BillsWeb.Models;
using BillsWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BillsWeb.Pages.Auth;

public class LoginModel : PageModel
{
    private readonly IBillsApiClient _apiClient;

    [BindProperty]
    public LoginViewModel Input { get; set; } = new();

    public string? ErrorMessage { get; set; }

    public LoginModel(IBillsApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public void OnGet()
    {
        if (HttpContext.Session.GetString("Token") != null)
        {
            Response.Redirect("/Bills");
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            ErrorMessage = "Please fill in all required fields";
            return Page();
        }

        try
        {
            var result = await _apiClient.LoginAsync(Input.Username, Input.Password);
            
            if (result == null)
            {
                ErrorMessage = "Invalid username or password - API returned null";
                return Page();
            }

            if (string.IsNullOrEmpty(result.AccessToken))
            {
                ErrorMessage = "Login failed - No access token received";
                return Page();
            }

            HttpContext.Session.SetString("Token", result.AccessToken);
            HttpContext.Session.SetString("RefreshToken", result.RefreshToken);
            HttpContext.Session.SetString("Username", Input.Username);

            return RedirectToPage("/Bills/Index");
        }
        catch (HttpRequestException ex)
        {
            ErrorMessage = $"Network error: {ex.Message}";
            return Page();
        }
        catch (TaskCanceledException ex)
        {
            ErrorMessage = $"Request timeout: {ex.Message}";
            return Page();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error: {ex.GetType().Name} - {ex.Message}";
            return Page();
        }
    }
}
