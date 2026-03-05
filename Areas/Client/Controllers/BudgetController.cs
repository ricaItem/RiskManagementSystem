using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WEB_Sentro.Models.Identity;
using WEB_Sentro.Services;
using System.Globalization;

namespace Web_Sentro.Areas.Client.Controllers
{
    [Area("Client")]
    [Authorize]
    public class BudgetController : Controller
    {
        private readonly ITenantDbFactory _tenantDbFactory;
        private readonly UserManager<ApplicationUser> _userManager;

        public BudgetController(ITenantDbFactory tenantDbFactory, UserManager<ApplicationUser> userManager)
        {
            _tenantDbFactory = tenantDbFactory;
            _userManager = userManager;
        }

        private async Task<int?> GetMyOrgIdAsync()
        {
            var me = await _userManager.GetUserAsync(User);
            return me?.OrganizationId;
        }

        private bool IsSuperAdmin() => User.IsInRole("SuperAdmin");

        public async Task<IActionResult> Index()
        {
            ViewData["Title"] = "Budget Management";
            var orgId = IsSuperAdmin() ? null : await GetMyOrgIdAsync();
            if (!orgId.HasValue)
                return View(new BudgetDashboardViewModel());

            await using var db = await _tenantDbFactory.CreateAsync(orgId.Value);

            // Fetch Data
            var sites = await db.Sites.AsNoTracking()
                .Where(s => s.OrgId == orgId.Value && s.Status != "Archived")
                .OrderBy(s => s.SiteName)
                .Select(s => new { s.SiteId, s.SiteName, s.SiteCode, s.BudgetAllocated })
                .ToListAsync();

            var expenses = await db.Expenses.AsNoTracking()
                .Where(e => e.OrgId == orgId.Value)
                .Select(e => new { e.SiteId, e.Amount, e.Date, e.Category })
                .ToListAsync();

            // KPI Calculations
            var totalAllocated = sites.Sum(s => s.BudgetAllocated ?? 0);
            var totalSpent = expenses.Sum(e => e.Amount);
            var remaining = totalAllocated - totalSpent;
            var utilization = totalAllocated > 0 ? (double)(totalSpent / totalAllocated) * 100 : 0;

            // Monthly Trend (Last 6 Months)
            var trendData = new List<decimal>();
            var trendLabels = new List<string>();
            var today = DateTime.UtcNow;
            for (int i = 5; i >= 0; i--)
            {
                var month = today.AddMonths(-i);
                var monthStart = new DateTime(month.Year, month.Month, 1);
                var monthEnd = monthStart.AddMonths(1).AddDays(-1);
                
                var monthlyTotal = expenses
                    .Where(e => e.Date >= monthStart && e.Date <= monthEnd)
                    .Sum(e => e.Amount);
                
                trendData.Add(monthlyTotal);
                trendLabels.Add(month.ToString("MMM"));
            }

            // Site List
            var siteList = new List<BudgetSiteViewModel>();
            foreach (var s in sites)
            {
                var siteExpenses = expenses.Where(e => e.SiteId == s.SiteId).ToList();
                var siteTotal = siteExpenses.Sum(e => e.Amount);
                var siteBudget = s.BudgetAllocated ?? 0;
                
                siteList.Add(new BudgetSiteViewModel
                {
                    SiteId = s.SiteId,
                    SiteName = s.SiteName,
                    SiteCode = s.SiteCode,
                    BudgetAllocated = siteBudget,
                    TotalExpenses = siteTotal,
                    UtilizationPercent = siteBudget > 0 ? (double)(siteTotal / siteBudget) * 100 : (double?)null,
                    RemainingBudget = siteBudget - siteTotal,
                    CategoryBreakdown = siteExpenses
                        .GroupBy(e => e.Category)
                        .ToDictionary(g => g.Key ?? "Other", g => g.Sum(e => e.Amount))
                });
            }

            var model = new BudgetDashboardViewModel
            {
                TotalAllocated = totalAllocated,
                TotalSpent = totalSpent,
                RemainingBudget = remaining,
                OverallUtilization = utilization,
                Sites = siteList,
                TrendLabels = trendLabels,
                TrendData = trendData
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateBudget(int siteId, decimal amount)
        {
            var orgId = IsSuperAdmin() ? null : await GetMyOrgIdAsync();
            if (!orgId.HasValue) return RedirectToAction(nameof(Index));

            await using var db = await _tenantDbFactory.CreateAsync(orgId.Value);
            var site = await db.Sites.FirstOrDefaultAsync(s => s.SiteId == siteId && s.OrgId == orgId.Value);
            
            if (site != null)
            {
                site.BudgetAllocated = amount;
                site.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
                TempData["Message"] = $"Budget for {site.SiteName} updated.";
            }
            
            return RedirectToAction(nameof(Index));
        }
    }

    public class BudgetDashboardViewModel
    {
        public decimal TotalAllocated { get; set; }
        public decimal TotalSpent { get; set; }
        public decimal RemainingBudget { get; set; }
        public double OverallUtilization { get; set; }
        public List<BudgetSiteViewModel> Sites { get; set; } = new();
        public List<string> TrendLabels { get; set; } = new();
        public List<decimal> TrendData { get; set; } = new();
    }

    public class BudgetSiteViewModel
    {
        public int SiteId { get; set; }
        public string SiteName { get; set; } = null!;
        public string SiteCode { get; set; } = null!;
        public decimal BudgetAllocated { get; set; }
        public decimal TotalExpenses { get; set; }
        public decimal RemainingBudget { get; set; }
        public double? UtilizationPercent { get; set; }
        public Dictionary<string, decimal> CategoryBreakdown { get; set; } = new();
    }
}
