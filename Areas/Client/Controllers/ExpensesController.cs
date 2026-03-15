using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
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
        private readonly IWebHostEnvironment _env;
        private readonly IAuditService _auditService;

        public ExpensesController(ITenantDbFactory tenantDbFactory, UserManager<ApplicationUser> userManager, IWebHostEnvironment env, IAuditService auditService)
        {
            _tenantDbFactory = tenantDbFactory;
            _userManager = userManager;
            _env = env;
            _auditService = auditService;
        }

        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null && await _userManager.IsInRoleAsync(user, "Employee"))
            {
                context.Result = RedirectToAction("Index", "MyWork", new { area = "Client" });
                return;
            }

            await next();
        }

        private async Task<ApplicationUser?> GetCurrentUserAsync() => await _userManager.GetUserAsync(User);
        private async Task<int?> GetMyOrgIdAsync()
        {
            var me = await GetCurrentUserAsync();
            return me?.OrganizationId;
        }
        private bool IsSuperAdmin() => User.IsInRole("SuperAdmin");

        public async Task<IActionResult> Index(int? siteId, string? category, int? purchaseOrderId, int page = 1, int pageSize = 10)
        {
            ViewData["Title"] = "Expenses";
            var orgId = IsSuperAdmin() ? null : await GetMyOrgIdAsync();
            if (!orgId.HasValue)
                return View(new PagedResult<Expense> { Items = new List<Expense>(), TotalCount = 0, PageNumber = 1, PageSize = pageSize });

            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 5, 50);

            await using var db = await _tenantDbFactory.CreateAsync(orgId.Value);

            // Filters
            var sites = await db.Sites.AsNoTracking().Where(s => s.OrgId == orgId.Value && s.Status != "Archived").OrderBy(s => s.SiteName).ToListAsync();
            ViewBag.Sites = sites;
            ViewBag.SiteFilterList = new List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem> { new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = "", Text = "All Sites" } };
            foreach (var s in sites)
                ((List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem>)ViewBag.SiteFilterList).Add(new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = s.SiteId.ToString(), Text = $"{s.SiteName} ({s.SiteCode})", Selected = siteId == s.SiteId });

            var query = db.Expenses.AsNoTracking().Include(e => e.Site).Include(e => e.PurchaseOrder).Include(e => e.Risk).Include(e => e.CostCode)
                .Where(e => e.OrgId == orgId.Value);

            if (siteId.HasValue) query = query.Where(e => e.SiteId == siteId.Value);
            if (!string.IsNullOrWhiteSpace(category)) query = query.Where(e => e.Category == category);

            // Stats Logic (based on filters)
            var totalSpend = await query.SumAsync(e => e.Amount);
            var categoryStats = await query.GroupBy(e => e.Category)
                .Select(g => new { Category = g.Key, Total = g.Sum(e => e.Amount) })
                .OrderByDescending(x => x.Total)
                .FirstOrDefaultAsync();

            ViewBag.TotalSpend = totalSpend;
            ViewBag.TopCategory = categoryStats?.Category ?? "None";
            ViewBag.TopCategoryAmount = categoryStats?.Total ?? 0m;

            var totalCount = await query.CountAsync();
            var items = await query.OrderByDescending(e => e.Date).ThenByDescending(e => e.ExpenseId).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            var model = new PagedResult<Expense> { Items = items, TotalCount = totalCount, PageNumber = page, PageSize = pageSize };
            ViewBag.SelectedSiteId = siteId;
            ViewBag.SelectedCategory = category;
            // ViewBag.Categories = new[] { "Labor", "Materials", "Equipment", "Mitigation" };
            ViewBag.Categories = await db.Expenses.AsNoTracking()
                .Where(e => e.OrgId == orgId.Value && e.Category != null)
                .Select(e => e.Category)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();

            // Populate Dropdowns for Modal
            await PopulateExpenseDropdownsAsync(orgId.Value);

            // Pre-fill Logic for Modal (if coming from PO)
            if (purchaseOrderId.HasValue)
            {
                var po = await db.PurchaseOrders.Include(p => p.LineItems).FirstOrDefaultAsync(p => p.PurchaseOrderId == purchaseOrderId.Value && p.OrgId == orgId.Value);
                if (po != null)
                {
                    ViewBag.PreFillPOId = po.PurchaseOrderId;
                    ViewBag.PreFillSiteId = po.SiteId;
                    ViewBag.PreFillAmount = po.LineItems?.Sum(l => l.Quantity * l.UnitCost) ?? 0m;
                    ViewBag.PreFillDate = DateTime.UtcNow.ToString("yyyy-MM-dd");
                }
            }

            return View(model);
        }

        public async Task<IActionResult> Create(int? purchaseOrderId)
        {
            ViewData["Title"] = "New Expense";
            var orgId = IsSuperAdmin() ? null : await GetMyOrgIdAsync();
            if (!orgId.HasValue) return RedirectToAction(nameof(Index));

            await PopulateExpenseDropdownsAsync(orgId.Value);

            var model = new Expense { Date = DateTime.UtcNow.Date, Category = "Materials" };
            ViewBag.SelectedPoTotal = (decimal?)null;

            if (purchaseOrderId.HasValue)
            {
                await using var db = await _tenantDbFactory.CreateAsync(orgId.Value);
                var po = await db.PurchaseOrders.Include(p => p.LineItems).FirstOrDefaultAsync(p => p.PurchaseOrderId == purchaseOrderId.Value && p.OrgId == orgId.Value);
                if (po != null)
                {
                    model.PurchaseOrderId = po.PurchaseOrderId;
                    model.SiteId = po.SiteId;
                    var total = po.LineItems?.Sum(l => l.Quantity * l.UnitCost) ?? 0m;
                    ViewBag.SelectedPoTotal = total;
                    model.Amount = total;
                }
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("SiteId,Amount,Category,Date,RiskId,PurchaseOrderId,CostCodeId")] Expense model, IFormFile? attachment)
        {
            var orgId = IsSuperAdmin() ? null : await GetMyOrgIdAsync();
            if (!orgId.HasValue) return RedirectToAction(nameof(Index));

            if (model.Amount <= 0) 
            { 
                ModelState.AddModelError("Amount", "Amount must be greater than 0."); 
                await PopulateExpenseDropdownsAsync(orgId.Value); 
                SetSelectedPoTotalFromModel(model.PurchaseOrderId); 
                return View(model); 
            }

            await using var db = await _tenantDbFactory.CreateAsync(orgId.Value);
            var siteOk = await db.Sites.AnyAsync(s => s.SiteId == model.SiteId && s.OrgId == orgId.Value);
            if (!siteOk) 
            { 
                ModelState.AddModelError("SiteId", "Invalid site."); 
                await PopulateExpenseDropdownsAsync(orgId.Value); 
                SetSelectedPoTotalFromModel(model.PurchaseOrderId); 
                return View(model); 
            }

            // File Upload Logic
            string? attachmentPath = null;
            if (attachment != null && attachment.Length > 0)
            {
                var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "receipts");
                Directory.CreateDirectory(uploadsFolder);
                var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(attachment.FileName)}";
                var filePath = Path.Combine(uploadsFolder, fileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await attachment.CopyToAsync(stream);
                }
                attachmentPath = $"/uploads/receipts/{fileName}";
            }

            // Fetch CostCode to set Category for legacy purposes
            string category = model.Category?.Trim() ?? "Other";
            if (model.CostCodeId.HasValue)
            {
                var cc = await db.CostCodes.FindAsync(model.CostCodeId.Value);
                if (cc != null) category = $"{cc.Code} {cc.Description}";
            }

            var entity = new Expense
            {
                OrgId = orgId.Value,
                SiteId = model.SiteId,
                Amount = model.Amount,
                Category = category,
                CostCodeId = model.CostCodeId,
                Date = model.Date,
                RiskId = model.RiskId,
                PurchaseOrderId = model.PurchaseOrderId,
                AttachmentPath = attachmentPath,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.Expenses.Add(entity);
            await db.SaveChangesAsync();

            var user = await GetCurrentUserAsync();
            if (user != null)
            {
                await _auditService.LogAsync(
                    orgId.Value, 
                    user.Id, 
                    "Expense", 
                    entity.ExpenseId, 
                    "ExpenseCreated", 
                    $"Expense recorded: {entity.Category} - {entity.Amount:C}", 
                    "Info", 
                    HttpContext.Connection.RemoteIpAddress?.ToString()
                );
            }

            TempData["Message"] = "Expense recorded.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            ViewData["Title"] = "Edit Expense";
            var orgId = IsSuperAdmin() ? null : await GetMyOrgIdAsync();
            if (!orgId.HasValue) return RedirectToAction(nameof(Index));

            await using var db = await _tenantDbFactory.CreateAsync(orgId.Value);
            var expense = await db.Expenses.FirstOrDefaultAsync(e => e.ExpenseId == id && e.OrgId == orgId.Value);
            if (expense == null) return NotFound();

            await PopulateExpenseDropdownsAsync(orgId.Value);
            SetSelectedPoTotalFromModel(expense.PurchaseOrderId);

            return View(expense);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("ExpenseId,SiteId,Amount,Category,Date,RiskId,PurchaseOrderId,CostCodeId")] Expense model, IFormFile? attachment)
        {
            if (id != model.ExpenseId) return NotFound();
            var orgId = IsSuperAdmin() ? null : await GetMyOrgIdAsync();
            if (!orgId.HasValue) return RedirectToAction(nameof(Index));

            await using var db = await _tenantDbFactory.CreateAsync(orgId.Value);
            var entity = await db.Expenses.FirstOrDefaultAsync(e => e.ExpenseId == id && e.OrgId == orgId.Value);
            if (entity == null) return NotFound();

            if (model.Amount <= 0)
            {
                ModelState.AddModelError("Amount", "Amount must be greater than 0.");
                await PopulateExpenseDropdownsAsync(orgId.Value);
                SetSelectedPoTotalFromModel(model.PurchaseOrderId);
                return View(model);
            }

            // Update fields
            entity.SiteId = model.SiteId;
            entity.Amount = model.Amount;
            entity.Category = model.Category;
            entity.CostCodeId = model.CostCodeId;
            entity.Date = model.Date;
            entity.RiskId = model.RiskId;
            entity.PurchaseOrderId = model.PurchaseOrderId;
            entity.UpdatedAt = DateTime.UtcNow;

            // Fetch CostCode to set Category for legacy purposes
            if (model.CostCodeId.HasValue)
            {
                var cc = await db.CostCodes.FindAsync(model.CostCodeId.Value);
                if (cc != null) entity.Category = $"{cc.Code} {cc.Description}";
            }

            // Handle file replacement
            if (attachment != null && attachment.Length > 0)
            {
                // Delete old file if exists
                if (!string.IsNullOrEmpty(entity.AttachmentPath))
                {
                    var oldPath = Path.Combine(_env.WebRootPath, entity.AttachmentPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                    if (System.IO.File.Exists(oldPath)) System.IO.File.Delete(oldPath);
                }

                var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "receipts");
                Directory.CreateDirectory(uploadsFolder);
                var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(attachment.FileName)}";
                var filePath = Path.Combine(uploadsFolder, fileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await attachment.CopyToAsync(stream);
                }
                entity.AttachmentPath = $"/uploads/receipts/{fileName}";
            }

            await db.SaveChangesAsync();

            var user = await GetCurrentUserAsync();
            if (user != null)
            {
                await _auditService.LogAsync(
                    orgId.Value, 
                    user.Id, 
                    "Expense", 
                    entity.ExpenseId, 
                    "ExpenseUpdated", 
                    $"Expense updated: {entity.Category} - {entity.Amount:C}", 
                    "Info", 
                    HttpContext.Connection.RemoteIpAddress?.ToString()
                );
            }

            TempData["Message"] = "Expense updated.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var orgId = IsSuperAdmin() ? null : await GetMyOrgIdAsync();
            if (!orgId.HasValue) return RedirectToAction(nameof(Index));

            await using var db = await _tenantDbFactory.CreateAsync(orgId.Value);
            var entity = await db.Expenses.FirstOrDefaultAsync(e => e.ExpenseId == id && e.OrgId == orgId.Value);
            if (entity == null) return NotFound();

            // Delete attachment
            if (!string.IsNullOrEmpty(entity.AttachmentPath))
            {
                var filePath = Path.Combine(_env.WebRootPath, entity.AttachmentPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                if (System.IO.File.Exists(filePath)) System.IO.File.Delete(filePath);
            }

            db.Expenses.Remove(entity);
            await db.SaveChangesAsync();

            var user = await GetCurrentUserAsync();
            if (user != null)
            {
                await _auditService.LogAsync(
                    orgId.Value, 
                    user.Id, 
                    "Expense", 
                    id, 
                    "ExpenseDeleted", 
                    $"Expense deleted: {entity.Category} - {entity.Amount:C}", 
                    "Warning", 
                    HttpContext.Connection.RemoteIpAddress?.ToString()
                );
            }

            TempData["Message"] = "Expense deleted.";
            return RedirectToAction(nameof(Index));
        }

        private async Task PopulateExpenseDropdownsAsync(int orgId)
        {
            await using var db = await _tenantDbFactory.CreateAsync(orgId);
            ViewBag.Sites = await db.Sites.AsNoTracking().Where(s => s.OrgId == orgId && s.Status != "Archived").OrderBy(s => s.SiteName).ToListAsync();
            var poWithLines = await db.PurchaseOrders.AsNoTracking()
                .Include(p => p.LineItems)
                .Where(p => p.OrgId == orgId)
                .OrderByDescending(p => p.OrderDate)
                .ToListAsync();
            ViewBag.PurchaseOrders = poWithLines.Select(p => new
            {
                p.PurchaseOrderId,
                p.OrderNumber,
                Total = p.LineItems != null ? p.LineItems.Sum(l => l.Quantity * l.UnitCost) : 0m
            }).ToList();
            ViewBag.Risks = await db.Risks.AsNoTracking().Where(r => r.OrgId == orgId && r.DeletedAt == null).OrderByDescending(r => r.CreatedAt).Select(r => new { r.RiskId, r.Title }).Take(200).ToListAsync();
            ViewBag.CostCodes = await db.CostCodes.AsNoTracking()
                .Where(c => c.OrgId == orgId && c.ParentCostCodeId != null) // Only leaf nodes
                .OrderBy(c => c.Code)
                .Select(c => new { c.CostCodeId, c.Code, c.Description })
                .ToListAsync();
        }

        private void SetSelectedPoTotalFromModel(int? purchaseOrderId)
        {
            ViewBag.SelectedPoTotal = (decimal?)null;
            if (!purchaseOrderId.HasValue) return;
            var list = ViewBag.PurchaseOrders as IEnumerable<dynamic>;
            if (list == null) return;
            foreach (var x in list)
            {
                if ((int)x.PurchaseOrderId == purchaseOrderId.Value)
                {
                    ViewBag.SelectedPoTotal = (decimal)x.Total;
                    return;
                }
            }
        }
    }
}
