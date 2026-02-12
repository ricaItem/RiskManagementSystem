using Microsoft.AspNetCore.Mvc;
using Web_Sentro.Areas.Client.Models;

namespace Web_Sentro.Areas.Client.Controllers
{
    [Area("Client")]
    public class RisksController : Controller
    {
        public IActionResult Identification()
        {
            ViewData["Title"] = "Risk Identification";

            // Mock Data for UI Testing
            var risks = new List<RiskIdentificationViewModel>
            {
                new RiskIdentificationViewModel {
                    Id = 1, Title = "Tower Crane Fatigue", Category = "Operational",
                    Priority = "Critical", DetectedBy = "J. Dela Cruz", ProjectSite = "North Wing"
                },
                new RiskIdentificationViewModel {
                    Id = 2, Title = "Steel Price Volatility", Category = "Financial",
                    Priority = "Medium", DetectedBy = "A. Santos", ProjectSite = "Global"
                },
                new RiskIdentificationViewModel {
                    Id = 3, Title = "Foundation Seepage", Category = "Operational",
                    Priority = "Critical", DetectedBy = "M. Rivera", ProjectSite = "Basement Level B2"
                },
                new RiskIdentificationViewModel {
                    Id = 4, Title = "Scaffolding Stability", Category = "No Category",
                    Priority = "Critical", DetectedBy = "R. Gomez", ProjectSite = "West Tower Facade"
                },
                new RiskIdentificationViewModel {
                    Id = 5, Title = "Supply Chain Delay (Cement)", Category = "Operational",
                    Priority = "Medium", DetectedBy = "L. Tan", ProjectSite = "Logistics Hub"
                },
                new RiskIdentificationViewModel {
                    Id = 6, Title = "Labor Shortage (Welding)", Category = "No Category",
                    Priority = "Medium", DetectedBy = "K. Sy", ProjectSite = "Structural Phase 3"
                },
                new RiskIdentificationViewModel {
                    Id = 7, Title = "Unprotected Floor Openings", Category = "Safety",
                    Priority = "Critical", DetectedBy = "J. Reyes", ProjectSite = "East Wing Floor 12"
                },
                new RiskIdentificationViewModel {
                    Id = 8, Title = "Currency Exchange Flux", Category = "Financial",
                    Priority = "Low", DetectedBy = "C. Puno", ProjectSite = "Procurement"
                },
                new RiskIdentificationViewModel {
                    Id = 9, Title = "Inadequate Site Lighting", Category = "No Category",
                    Priority = "Medium", DetectedBy = "D. Lim", ProjectSite = "Parking Annex"
                },
                new RiskIdentificationViewModel {
                    Id = 10, Title = "Utility Line Encroachment", Category = "Operational",
                    Priority = "Critical", DetectedBy = "S. Bautista", ProjectSite = "South Site Boundary"
                }
            };

            return View(risks);
        }

        [HttpPost]
        public IActionResult IdentifyNewRisk(RiskIdentificationViewModel model)
        {
            // Logic: Save to Database here
            // _context.Risks.Add(model);
            // _context.SaveChanges();
            return RedirectToAction("Identification");
        }
        [HttpGet]
        public IActionResult Assess(int id)
        {
            ViewData["Title"] = "Risk Assessment";

            // In production: Fetch the specific risk by ID
            var model = new RiskAssessmentViewModel
            {
                RiskId = id,
                RiskTitle = "Tower Crane Mechanical Fatigue", // Example
                Likelihood = 1,
                Impact = 1
            };

            return View(model);
        }

        [HttpPost]
        public IActionResult SaveAssessment(RiskAssessmentViewModel model)
        {
            // Logic: If Score > 15, automatically flag for Mitigation Board
            if (model.RiskScore >= 15)
            {
                // Code to push to Mitigation Board
            }

            // Save to DB logic here...
            return RedirectToAction("Identification");
        }

        [HttpGet]
        public IActionResult Monitoring()
        {
            ViewData["Title"] = "Risk Monitoring Hub";

            var model = new RiskMonitoringViewModel
            {
                ProjectName = "Sentro Tower - Davao",
                Latitude = 7.0707,
                Longitude = 125.6083,
                Temperature = 31,
                WeatherCondition = "Thunderstorm Warning",
                WindSpeed = 45.5, // km/h
                ActiveRisksCount = 12,
                HighPriorityRisks = new List<RiskIdentificationViewModel>() // Populate from DB
            };


            model.HighPriorityRisks = new List<RiskIdentificationViewModel>
    {
        new RiskIdentificationViewModel {
            Title = "Tower Crane Fatigue", ProjectSite = "North Wing",
            Category = "Operational", Priority = "Critical", DetectedBy = "J. Dela Cruz"
        },
        new RiskIdentificationViewModel {
            Title = "Steel Price Volatility", ProjectSite = "Global",
            Category = "Financial", Priority = "Medium", DetectedBy = "System Auth"
        }
    };

            return View(model);
        }


    }
}