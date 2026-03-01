using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WEB_Sentro.Data.Entities;
using WEB_Sentro.Models.Identity;
using WEB_Sentro.Services;
using Web_Sentro.Areas.Client.Models;

namespace Web_Sentro.Areas.Client.Controllers
{
    [Area("Client")]
    [Authorize]
    public class ExpensesController : Controller
    {
        private readonly ITenantDbFactory _tenantDbFactory;
        private readonly UserManager<ApplicationUser> _userManager;

        public ExpensesController(ITenantDbFactory tenantDbFactory, UserManager<ApplicationUser> userManager)
        {
            _tenantDbFactory = tenantDbFactory;
            _userManager = userManager;
        }

        private async Task<ApplicationUser?> GetCurrentUserAsync() => await _userManager.GetUserAsync(User);
        private async Task<int?> GetMyOrgIdAsync()
        {
            var me = await GetCurrentUserAsync();
            return me?.OrganizationId;
        }
        private bool IsSuperAdmin() => User.IsInRole("SuperAdmin");

        public async Task<IActionResult> Index(int? siteId, string? category, int page = 1, int pageSize = 10)
        {
            ViewData["Title"] = "Expenses";
            var user = await GetCurrentUserAsync();
            if (user == null) return Challenge();

            var orgId = IsSuperAdmin() ? null : await GetMyOrgIdAsync();
            if (!orgId.HasValue)
                return View(new PagedResult<Expense> { Items = new List<Expense>(), TotalCount = 0, PageNumber = 1, PageSize = pageSize });

            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 5, 50);

            await using var db = await _tenantDbFactory.CreateAsync(orgId.Value);

            var sites = await db.Sites.AsNoTracking().Where(s => s.OrgId == orgId.Value && s.Status != "Archived").OrderBy(s => s.SiteName).ToListAsync();
            ViewBag.Sites = sites;
            var siteFilterList = new List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem> { new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = "", Text = "All Sites" } };
            foreach (var s in sites)
                siteFilterList.Add(new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = s.SiteId.ToString(), Text = $"{s.SiteName} ({s.SiteCode})", Selected = siteId == s.SiteId });
            ViewBag.SiteFilterList = siteFilterList;

            var query = db.Expenses.AsNoTracking().Include(e => e.Site).Include(e => e.PurchaseOrder).Include(e => e.Risk)
                .Where(e => e.OrgId == orgId.Value);

            if (siteId.HasValue) query = query.Where(e => e.SiteId == siteId.Value);
            if (!string.IsNullOrWhiteSpace(category)) query = query.Where(e => e.Category == category);

            var totalCount = await query.CountAsync();
            var items = await query.OrderByDescending(e => e.Date).ThenByDescending(e => e.ExpenseId).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            var model = new PagedResult<Expense> { Items = items, TotalCount = totalCount, PageNumber = page, PageSize = pageSize };
            ViewBag.SelectedSiteId = siteId;
            ViewBag.SelectedCategory = category;
            ViewBag.Categories = new[] { "Labor", "Materials", "Equipment", "Mitigation" };
            return View(model);
        }

        public async Task<IActionResult> Create()
        {
            ViewData["Title"] = "New Expense";
            var orgId = IsSuperAdmin() ? null : await GetMyOrgIdAsync();
            if (!orgId.HasValue) return RedirectToAction(nameof(Index));

            await using var db = await _tenantDbFactory.CreateAsync(orgId.Value);
            ViewBag.Sites = await db.Sites.AsNoTracking().Where(s => s.OrgId == orgId.Value && s.Status != "Archived").OrderBy(s => s.SiteName).ToListAsync();
            ViewBag.PurchaseOrders = await db.PurchaseOrders.AsNoTracking().Where(p => p.OrgId == orgId.Value).OrderByDescending(p => p.OrderDate).Select(p => new { p.PurchaseOrderId, p.OrderNumber }).ToListAsync();
            ViewBag.Risks = await db.Risks.AsNoTracking().Where(r => r.OrgId == orgId.Value && r.DeletedAt == null).OrderByDescending(r => r.CreatedAt).Select(r => new { r.RiskId, r.Title }).Take(200).ToListAsync();
            return View(new Expense { Date = DateTime.UtcNow.Date, Category = "Materials" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("SiteId,Amount,Category,Date,RiskId,PurchaseOrderId")] Expense model)
        {
            var orgId = IsSuperAdmin() ? null : await GetMyOrgIdAsync();
            if (!orgId.HasValue) return RedirectToAction(nameof(Index));

            if (model.Amount <= 0) { ModelState.AddModelError("Amount", "Amount must be greater than 0."); await PopulateExpenseDropdownsAsync(orgId.Value); return View(model); }

            await using var db = await _tenantDbFactory.CreateAsync(orgId.Value);
            var siteOk = await db.Sites.AnyAsync(s => s.SiteId == model.SiteId && s.OrgId == orgId.Value);
            if (!siteOk) { ModelState.AddModelError("SiteId", "Invalid site."); await PopulateExpenseDropdownsAsync(orgId.Value); return View(model); }

            if (model.RiskId.HasValue)
            {
                var riskOk = await db.Risks.AnyAsync(r => r.RiskId == model.RiskId.Value && r.OrgId == orgId.Value && r.DeletedAt == null);
                if (!riskOk) model.RiskId = null;
            }
            if (model.PurchaseOrderId.HasValue)
            {
                var poOk = await db.PurchaseOrders.AnyAsync(p => p.PurchaseOrderId == model.PurchaseOrderId.Value && p.OrgId == orgId.Value);
                if (!poOk) model.PurchaseOrderId = null;
            }

            var entity = new Expense
            {
                OrgId = orgId.Value,
                SiteId = model.SiteId,
                Amount = model.Amount,
                Category = model.Category?.Trim() ?? "Materials",
                Date = model.Date,
                RiskId = model.RiskId,
                PurchaseOrderId = model.PurchaseOrderId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.Expenses.Add(entity);
            await db.SaveChangesAsync();
            TempData["Message"] = "Expense recorded.";
            return RedirectToAction(nameof(Index));
        }

        private async Task PopulateExpenseDropdownsAsync(int orgId)
        {
            await using var db = await _tenantDbFactory.CreateAsync(orgId);
            ViewBag.Sites = await db.Sites.AsNoTracking().Where(s => s.OrgId == orgId && s.Status != "Archived").OrderBy(s => s.SiteName).ToListAsync();
            ViewBag.PurchaseOrders = await db.PurchaseOrders.AsNoTracking().Where(p => p.OrgId == orgId).OrderByDescending(p => p.OrderDate).Select(p => new { p.PurchaseOrderId, p.OrderNumber }).ToListAsync();
            ViewBag.Risks = await db.Risks.AsNoTracking().Where(r => r.OrgId == orgId && r.DeletedAt == null).OrderByDescending(r => r.CreatedAt).Select(r => new { r.RiskId, r.Title }).Take(200).ToListAsync();
        }
    }
}
