using Microsoft.AspNetCore.Mvc;
using Web_Sentro.Areas.Client.Models;

namespace Web_Sentro.Areas.Client.Controllers
{
    [Area("Client")]
    public class MitigationController : Controller
    {
        public IActionResult Index()
        {
            ViewData["Title"] = "Mitigation Board";

            // Mock Data - In production, fetch from _context.MitigationTasks
            var tasks = new List<MitigationTaskViewModel>
            {
                new MitigationTaskViewModel { Id = "T1", Title = "Install Safety Netting", Status = "ToDo", Priority = "Critical", AssignedTo = "Site Team A" },
                new MitigationTaskViewModel { Id = "T2", Title = "Adjust Steel Procurement", Status = "InProgress", Priority = "High", AssignedTo = "Procurement" },
                new MitigationTaskViewModel { Id = "T3", Title = "Crane Inspection", Status = "Review", Priority = "Critical", AssignedTo = "Safety Officer" }
            };

            return View(tasks);
        }
    }
}