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

    public string? ErrorMessage { get; set; }

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

        Input = bill;
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

        var success = await _apiClient.UpdateBillAsync(Input.Id, Input, token);
        
        if (!success)
        {
            ErrorMessage = "Failed to update bill";
            return Page();
        }

        return LocalRedirect("/billsweb/bills/index");
    }
}
