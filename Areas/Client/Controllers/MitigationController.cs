using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Web_Sentro.Areas.Client.Controllers
{
    [Area("Client")]
    public class MitigationController : Controller
    {
        // GET: Client/Mitigation
        public IActionResult Index()
        {
            // Mock Data: Tasks linked to specific Risks
            var tasks = new List<MitigationTaskViewModel>
            {
                new MitigationTaskViewModel {
                    Id = "T-501",
                    RiskId = "R-1024",
                    Title = "Install secondary guardrails",
                    Assignee = "Mike T.",
                    DueDate = DateTime.Now.AddDays(2),
                    Status = "ToDo",
                    Priority = "High"
                },
                new MitigationTaskViewModel {
                    Id = "T-502",
                    RiskId = "R-1025",
                    Title = "Source alternative lumber supplier",
                    Assignee = "Sarah J.",
                    DueDate = DateTime.Now.AddDays(5),
                    Status = "InProgress",
                    Priority = "Medium"
                },
                new MitigationTaskViewModel {
                    Id = "T-503",
                    RiskId = "R-1024",
                    Title = "Conduct soil stability test",
                    Assignee = "Eng. Davila",
                    DueDate = DateTime.Now.AddDays(-1),
                    Status = "Review",
                    Priority = "Critical"
                },
                new MitigationTaskViewModel {
                    Id = "T-504",
                    RiskId = "R-1026",
                    Title = "Update insurance policy",
                    Assignee = "Legal Dept",
                    DueDate = DateTime.Now.AddDays(10),
                    Status = "Done",
                    Priority = "Low"
                }
            };

            return View(tasks);
        }

        // POST: Client/Mitigation/UpdateStatus (Called via AJAX when dragging)
        [HttpPost]
        public IActionResult UpdateStatus(string taskId, string newStatus)
        {
            // Logic to update DB would go here
            return Json(new { success = true });
        }
    }

    public class MitigationTaskViewModel
    {
        public string Id { get; set; }
        public string RiskId { get; set; } // Links back to Risk Registry
        public string Title { get; set; }
        public string Assignee { get; set; }
        public DateTime DueDate { get; set; }
        public string Status { get; set; } // ToDo, InProgress, Review, Done
        public string Priority { get; set; } // Critical, High, Medium, Low
    }
}