using BillsWeb.Models;
using BillsWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BillsWeb.Pages.Bills;

public class BillsIndexModel : PageModel
{
    private readonly IBillsApiClient _apiClient;

    public List<BillViewModel> Bills { get; set; } = new();
    public IEnumerable<IGrouping<string, BillViewModel>> BillsByMonth { get; set; } = Enumerable.Empty<IGrouping<string, BillViewModel>>();
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
            return LocalRedirect("/billsweb/auth/login");
        }

        Bills = await _apiClient.GetBillsAsync(token);
        
        BillsByMonth = Bills
            .OrderBy(b => b.Date)
            .GroupBy(b => b.Date.ToString("MMMM yyyy"));
        
        return Page();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var token = HttpContext.Session.GetString("Token");
        if (string.IsNullOrEmpty(token))
        {
            return LocalRedirect("/billsweb/auth/login");
        }

        var success = await _apiClient.DeleteBillAsync(id, token);
        if (success)
        {
            Message = "Bill deleted successfully";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostToggleMonthPaidAsync(string month, bool isPaid)
    {
        var token = HttpContext.Session.GetString("Token");
        if (string.IsNullOrEmpty(token))
        {
            return LocalRedirect("/billsweb/auth/login");
        }

        var bills = await _apiClient.GetBillsAsync(token);
        var monthBills = bills.Where(b => b.Date.ToString("MMMM yyyy") == month).ToList();

        var successCount = 0;
        foreach (var bill in monthBills)
        {
            var success = await _apiClient.ToggleBillPaidAsync(bill.Id, isPaid, token);
            if (success)
            {
                successCount++;
            }
        }

        if (successCount > 0)
        {
            Message = $"Marked {successCount} bill(s) as {(isPaid ? "paid" : "unpaid")}";
        }

        return RedirectToPage();
    }
}
