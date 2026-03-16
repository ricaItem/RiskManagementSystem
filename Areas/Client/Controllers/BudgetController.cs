using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WEB_Sentro.Models.Identity;
using WEB_Sentro.Services;
using System.Globalization;
using WEB_Sentro.Areas.Client.Models;

namespace Web_Sentro.Areas.Client.Controllers
{
    [Area("Client")]
    [Authorize(Policy = "MainAdminOnly")]
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

            // Fetch Committed Costs (POs and COs)
            var poAmounts = await db.PurchaseOrders.AsNoTracking()
                .Where(p => p.OrgId == orgId.Value && p.Status != "Draft" && p.Status != "Cancelled")
                .SelectMany(p => p.LineItems)
                .GroupBy(l => l.PurchaseOrder.SiteId)
                .Select(g => new { SiteId = g.Key, Amount = g.Sum(l => l.Quantity * l.UnitCost) })
                .ToListAsync();

            var coAmounts = await db.ChangeOrders.AsNoTracking()
                .Where(c => c.OrgId == orgId.Value && c.Status == "Approved")
                .SelectMany(c => c.LineItems)
                .GroupBy(l => l.ChangeOrder.SiteId)
                .Select(g => new { SiteId = g.Key, Amount = g.Sum(l => l.Amount) })
                .ToListAsync();

            // KPI Calculations
            var totalAllocated = sites.Sum(s => s.BudgetAllocated ?? 0);
            var totalActual = expenses.Sum(e => e.Amount);
            var totalCommitted = poAmounts.Sum(p => p.Amount) + coAmounts.Sum(c => c.Amount);
            var remaining = totalAllocated - totalCommitted; // Remaining Budget = Budget - Committed (Committed includes Actuals usually, but here let's assume Committed = POs + COs, and Actuals are just payments. Wait. If I issue a PO for 10k, Committed is 10k. If I pay 5k, Actual is 5k. Remaining to Spend on PO is 5k. Remaining Budget is still Budget - 10k. So Budget - Committed is correct.)
            var utilization = totalAllocated > 0 ? (double)(totalCommitted / totalAllocated) * 100 : 0;

            // Monthly Trend (Last 6 Months) - Keep based on Expenses (Cash Flow)
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
                var siteActual = siteExpenses.Sum(e => e.Amount);
                var sitePO = poAmounts.FirstOrDefault(p => p.SiteId == s.SiteId)?.Amount ?? 0;
                var siteCO = coAmounts.FirstOrDefault(c => c.SiteId == s.SiteId)?.Amount ?? 0;
                var siteCommitted = sitePO + siteCO;
                var siteBudget = s.BudgetAllocated ?? 0;
                
