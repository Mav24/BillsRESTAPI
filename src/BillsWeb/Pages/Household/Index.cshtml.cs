using BillsWeb.Models;
using BillsWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BillsWeb.Pages.Household;

public class HouseholdIndexModel : PageModel
{
    private readonly IBillsApiClient _apiClient;

    public HouseholdViewModel? Household { get; set; }
    public string CurrentUsername { get; set; } = string.Empty;
    public bool IsCurrentUserOwner => Household is not null
        && Household.Members.Any(m => m.Id == Household.OwnerUserId && m.Username == CurrentUsername);

    private static bool IsOwner(HouseholdViewModel household, string username)
        => household.Members.Any(m => m.Id == household.OwnerUserId && m.Username == username);

    [TempData]
    public string? Message { get; set; }

    [TempData]
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

        CurrentUsername = HttpContext.Session.GetString("Username") ?? string.Empty;
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

    public async Task<IActionResult> OnPostInviteAsync(string email, bool shareExistingBills)
    {
        var token = HttpContext.Session.GetString("Token");
        if (string.IsNullOrEmpty(token))
        {
            return RedirectToPage("/Auth/Login");
        }

        var currentUsername = HttpContext.Session.GetString("Username") ?? string.Empty;
        var household = await _apiClient.GetMyHouseholdAsync(token);
        if (household is null)
        {
            ErrorMessage = "You must be in a household to invite members";
            return RedirectToPage();
        }

        if (!IsOwner(household, currentUsername))
        {
            ErrorMessage = "Only the main account holder can invite members";
            return RedirectToPage();
        }

        var success = await _apiClient.InviteToHouseholdAsync(email, shareExistingBills, token);
        
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

    public async Task<IActionResult> OnPostLeaveAsync()
    {
        var token = HttpContext.Session.GetString("Token");
        if (string.IsNullOrEmpty(token))
        {
            return RedirectToPage("/Auth/Login");
        }

        var success = await _apiClient.LeaveHouseholdAsync(token);
        
        if (success)
        {
            Message = "You have successfully left the household";
        }
        else
        {
            ErrorMessage = "Failed to leave household";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRemoveMemberAsync(string memberId)
    {
        var token = HttpContext.Session.GetString("Token");
        if (string.IsNullOrEmpty(token))
        {
            return RedirectToPage("/Auth/Login");
        }

        if (string.IsNullOrWhiteSpace(memberId))
        {
            ErrorMessage = "Invalid household member";
            return RedirectToPage();
        }

        var success = await _apiClient.RemoveHouseholdMemberAsync(memberId, token);

        if (success)
        {
            Message = "Member removed from household";
        }
        else
        {
            ErrorMessage = "Failed to remove member. Only the main account holder can remove members.";
        }

        return RedirectToPage();
    }
}
