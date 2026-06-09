using BillsWeb.Models;
using BillsWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BillsWeb.Pages.Bills;

public class BillsCalendarModel : PageModel
{
    private readonly IBillsApiClient _apiClient;

    public Dictionary<int, List<BillViewModel>> BillsByDay { get; set; } = new();
    public int Year { get; set; }
    public int Month { get; set; }
    public string MonthName { get; set; } = string.Empty;
    public int DaysInMonth { get; set; }
    public int FirstDayOffset { get; set; }

    // Monthly summary totals
    public decimal MonthFullTotal { get; set; }
    public decimal MonthMyShare { get; set; }
    public decimal MonthTheyOwe { get; set; }
    public decimal MonthPaidTotal { get; set; }
    public int MonthUnpaidCount { get; set; }

    public BillsCalendarModel(IBillsApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<IActionResult> OnGetAsync(int? year, int? month)
    {
        var token = HttpContext.Session.GetString("Token");
        if (string.IsNullOrEmpty(token))
        {
            return LocalRedirect("/billsweb/auth/login");
        }

        var today = DateTime.Today;
        Year = year ?? today.Year;
        Month = month ?? today.Month;

        // Clamp to valid range
        if (Month < 1) Month = 1;
        if (Month > 12) Month = 12;

        var firstOfMonth = new DateTime(Year, Month, 1);
        MonthName = firstOfMonth.ToString("MMMM yyyy");
        DaysInMonth = DateTime.DaysInMonth(Year, Month);
        FirstDayOffset = (int)firstOfMonth.DayOfWeek; // 0=Sunday

        var allBills = await _apiClient.GetBillsAsync(token);

        var monthBills = allBills
            .Where(b => b.Date.Year == Year && b.Date.Month == Month)
            .ToList();

        BillsByDay = monthBills
            .GroupBy(b => b.Date.Day)
            .ToDictionary(g => g.Key, g => g.ToList());

        MonthFullTotal   = monthBills.Sum(b => b.Amount);
        MonthPaidTotal   = monthBills.Where(b => b.IsPaid).Sum(b => b.Amount);
        MonthTheyOwe     = monthBills.Where(b => !b.IsPaid).Sum(b => b.AmountOverMinimum);
        MonthMyShare     = monthBills.Where(b => !b.IsPaid).Sum(b => b.Amount - b.AmountOverMinimum);
        MonthUnpaidCount = monthBills.Count(b => !b.IsPaid);

        return Page();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int? id)
    {
        var token = HttpContext.Session.GetString("Token");
        if (string.IsNullOrEmpty(token))
        {
            return LocalRedirect("/billsweb/auth/login");
        }

        await _apiClient.DeleteBillAsync(id ?? 0, token);
        return RedirectToPage();
    }
}