                siteList.Add(new BudgetSiteViewModel
                {
                    SiteId = s.SiteId,
                    SiteName = s.SiteName,
                    SiteCode = s.SiteCode,
                    BudgetAllocated = siteBudget,
                    TotalActual = siteActual,
                    TotalCommitted = siteCommitted,
                    UtilizationPercent = siteBudget > 0 ? (double)(siteCommitted / siteBudget) * 100 : (double?)null,
                    RemainingBudget = siteBudget - siteCommitted,
                    CategoryBreakdown = siteExpenses
                        .GroupBy(e => e.Category)
                        .ToDictionary(g => g.Key ?? "Other", g => g.Sum(e => e.Amount))
                });
            }

            var model = new BudgetDashboardViewModel
            {
                TotalAllocated = totalAllocated,
                TotalActual = totalActual,
                TotalCommitted = totalCommitted,
                RemainingBudget = remaining,
                OverallUtilization = utilization,
                Sites = siteList,
                TrendLabels = trendLabels,
                TrendData = trendData
            };

            return View(model);
        }

        public async Task<IActionResult> Details(int id)
        {
            var orgId = IsSuperAdmin() ? null : await GetMyOrgIdAsync();
            if (!orgId.HasValue) return RedirectToAction(nameof(Index));

            await using var db = await _tenantDbFactory.CreateAsync(orgId.Value);

            var site = await db.Sites
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.SiteId == id && s.OrgId == orgId.Value);

            if (site == null) return NotFound();

            // 1. Get Cost Codes lookup
            var costCodes = await db.CostCodes.AsNoTracking()
                .Where(c => c.OrgId == orgId.Value)
                .ToDictionaryAsync(c => c.CostCodeId, c => new { c.Code, c.Description });

            // 2. Get Expenses for this Site
            var expenses = await db.Expenses.AsNoTracking()
                .Where(e => e.SiteId == id && e.OrgId == orgId.Value)
                .ToListAsync();

            // 3. Get PO Lines (Committed)
            // Filter POs: != Draft and != Cancelled
            var poLines = await db.PurchaseOrders.AsNoTracking()
                .Where(po => po.SiteId == id && po.OrgId == orgId.Value && po.Status != "Draft" && po.Status != "Cancelled")
                .SelectMany(po => po.LineItems)
                .Select(l => new { l.Quantity, l.UnitCost, l.CostCodeId }) 
                .ToListAsync();

            // 4. Get CO Lines (Committed)
            // Filter COs: == Approved
            var coLines = await db.ChangeOrders.AsNoTracking()
                .Where(co => co.SiteId == id && co.OrgId == orgId.Value && co.Status == "Approved")
                .SelectMany(co => co.LineItems)
                .Select(l => new { l.Amount, l.CostCodeId })
                .ToListAsync();

            // Aggregation
            var breakdown = new List<BudgetLineItemViewModel>();
            
            // Get all unique CostCodeIds encountered
            var allCostCodeIds = expenses.Where(e => e.CostCodeId.HasValue).Select(e => e.CostCodeId.Value)
                .Union(poLines.Where(p => p.CostCodeId.HasValue).Select(p => p.CostCodeId.Value))
                .Union(coLines.Where(c => c.CostCodeId.HasValue).Select(c => c.CostCodeId.Value))
                .Distinct()
                .ToList();

            foreach (var ccId in allCostCodeIds)
            {
                var cc = costCodes.ContainsKey(ccId) ? costCodes[ccId] : null;
                var code = cc?.Code ?? "Uncoded";
                var desc = cc?.Description ?? "Unknown Cost Code";

                var actual = expenses.Where(e => e.CostCodeId == ccId).Sum(e => e.Amount);
                var committedPO = poLines.Where(l => l.CostCodeId == ccId).Sum(l => l.Quantity * l.UnitCost);
                var committedCO = coLines.Where(l => l.CostCodeId == ccId).Sum(l => l.Amount);
                var totalCommitted = committedPO + committedCO;

                breakdown.Add(new BudgetLineItemViewModel
                {
                    CostCode = code,
                    Description = desc,
                    ActualAmount = actual,
                    CommittedAmount = totalCommitted,
                    TotalAmount = Math.Max(actual, totalCommitted)
                });
            }

            // Also handle "Uncoded" items (CostCodeId == null)
            var actualUncoded = expenses.Where(e => e.CostCodeId == null).Sum(e => e.Amount);
            var committedPOUncoded = poLines.Where(l => l.CostCodeId == null).Sum(l => l.Quantity * l.UnitCost);
            var committedCOUncoded = coLines.Where(l => l.CostCodeId == null).Sum(l => l.Amount);
            
            if (actualUncoded > 0 || committedPOUncoded > 0 || committedCOUncoded > 0)
            {
                 breakdown.Add(new BudgetLineItemViewModel
                {
                    CostCode = "None",
                    Description = "Uncategorized",
                    ActualAmount = actualUncoded,
                    CommittedAmount = committedPOUncoded + committedCOUncoded,
                    TotalAmount = Math.Max(actualUncoded, committedPOUncoded + committedCOUncoded)
                });
            }

            var model = new SiteBudgetDetailsViewModel
            {
                SiteId = site.SiteId,
                SiteName = site.SiteName,
                TotalBudget = site.BudgetAllocated ?? 0,
                TotalActual = breakdown.Sum(b => b.ActualAmount),
                TotalCommitted = breakdown.Sum(b => b.CommittedAmount),
                BudgetVariance = (site.BudgetAllocated ?? 0) - breakdown.Sum(b => b.CommittedAmount),
                Items = breakdown.OrderBy(b => b.CostCode).ToList()
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
        public decimal TotalActual { get; set; }
        public decimal TotalCommitted { get; set; }
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
        public decimal TotalActual { get; set; }
        public decimal TotalCommitted { get; set; }
        public decimal RemainingBudget { get; set; }
        public double? UtilizationPercent { get; set; }
        public Dictionary<string, decimal> CategoryBreakdown { get; set; } = new();
    }
}
