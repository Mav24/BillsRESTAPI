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
    
    public bool LoginSuccess { get; set; } = false;

    public LoginModel(IBillsApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public IActionResult OnGet()
    {
        if (HttpContext.Session.GetString("Token") != null)
        {
            return LocalRedirect("/billsweb/bills/index");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        System.Diagnostics.Debug.WriteLine("=== Login OnPostAsync Started ===");
        
        if (!ModelState.IsValid)
        {
            ErrorMessage = "Please fill in all required fields";
            System.Diagnostics.Debug.WriteLine("ModelState Invalid");
            return Page();
        }

        try
        {
            System.Diagnostics.Debug.WriteLine($"Attempting login for user: {Input.Username}");
            var result = await _apiClient.LoginAsync(Input.Username, Input.Password);
            
            if (result == null)
            {
                ErrorMessage = "Invalid username or password - API returned null";
                System.Diagnostics.Debug.WriteLine("API returned null");
                return Page();
            }

            if (string.IsNullOrEmpty(result.AccessToken))
            {
                ErrorMessage = "Login failed - No access token received";
                System.Diagnostics.Debug.WriteLine("No access token received");
                return Page();
            }

            System.Diagnostics.Debug.WriteLine($"Login successful, setting session");
            HttpContext.Session.SetString("Token", result.AccessToken);
            HttpContext.Session.SetString("RefreshToken", result.RefreshToken);
            HttpContext.Session.SetString("Username", Input.Username);
            
            // Ensure session is saved before returning
            await HttpContext.Session.CommitAsync();
            System.Diagnostics.Debug.WriteLine($"Session committed, Session ID: {HttpContext.Session.Id}");
            // Log Set-Cookie header emitted by the response for debugging
            try
            {
                var setCookie = HttpContext.Response.Headers["Set-Cookie"].ToString();
                System.Diagnostics.Debug.WriteLine($"Response Set-Cookie: {setCookie}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to read Set-Cookie header: {ex.Message}");
            }

            // Redirect directly to Bills Index page (use the app path so session cookie is sent)
            // Also create a backup HttpOnly cookie with the access token so we can recover session if in-memory
            try
            {
                var cookieOptions = new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.None,
                    Path = "/"
                };
                HttpContext.Response.Cookies.Append("BillsWeb.AuthToken", result.AccessToken, cookieOptions);
            }
            catch { /* best-effort, ignore on failure */ }

            System.Diagnostics.Debug.WriteLine("Login complete, redirecting to /billsweb/bills/index");
            return LocalRedirect("/billsweb/bills/index");
        }
        catch (HttpRequestException ex)
        {
            ErrorMessage = $"Network error: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"HttpRequestException: {ex}");
            return Page();
        }
        catch (TaskCanceledException ex)
        {
            ErrorMessage = $"Request timeout: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"TaskCanceledException: {ex}");
            return Page();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error: {ex.GetType().Name} - {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"Exception: {ex}");
            return Page();
        }
    }
}
