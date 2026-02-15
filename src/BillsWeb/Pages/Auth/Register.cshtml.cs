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

    public RegisterModel(IBillsApiClient apiClient)
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
            return Page();
        }

        var result = await _apiClient.RegisterAsync(Input.Username, Input.Email, Input.Password);
        
        if (result == null)
        {
            ErrorMessage = "Registration failed. Username or email may already be in use.";
            return Page();
        }

        HttpContext.Session.SetString("Token", result.AccessToken);
        HttpContext.Session.SetString("RefreshToken", result.RefreshToken);
        HttpContext.Session.SetString("Username", Input.Username);

        return RedirectToPage("/Bills/Index");
    }
}
