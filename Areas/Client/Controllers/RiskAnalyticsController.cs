using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WEB_Sentro.Areas.Client.Models;

namespace WEB_Sentro.Areas.Client.Controllers
{
    [Area("Client")]
    [Authorize]
    public class RiskAnalyticsController : Controller
    {
        public IActionResult Index()
        {
            var model = new RiskAnalyticsViewModel
            {
                LastUpdatedHumanized = "Just now"
            };
            return View(model);
        }
    }
}
