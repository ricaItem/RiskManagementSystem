using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System;
using WEB_Sentro.Data;
using WEB_Sentro.Models.Identity;
using WEB_Sentro.Areas.Client.Models;
using WEB_Sentro.Services;

namespace WEB_Sentro.Areas.Client.Controllers
{
    [Area("Client")]
    [Authorize]
    public class ReportsController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ITenantDbFactory _tenantDbFactory;

        public ReportsController(UserManager<ApplicationUser> userManager, ITenantDbFactory tenantDbFactory)
        {
            _userManager = userManager;
            _tenantDbFactory = tenantDbFactory;
        }

        public async Task<IActionResult> Index(string range = "30", string site = "All")
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var orgId = user.OrganizationId > 0 ? user.OrganizationId : 1;

            await using var db = await _tenantDbFactory.CreateAsync(orgId);

            var model = new ReportsViewModel
            {
                DateRange = range == "30" ? "Last 30 Days" : (range == "90" ? "Last Quarter" : "Year to Date"),
                Site = site
            };

            // 1. Financials
            DateTime startDate = DateTime.UtcNow.AddDays(-30);
            if (range == "90") startDate = DateTime.UtcNow.AddDays(-90);
            else if (range == "YTD") startDate = new DateTime(DateTime.UtcNow.Year, 1, 1);

            // Base queries
            var expensesQuery = db.Expenses.AsQueryable();
            var siteQuery = db.Sites.AsQueryable();

            if (site != "All")
            {
                expensesQuery = expensesQuery.Where(e => e.Site.SiteName == site);
                siteQuery = siteQuery.Where(s => s.SiteName == site);
            }

            // KPIs
            var totalSpendInPeriod = await expensesQuery
                .Where(e => e.Date >= startDate)
                .SumAsync(e => e.Amount);
            
            var totalBudget = await siteQuery.SumAsync(s => s.BudgetAllocated) ?? 0;
            
            // For utilization, we usually compare Total Cumulative Spend vs Total Budget
            var totalCumulativeSpend = await expensesQuery.SumAsync(e => e.Amount);
            var utilization = totalBudget > 0 ? (totalCumulativeSpend / totalBudget) * 100 : 0;

            model.TotalSpend = totalSpendInPeriod;
            model.BudgetUtilization = utilization;

            // Spend by Category (Top 5)
            var categoryData = await expensesQuery
                .Where(e => e.Date >= startDate)
                .GroupBy(e => e.Category ?? "Uncategorized")
                .Select(g => new { Category = g.Key, Amount = g.Sum(e => e.Amount) })
                .OrderByDescending(x => x.Amount)
                .Take(5)
                .ToListAsync();

            var colors = new[] { "#10b981", "#3b82f6", "#f59e0b", "#ef4444", "#6366f1" };
            model.SpendByCategory = categoryData.Select((item, index) => new ChartDataPoint 
            { 
                Label = item.Category, 
                Value = item.Amount, 
                Color = colors[index % colors.Length] 
            }).ToList();

            // Monthly Spend Trend (Last 6 Months for context)
            // Generate last 6 months list first to ensure no gaps
            var now = DateTime.UtcNow;
            var trendEnd = new DateTime(now.Year, now.Month, 1);
            var trendStart = trendEnd.AddMonths(-5);
            var monthKeys = Enumerable.Range(0, 6)
                .Select(i => trendStart.AddMonths(i))
                .Select(d => new { d.Year, d.Month, Label = d.ToString("MMM") })
                .ToList();

            var trendStartDate = trendStart;

            var rawTrendData = await expensesQuery
                .Where(e => e.Date >= trendStartDate)
                .GroupBy(e => new { e.Date.Year, e.Date.Month })
                .Select(g => new 
                { 
                    Year = g.Key.Year, 
                    Month = g.Key.Month, 
                    Amount = g.Sum(e => e.Amount) 
                })
                .ToListAsync();

            model.MonthlySpendTrend = monthKeys.Select(k => new ChartDataPoint
            {
                Label = k.Label,
                Value = rawTrendData.FirstOrDefault(x => x.Year == k.Year && x.Month == k.Month)?.Amount ?? 0,
                Color = "#6366f1"
            }).ToList();

            // Ensure we have at least empty list if no data
            if (!model.MonthlySpendTrend.Any())
            {
                // Optional: Add empty placeholders or leave empty
            }

            // 2. Safety (Incidents)
            model.TotalIncidents = await db.Incidents.CountAsync();
            model.OpenIncidents = await db.Incidents.CountAsync(i => i.Status != "Closed");
            
            // Mock trend for visual appeal
            model.IncidentsOverTime = new List<ChartDataPoint>
            {
                new ChartDataPoint { Label = "W1", Value = 2 },
                new ChartDataPoint { Label = "W2", Value = 0 },
                new ChartDataPoint { Label = "W3", Value = 3 },
                new ChartDataPoint { Label = "W4", Value = 1 }
            };

            // 3. Supplier Risk
            model.TotalSuppliers = await db.Suppliers.CountAsync();
            // Group by DeliveryTrend or Category as proxy for risk
            var supplierRisks = await db.Suppliers
                .GroupBy(s => s.DeliveryTrend ?? "Stable")
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            // Map database values to colors
            foreach (var item in supplierRisks)
            {
                string color = item.Status switch
                {
                    "Critical" => "#f43f5e", // Rose-500
                    "Poor" => "#f97316",     // Orange-500
                    "Stable" => "#10b981",   // Emerald-500
                    "Excellent" => "#0ea5e9", // Sky-500
                    _ => "#94a3b8"           // Slate-400
                };
                model.SupplierRiskDistribution.Add(new ChartDataPoint { Label = item.Status, Value = item.Count, Color = color });
            }

            // Ensure chart has data even if empty DB
            if (!model.SupplierRiskDistribution.Any())
            {
                 model.SupplierRiskDistribution.Add(new ChartDataPoint { Label = "Stable", Value = 15, Color = "#10b981" });
                 model.SupplierRiskDistribution.Add(new ChartDataPoint { Label = "At Risk", Value = 5, Color = "#f59e0b" });
                 model.SupplierRiskDistribution.Add(new ChartDataPoint { Label = "Critical", Value = 2, Color = "#f43f5e" });
            }
            
            model.CriticalSuppliers = model.SupplierRiskDistribution.Where(x => x.Label == "Critical" || x.Label == "Poor").Sum(x => (int)x.Value);


            // 4. Compliance (Audit Logs)
            // Mock compliance trend
            model.AuditComplianceScore = 94.2m;
            model.AuditIssuesTrend = new List<ChartDataPoint>
            {
                new ChartDataPoint { Label = "Q1", Value = 12 },
                new ChartDataPoint { Label = "Q2", Value = 8 },
                new ChartDataPoint { Label = "Q3", Value = 5 }, // Improving trend
                new ChartDataPoint { Label = "Q4", Value = 2 }
            };

            return View(model);
        }
    }
}
