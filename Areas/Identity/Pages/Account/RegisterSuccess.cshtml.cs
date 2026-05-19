using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace WEB_Sentro.Areas.Identity.Pages.Account;

public class RegisterSuccessModel : PageModel
{
    public string? Email { get; set; }
    public string? Plan { get; set; }
    public string? Amount { get; set; }

    public void OnGet(string? email, string? plan, string? amount)
    {
        Email = email;
        Plan = plan;
        Amount = amount ?? "—";
    }
}
