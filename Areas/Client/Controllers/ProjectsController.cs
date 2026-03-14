using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WEB_Sentro.Areas.Client.Controllers
{
    [Area("Client")]
    [Authorize]
    public class ProjectsController : Controller
    {
        public IActionResult Map()
        {
            ViewData["Title"] = "Geolocation Hub";
            return View();
        }

        public IActionResult Weather()
        {
            ViewData["Title"] = "Site Weather";
            return View();
        }
    }
}
