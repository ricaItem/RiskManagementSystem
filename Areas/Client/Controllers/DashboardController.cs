using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace WEB_Sentro.Areas.Client.Controllers
{
    [Area("Client")]
    [Authorize]
    public class DashboardController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        public async Task<IActionResult> DashboardContent()
        {
            // Simulate delay for skeleton demonstration
            await Task.Delay(1500);
            return PartialView("_DashboardContent");
        }
    }
}
