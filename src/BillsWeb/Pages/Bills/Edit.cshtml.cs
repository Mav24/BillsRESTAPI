using BillsWeb.Models;
using BillsWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BillsWeb.Pages.Bills;

public class EditBillModel : PageModel
{
    private readonly IBillsApiClient _apiClient;

    [BindProperty]
    public BillViewModel Input { get; set; } = new();

    [BindProperty]
    public decimal? TheyOwe { get; set; }

    [BindProperty]
    public string? ShareWith { get; set; }

    public string? ErrorMessage { get; set; }

    public HouseholdViewModel? Household { get; set; }

    public string? CurrentUsername { get; set; }

    public EditBillModel(IBillsApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var token = HttpContext.Session.GetString("Token");
        if (string.IsNullOrEmpty(token))
        {
            return LocalRedirect("/billsweb/auth/login");
        }

        var bill = await _apiClient.GetBillAsync(id, token);
        if (bill == null)
        {
            return NotFound();
        }

        if (bill.IsShared)
        {
            return LocalRedirect("/billsweb/bills/calendar");
        }

        Input = bill;
        TheyOwe = bill.AmountOverMinimum;

        CurrentUsername = HttpContext.Session.GetString("Username");
        Household = await _apiClient.GetMyHouseholdAsync(token);

        // Determine current sharing state
        if (Household is not null)
        {
            // Check if shared with specific person
            var shares = await _apiClient.GetBillSharesAsync(id, token);
            if (shares.Count > 0)
            {
                ShareWith = shares[0].Email;
            }
            else if (bill.HouseholdId is not null)
            {
                // Bill is associated with the household
                ShareWith = "household";
            }
            else
            {
                ShareWith = "private";
            }
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var token = HttpContext.Session.GetString("Token");
        if (string.IsNullOrEmpty(token))
        {
            return LocalRedirect("/billsweb/auth/login");
        }

        if (!ModelState.IsValid)
        {
            CurrentUsername = HttpContext.Session.GetString("Username");
            Household = await _apiClient.GetMyHouseholdAsync(token);
            return Page();
        }

        // AmountOverMinimum stores what they owe; default to 0 (you pay the full amount)
        Input.AmountOverMinimum = TheyOwe ?? 0;
        Input.ShareWith = ShareWith ?? "private";

        var (success, errorMessage) = await _apiClient.UpdateBillAsync(Input.Id, Input, token);

        if (!success)
        {
            ErrorMessage = errorMessage ?? "Failed to update bill";
            CurrentUsername = HttpContext.Session.GetString("Username");
            Household = await _apiClient.GetMyHouseholdAsync(token);
            return Page();
        }

        return LocalRedirect("/billsweb/bills/calendar");
    }
}
