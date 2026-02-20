using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using WEB_Sentro.Models;

namespace WEB_Sentro.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                if (User.IsInRole("SuperAdmin")) return LocalRedirect(Url.Content("~/Vendor/Dashboard"));
                if (User.IsInRole("Admin")) return LocalRedirect(Url.Content("~/Client/Dashboard"));
                if (User.IsInRole("Employee")) return LocalRedirect(Url.Content("~/Client/MyWork"));
                return LocalRedirect(Url.Content("~/Client/Dashboard"));
            }
            return View();
        }
        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
