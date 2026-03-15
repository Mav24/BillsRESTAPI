using BillsWeb.Models;
using BillsWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BillsWeb.Pages.Auth;

public class RegisterModel : PageModel
{
    private readonly IBillsApiClient _apiClient;

    [BindProperty]
    public RegisterViewModel Input { get; set; } = new();

    public string? ErrorMessage { get; set; }
    
    public bool RegistrationSuccess { get; set; } = false;

    public RegisterModel(IBillsApiClient apiClient)
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
        System.Diagnostics.Debug.WriteLine("=== Register OnPostAsync Started ===");
        
        if (!ModelState.IsValid)
        {
            System.Diagnostics.Debug.WriteLine("ModelState Invalid");
            return Page();
        }

        System.Diagnostics.Debug.WriteLine($"Attempting registration for user: {Input.Username}");
        var registerResult = await _apiClient.RegisterAsync(Input.Username, Input.Email, Input.Password);

        if (registerResult == null)
        {
            ErrorMessage = "Registration failed. Username or email may already be in use.";
            System.Diagnostics.Debug.WriteLine("Registration failed - API returned null");
            return Page();
        }
        // Registration succeeded — redirect user to login page. Automatic login caused issues in some hosts.
        System.Diagnostics.Debug.WriteLine("Registration succeeded - redirecting user to login page");
        return LocalRedirect("/billsweb/auth/login");
    }
}
