using BillsWeb.Models;
using BillsWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BillsWeb.Pages.Bills;

public class BillsIndexModel : PageModel
{
    private readonly IBillsApiClient _apiClient;

    public List<BillViewModel> Bills { get; set; } = new();
    public string? Message { get; set; }

    public BillsIndexModel(IBillsApiClient apiClient)
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

        Bills = await _apiClient.GetBillsAsync(token);
        return Page();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var token = HttpContext.Session.GetString("Token");
        if (string.IsNullOrEmpty(token))
        {
            return RedirectToPage("/Auth/Login");
        }

        var success = await _apiClient.DeleteBillAsync(id, token);
        if (success)
        {
            Message = "Bill deleted successfully";
        }

        return RedirectToPage();
    }
}
