using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BlindMatchPAS.Areas.Identity.Pages.Account
{
    // Self-registration is disabled. All accounts are created by the Module Leader.
    public class RegisterModel : PageModel
    {
        public IActionResult OnGet(string? returnUrl = null)
        {
            TempData["Error"] = "Self-registration is disabled. Contact your Module Leader to get an account.";
            return RedirectToPage("/Account/Login");
        }

        public IActionResult OnPost(string? returnUrl = null)
        {
            TempData["Error"] = "Self-registration is disabled.";
            return RedirectToPage("/Account/Login");
        }
    }
}
