using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Web_Sentro.Areas.Client.Controllers
{
    [Area("Client")]
    public class RisksController : Controller
    {
        public IActionResult Index(string filterStatus = "All", string search = "")
        {
            var risks = new List<RiskViewModel>
            {
                new RiskViewModel {
                    Id = "R-1024",
                    Title = "Crane Stability on Soft Soil",
                    Reporter = "Sarah Field (Site Mgr)",
                    Project = "Sky Tower",
                    Category = "Safety",
                    Probability = 4,
                    Impact = 4,
                    Status = "Open",
                    DateIdentified = DateTime.Now.AddDays(-2)
                },
                new RiskViewModel {
                    Id = "R-1025",
                    Title = "Steel Shipment Delay",
                    Reporter = "John Doe (Procurement)",
                    Project = "River Bridge",
                    Category = "Operational",
                    Probability = 3,
                    Impact = 3,
                    Status = "Mitigating",
                    DateIdentified = DateTime.Now.AddDays(-5)
                },
                new RiskViewModel {
                    Id = "R-1026",
                    Title = "Regulatory Permit Expiry",
                    Reporter = "Legal Team",
                    Project = "Sky Tower",
                    Category = "Compliance",
                    Probability = 5,
                    Impact = 5,
                    Status = "Critical",
                    DateIdentified = DateTime.Now.AddHours(-4)
                },
                new RiskViewModel {
                    Id = "R-1027",
                    Title = "Budget Overrun - Concrete",
                    Reporter = "Finance Dept",
                    Project = "Downtown Hub",
                    Category = "Financial",
                    Probability = 2,
                    Impact = 4,
                    Status = "Review",
                    DateIdentified = DateTime.Now.AddDays(-10)
                }
            };

            if (filterStatus != "All")
            {
                risks = risks.Where(r => r.Status == filterStatus).ToList();
            }

            if (!string.IsNullOrEmpty(search))
            {
                risks = risks.Where(r => r.Title.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            ViewData["CurrentFilter"] = filterStatus;
            return View(risks);
        }

        public IActionResult Create()
        {
            return View();
        }

        // GET: /Client/Risks/Assess/{id}
        public IActionResult Assess(string id)
        {
            // Mock Data: In a real app, fetch from DB using 'id'
            var risk = new RiskViewModel
            {
                Id = id,
                Title = "Crane Stability on Soft Soil",
                Description = "Heavy rains have softened the ground near the north foundation.",
                Project = "Sky Tower",
                Category = "Safety",
                Probability = 0, // Not assessed yet
                Impact = 0,
                Status = "Open",
                DateIdentified = DateTime.UtcNow.AddDays(-2)
            };

            return View(risk);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
            public IActionResult Assess(RiskViewModel model)
        {
            if (!ModelState.IsValid)
            {
                // Return same view with validation errors
                return View(model);
            }

            // Logic to save the new Score (Probability * Impact) to the database would go here.
            // Example:
            // var score = model.Probability * model.Impact;
            // ... persist model/score ...

            return RedirectToAction(nameof(Index));
        }
    }

    public class RiskViewModel
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Reporter { get; set; }
        public string Project { get; set; }
        public string Category { get; set; }
        public int Probability { get; set; }
        public int Impact { get; set; }
        public string Status { get; set; }
        public DateTime? DateIdentified { get; set; }
        public string Description { get; set; }

        public int RiskScore => Probability * Impact;

        public string RiskColorClass => RiskScore >= 15 ? "text-rose-600 bg-rose-100 border-rose-200" :
                                        RiskScore >= 8 ? "text-amber-600 bg-amber-100 border-amber-200" :
                                        "text-emerald-600 bg-emerald-100 border-emerald-200";

        public string RiskLabel => RiskScore >= 15 ? "HIGH" : RiskScore >= 8 ? "MED" : "LOW";
    }
}