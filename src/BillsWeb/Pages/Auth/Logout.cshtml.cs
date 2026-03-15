using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BillsWeb.Pages.Auth;

public class LogoutModel : PageModel
{
    public IActionResult OnGet()
    {
        HttpContext.Session.Clear();
        HttpContext.Response.Cookies.Delete("BillsWeb.AuthToken");
        HttpContext.Response.Cookies.Delete("BillsWeb.RefreshToken");
        HttpContext.Response.Cookies.Delete("BillsWeb.Username");
        return LocalRedirect("/billsweb");
    }
}
