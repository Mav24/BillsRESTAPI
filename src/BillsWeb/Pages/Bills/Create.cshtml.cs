using BillsWeb.Models;
using BillsWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BillsWeb.Pages.Bills;

public class CreateBillModel : PageModel
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

    public CreateBillModel(IBillsApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var token = HttpContext.Session.GetString("Token");
        if (string.IsNullOrEmpty(token))
        {
            return LocalRedirect("/billsweb/auth/login");
        }

        Input.Date = DateTime.Now;
        Input.IsPaid = false;

        CurrentUsername = HttpContext.Session.GetString("Username");
        Household = await _apiClient.GetMyHouseholdAsync(token);
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

        var (result, errorMessage) = await _apiClient.CreateBillAsync(Input, token);

        if (result == null)
        {
            ErrorMessage = errorMessage ?? "Failed to create bill";
            CurrentUsername = HttpContext.Session.GetString("Username");
            Household = await _apiClient.GetMyHouseholdAsync(token);
            return Page();
        }

        return LocalRedirect("/billsweb/bills/calendar");
    }
}
