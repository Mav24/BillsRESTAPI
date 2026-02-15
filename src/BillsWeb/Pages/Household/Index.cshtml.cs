using BillsWeb.Models;
using BillsWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BillsWeb.Pages.Household;

public class HouseholdIndexModel : PageModel
{
    private readonly IBillsApiClient _apiClient;

    public HouseholdViewModel? Household { get; set; }
    public string? Message { get; set; }
    public string? ErrorMessage { get; set; }

    public HouseholdIndexModel(IBillsApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var token = HttpContext.Session.GetString("Token");
        if (string.IsNullOrEmpty(token))
        {
            return RedirectToPage("/Auth/Login");
        }

        Household = await _apiClient.GetMyHouseholdAsync(token);
        return Page();
    }

    public async Task<IActionResult> OnPostCreateAsync(string name)
    {
        var token = HttpContext.Session.GetString("Token");
        if (string.IsNullOrEmpty(token))
        {
            return RedirectToPage("/Auth/Login");
        }

        var success = await _apiClient.CreateHouseholdAsync(name, token);
        
        if (success)
        {
            Message = "Household created successfully";
        }
        else
        {
            ErrorMessage = "Failed to create household";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostInviteAsync(string email)
    {
        var token = HttpContext.Session.GetString("Token");
        if (string.IsNullOrEmpty(token))
        {
            return RedirectToPage("/Auth/Login");
        }

        var success = await _apiClient.InviteToHouseholdAsync(email, token);
        
        if (success)
        {
            Message = "Invitation sent successfully";
        }
        else
        {
            ErrorMessage = "Failed to send invitation";
        }

        return RedirectToPage();
    }
}
