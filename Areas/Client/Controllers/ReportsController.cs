using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;

namespace Web_Sentro.Areas.Client.Controllers
{
    [Area("Client")]
    public class ReportsController : Controller
    {
        // GET: Client/Reports/RiskPerformance
        public IActionResult RiskPerformance()
        {
            // Mock Data: Comparing "Before" (Inherent) vs "After" (Residual)
            var reportData = new List<RiskPerformanceViewModel>
            {
                new RiskPerformanceViewModel {
                    RiskTitle = "Crane Stability on Soft Soil",
                    InherentScore = 20, // Critical (4x5)
                    ResidualScore = 8,  // Reduced to Medium (2x4) after mitigation
                    Status = "Improved"
                },
                new RiskPerformanceViewModel {
                    RiskTitle = "Steel Shipment Delay",
                    InherentScore = 9,  // Medium (3x3)
                    ResidualScore = 9,  // No change yet
                    Status = "Stagnant"
                },
                new RiskPerformanceViewModel {
                    RiskTitle = "Regulatory Permit Expiry",
                    InherentScore = 25, // Extreme (5x5)
                    ResidualScore = 5,  // Low (1x5) - Crisis Averted
                    Status = "Resolved"
                }
            };

            return View(reportData);
        }
    }

    public class RiskPerformanceViewModel
    {
        public string RiskTitle { get; set; }
        public int InherentScore { get; set; } // Score BEFORE mitigation
        public int ResidualScore { get; set; } // Score AFTER mitigation
        public string Status { get; set; }

        // Helper for calculating reduction %
        public int ReductionPercent => InherentScore == 0 ? 0 :
            (int)((double)(InherentScore - ResidualScore) / InherentScore * 100);
    }
}