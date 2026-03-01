using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WEB_Sentro.Models.Identity;
using WEB_Sentro.Services;

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
            ViewData["Title"] = "Budget by Site";
            var orgId = IsSuperAdmin() ? null : await GetMyOrgIdAsync();
            if (!orgId.HasValue)
                return View(new List<BudgetSiteViewModel>());

            await using var db = await _tenantDbFactory.CreateAsync(orgId.Value);
            var sites = await db.Sites.AsNoTracking().Where(s => s.OrgId == orgId.Value && s.Status != "Archived").OrderBy(s => s.SiteName).Select(s => new { s.SiteId, s.SiteName, s.SiteCode, s.BudgetAllocated }).ToListAsync();
            var expenseTotals = await db.Expenses.Where(e => e.OrgId == orgId.Value).GroupBy(e => e.SiteId).Select(g => new { SiteId = g.Key, Total = g.Sum(e => e.Amount) }).ToListAsync();

            var list = new List<BudgetSiteViewModel>();
            foreach (var s in sites)
            {
                var total = expenseTotals.FirstOrDefault(x => x.SiteId == s.SiteId)?.Total ?? 0;
                var budget = s.BudgetAllocated ?? 0;
                var utilization = budget > 0 ? (double)(total / budget) * 100 : (double?)null;
                list.Add(new BudgetSiteViewModel
                {
                    SiteId = s.SiteId,
                    SiteName = s.SiteName,
                    SiteCode = s.SiteCode,
                    BudgetAllocated = budget,
                    TotalExpenses = total,
                    UtilizationPercent = utilization
                });
            }
            return View(list);
        }
    }

    public class BudgetSiteViewModel
    {
        public int SiteId { get; set; }
        public string SiteName { get; set; } = null!;
        public string SiteCode { get; set; } = null!;
        public decimal BudgetAllocated { get; set; }
        public decimal TotalExpenses { get; set; }
        public double? UtilizationPercent { get; set; }
    }
}
