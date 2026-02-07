using Microsoft.AspNetCore.Mvc;

namespace YourProjectName.Areas.Vendor.Controllers
{
    [Area("Vendor")]
    public class OrganizationsController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}