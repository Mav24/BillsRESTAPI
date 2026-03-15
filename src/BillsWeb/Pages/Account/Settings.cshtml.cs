using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using BillsWeb.Services;

namespace BillsWeb.Pages.Account;

public class SettingsModel : PageModel
{
    private readonly IBillsApiClient _apiClient;

    public SettingsModel(IBillsApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [TempData]
    public string? ErrorMessage { get; set; }

    [TempData]
    public string? SuccessMessage { get; set; }

    public IActionResult OnGet()
    {
        var token = HttpContext.Session.GetString("Token");
        if (string.IsNullOrEmpty(token))
        {
            return LocalRedirect("/billsweb/auth/login");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostDeleteAccountAsync()
    {
        var token = HttpContext.Session.GetString("Token");
        if (string.IsNullOrEmpty(token))
        {
            return LocalRedirect("/billsweb/auth/login");
        }

        try
        {
            var success = await _apiClient.DeleteAccountAsync(token);

            if (success)
            {
                // Clear session and redirect to home page
                HttpContext.Session.Clear();
                TempData["SuccessMessage"] = "Your account has been successfully deleted.";
                return LocalRedirect("/billsweb");
            }
            else
            {
                ErrorMessage = "Failed to delete account. Please try again.";
                return Page();
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error deleting account: {ex.Message}";
            return Page();
        }
    }
}
