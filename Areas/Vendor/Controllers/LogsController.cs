using Microsoft.AspNetCore.Mvc;

namespace WEB_Sentro.Areas.Vendor.Controllers
{
    [Area("Vendor")]
    public class LogsController : Controller
    {
            public IActionResult Index()
            {
                return View();
            }
        
    }
}