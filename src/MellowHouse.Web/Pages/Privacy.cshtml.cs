using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MellowHouse.Web.Pages;

public class PrivacyModel : PageModel
{
    public IActionResult OnGet()
    {
        return Redirect("/Legal#privacy");
    }
}

