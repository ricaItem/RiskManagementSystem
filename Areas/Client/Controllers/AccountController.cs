using Microsoft.AspNetCore.Mvc;

namespace WEB_Sentro.Areas.Client.Controllers
{
    [Area("Client")]
    public class AccountController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
