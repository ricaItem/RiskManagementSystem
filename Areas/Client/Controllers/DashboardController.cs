using Microsoft.AspNetCore.Mvc;
namespace WEB_Sentro.Areas.Client.Controllers
{
    [Area("Client")]
    public class DashboardController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
