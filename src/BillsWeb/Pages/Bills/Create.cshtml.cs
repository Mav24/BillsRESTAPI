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

    public string? ErrorMessage { get; set; }

    public CreateBillModel(IBillsApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public IActionResult OnGet()
    {
        var token = HttpContext.Session.GetString("Token");
        if (string.IsNullOrEmpty(token))
        {
            return LocalRedirect("/billsweb/auth/login");
        }

        Input.Date = DateTime.Now;
        Input.IsPaid = false; // New bills are never paid on creation
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
            return Page();
        }

        // AmountOverMinimum stores what they owe; default to 0 (you pay the full amount)
        Input.AmountOverMinimum = TheyOwe ?? 0;

        var result = await _apiClient.CreateBillAsync(Input, token);
        
        if (result == null)
        {
            ErrorMessage = "Failed to create bill";
            return Page();
        }

        return LocalRedirect("/billsweb/bills/index");
    }
}
