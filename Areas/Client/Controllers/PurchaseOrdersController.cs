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
    [Authorize(Policy = "ProcurementAccess")]
    public class PurchaseOrdersController : Controller
    {
        private readonly ITenantDbFactory _tenantDbFactory;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAuditService _auditService;

        public PurchaseOrdersController(ITenantDbFactory tenantDbFactory, UserManager<ApplicationUser> userManager, IAuditService auditService)
        {
            _tenantDbFactory = tenantDbFactory;
            _userManager = userManager;
            _auditService = auditService;
        }

        private async Task<ApplicationUser?> GetCurrentUserAsync() => await _userManager.GetUserAsync(User);
        private async Task<int?> GetMyOrgIdAsync()
        {
            var me = await GetCurrentUserAsync();
            return me?.OrganizationId;
        }
        private bool IsSuperAdmin() => User.IsInRole("SuperAdmin");

        public async Task<IActionResult> Index(int? siteId, string? status, int page = 1, int pageSize = 10)
        {
            ViewData["Title"] = "Purchase Orders";
            var user = await GetCurrentUserAsync();
            if (user == null) return Challenge();

            var orgId = IsSuperAdmin() ? null : await GetMyOrgIdAsync();
            if (!orgId.HasValue)
                return View(new PagedResult<PurchaseOrder> { Items = new List<PurchaseOrder>(), TotalCount = 0, PageNumber = 1, PageSize = pageSize });

            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 5, 50);

            await using var db = await _tenantDbFactory.CreateAsync(orgId.Value);

            var sites = await db.Sites.AsNoTracking().Where(s => s.OrgId == orgId.Value && s.Status != "Archived").OrderBy(s => s.SiteName).Select(s => new { s.SiteId, s.SiteName, s.SiteCode }).ToListAsync();
            ViewBag.Sites = sites;
            ViewBag.SiteFilterList = new List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem> { new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = "", Text = "All Sites" } };
            foreach (var s in sites)
                ((List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem>)ViewBag.SiteFilterList).Add(new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = s.SiteId.ToString(), Text = $"{s.SiteName} ({s.SiteCode})", Selected = siteId == s.SiteId });

            var query = db.PurchaseOrders.AsNoTracking()
                .Include(p => p.Site).Include(p => p.Supplier)
                .Where(p => p.OrgId == orgId.Value);

            if (siteId.HasValue) query = query.Where(p => p.SiteId == siteId.Value);
            if (!string.IsNullOrWhiteSpace(status)) query = query.Where(p => p.Status == status);

            var totalCount = await query.CountAsync();
            var items = await query.OrderByDescending(p => p.OrderDate).ThenByDescending(p => p.PurchaseOrderId).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            var model = new PagedResult<PurchaseOrder> { Items = items, TotalCount = totalCount, PageNumber = page, PageSize = pageSize };
            ViewBag.SelectedSiteId = siteId;
            ViewBag.SelectedStatus = status;
            ViewBag.Statuses = new[] { "Draft", "Sent", "Received", "Cancelled" };
            return View(model);
        }

        public async Task<IActionResult> Details(int id)
        {
            ViewData["Title"] = "Purchase Order";
            var orgId = IsSuperAdmin() ? null : await GetMyOrgIdAsync();
            if (!orgId.HasValue) return RedirectToAction(nameof(Index));

            await using var db = await _tenantDbFactory.CreateAsync(orgId.Value);
            var po = await db.PurchaseOrders.AsNoTracking()
                .Include(p => p.Site).Include(p => p.Supplier).Include(p => p.LineItems).ThenInclude(l => l.CostCode)
                .FirstOrDefaultAsync(p => p.PurchaseOrderId == id && p.OrgId == orgId.Value);
            if (po == null) return NotFound();
            return View(po);
        }

        public async Task<IActionResult> Create()
        {
            ViewData["Title"] = "New Purchase Order";
            var orgId = IsSuperAdmin() ? null : await GetMyOrgIdAsync();
            if (!orgId.HasValue) return RedirectToAction(nameof(Index));

            await using var db = await _tenantDbFactory.CreateAsync(orgId.Value);
            var sites = await db.Sites.AsNoTracking().Where(s => s.OrgId == orgId.Value && s.Status != "Archived").OrderBy(s => s.SiteName).ToListAsync();
            var suppliers = await db.Suppliers.AsNoTracking().Where(s => s.OrgId == orgId.Value).OrderBy(s => s.Name).ToListAsync();
            ViewBag.Sites = sites;
            ViewBag.Suppliers = suppliers;
            ViewBag.CostCodes = await db.CostCodes.AsNoTracking().Where(c => c.OrgId == orgId.Value && c.ParentCostCodeId != null).OrderBy(c => c.Code).Select(c => new { c.CostCodeId, c.Code, c.Description }).ToListAsync();
            var nextNum = await db.PurchaseOrders.Where(p => p.OrgId == orgId.Value).CountAsync() + 1;
            ViewBag.SuggestedOrderNumber = $"PO-{DateTime.UtcNow:yyyyMMdd}-{nextNum:D4}";
            return View(new PurchaseOrder { OrderDate = DateTime.UtcNow.Date, Status = "Draft" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("SiteId,SupplierId,OrderNumber,OrderDate,Status,ExpectedDeliveryDate")] PurchaseOrder model, List<PurchaseOrderLine> items)
        {
            var orgId = IsSuperAdmin() ? null : await GetMyOrgIdAsync();
            if (!orgId.HasValue) return RedirectToAction(nameof(Index));

            if (model == null) return RedirectToAction(nameof(Index));

            if (string.IsNullOrWhiteSpace(model.OrderNumber)) 
            { 
                ModelState.AddModelError("OrderNumber", "Order number is required."); 
                await PopulateCreateDropdownsAsync(orgId.Value, model); 
                if (items != null) model.LineItems = items;
                return View(model); 
            }

            await using var db = await _tenantDbFactory.CreateAsync(orgId.Value);
            var site = await db.Sites.AnyAsync(s => s.SiteId == model.SiteId && s.OrgId == orgId.Value);
            var supplier = await db.Suppliers.AnyAsync(s => s.SupplierId == model.SupplierId && s.OrgId == orgId.Value);
            if (!site || !supplier) 
            { 
                ModelState.AddModelError("", "Invalid site or supplier."); 
                await PopulateCreateDropdownsAsync(orgId.Value, model); 
                if (items != null) model.LineItems = items;
                return View(model); 
            }

            var entity = new PurchaseOrder
            {
                OrgId = orgId.Value,
                SiteId = model.SiteId,
                SupplierId = model.SupplierId,
                OrderNumber = model.OrderNumber.Trim(),
                OrderDate = model.OrderDate,
                Status = model.Status ?? "Draft",
                ExpectedDeliveryDate = model.ExpectedDeliveryDate,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.PurchaseOrders.Add(entity);
            await db.SaveChangesAsync();

            var user = await GetCurrentUserAsync();
            if (user != null)
            {
                await _auditService.LogAsync(
                    orgId.Value, 
                    user.Id, 
                    "PurchaseOrder", 
                    entity.PurchaseOrderId, 
                    "POCreated", 
                    $"Purchase Order {entity.OrderNumber} created.", 
                    "Info", 
                    HttpContext.Connection.RemoteIpAddress?.ToString()
                );
            }

            if (items != null && items.Any())
            {
                foreach (var item in items)
                {
                    if (!string.IsNullOrWhiteSpace(item.Description) && item.Quantity > 0)
                    {
                        var line = new PurchaseOrderLine 
                        { 
                            PurchaseOrderId = entity.PurchaseOrderId, 
                            Description = item.Description, 
                            Quantity = item.Quantity, 
                            UnitCost = item.UnitCost,
                            CostCodeId = item.CostCodeId
                        };
                        db.PurchaseOrderLines.Add(line);
                    }
                }
                await db.SaveChangesAsync();
            }

            TempData["ToastSuccess"] = "Purchase order created.";
            return RedirectToAction(nameof(Details), new { id = entity.PurchaseOrderId });
        }

        private async Task PopulateCreateDropdownsAsync(int orgId, PurchaseOrder? model)
        {
            await using var db = await _tenantDbFactory.CreateAsync(orgId);
            ViewBag.Sites = await db.Sites.AsNoTracking().Where(s => s.OrgId == orgId && s.Status != "Archived").OrderBy(s => s.SiteName).ToListAsync();
            ViewBag.Suppliers = await db.Suppliers.AsNoTracking().Where(s => s.OrgId == orgId).OrderBy(s => s.Name).ToListAsync();
            ViewBag.SuggestedOrderNumber = $"PO-{DateTime.UtcNow:yyyyMMdd}-{await db.PurchaseOrders.CountAsync(p => p.OrgId == orgId) + 1:D4}";
            ViewBag.CostCodes = await db.CostCodes.AsNoTracking().Where(c => c.OrgId == orgId && c.ParentCostCodeId != null).OrderBy(c => c.Code).Select(c => new { c.CostCodeId, c.Code, c.Description }).ToListAsync();
        }

        public async Task<IActionResult> Edit(int id)
        {
            ViewData["Title"] = "Edit Purchase Order";
            var orgId = IsSuperAdmin() ? null : await GetMyOrgIdAsync();
            if (!orgId.HasValue) return RedirectToAction(nameof(Index));

            await using var db = await _tenantDbFactory.CreateAsync(orgId.Value);
            var po = await db.PurchaseOrders.Include(p => p.LineItems).FirstOrDefaultAsync(p => p.PurchaseOrderId == id && p.OrgId == orgId.Value);
            if (po == null) return NotFound();
            ViewBag.Sites = await db.Sites.AsNoTracking().Where(s => s.OrgId == orgId.Value && s.Status != "Archived").OrderBy(s => s.SiteName).ToListAsync();
            ViewBag.Suppliers = await db.Suppliers.AsNoTracking().Where(s => s.OrgId == orgId.Value).OrderBy(s => s.Name).ToListAsync();
            ViewBag.CostCodes = await db.CostCodes.AsNoTracking().Where(c => c.OrgId == orgId.Value && c.ParentCostCodeId != null).OrderBy(c => c.Code).Select(c => new { c.CostCodeId, c.Code, c.Description }).ToListAsync();
            return View(po);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("PurchaseOrderId,SiteId,SupplierId,OrderNumber,OrderDate,Status,ExpectedDeliveryDate")] PurchaseOrder model, List<PurchaseOrderLine> items)
        {
            if (id != model.PurchaseOrderId) return NotFound();
            var orgId = IsSuperAdmin() ? null : await GetMyOrgIdAsync();
            if (!orgId.HasValue) return RedirectToAction(nameof(Index));

            await using var db = await _tenantDbFactory.CreateAsync(orgId.Value);
            var entity = await db.PurchaseOrders.Include(p => p.LineItems).FirstOrDefaultAsync(p => p.PurchaseOrderId == id && p.OrgId == orgId.Value);
            if (entity == null) return NotFound();

            entity.SiteId = model.SiteId;
            entity.SupplierId = model.SupplierId;
            entity.OrderNumber = model.OrderNumber?.Trim() ?? entity.OrderNumber;
            entity.OrderDate = model.OrderDate;
            entity.Status = model.Status ?? entity.Status;
            entity.ExpectedDeliveryDate = model.ExpectedDeliveryDate;
            entity.UpdatedAt = DateTime.UtcNow;

            // Handle Line Items
            if (entity.LineItems != null)
            {
                db.PurchaseOrderLines.RemoveRange(entity.LineItems);
            }

            if (items != null && items.Any())
            {
                foreach (var item in items)
                {
                    if (!string.IsNullOrWhiteSpace(item.Description) && item.Quantity > 0)
                    {
                        var newLine = new PurchaseOrderLine 
                        { 
                            PurchaseOrderId = entity.PurchaseOrderId, 
                            Description = item.Description, 
                            Quantity = item.Quantity, 
                            UnitCost = item.UnitCost,
                            CostCodeId = item.CostCodeId
                        };
                        db.PurchaseOrderLines.Add(newLine);
                    }
                }
            }

            await db.SaveChangesAsync();
            
            var user = await GetCurrentUserAsync();
            if (user != null)
            {
                await _auditService.LogAsync(
                    orgId.Value, 
                    user.Id, 
                    "PurchaseOrder", 
                    entity.PurchaseOrderId, 
                    "POUpdated", 
                    $"Purchase Order {entity.OrderNumber} updated.", 
                    "Info", 
                    HttpContext.Connection.RemoteIpAddress?.ToString()
                );
            }
            
            TempData["ToastSuccess"] = "Purchase order updated.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int id, string status)
        {
            var orgId = IsSuperAdmin() ? null : await GetMyOrgIdAsync();
            if (!orgId.HasValue) return RedirectToAction(nameof(Index));
            var allowed = new[] { "Draft", "Sent", "Received", "Cancelled" };
            if (string.IsNullOrEmpty(status) || !allowed.Contains(status)) return RedirectToAction(nameof(Details), new { id });

            await using var db = await _tenantDbFactory.CreateAsync(orgId.Value);
            var entity = await db.PurchaseOrders.FirstOrDefaultAsync(p => p.PurchaseOrderId == id && p.OrgId == orgId.Value);
            if (entity == null) return NotFound();
            entity.Status = status;
            entity.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            var user = await GetCurrentUserAsync();
            if (user != null)
            {
                await _auditService.LogAsync(
                    orgId.Value, 
                    user.Id, 
                    "PurchaseOrder", 
                    id, 
                    "StatusChanged", 
                    $"Status updated to {status} for PO {entity.OrderNumber}", 
                    "Info", 
                    HttpContext.Connection.RemoteIpAddress?.ToString()
                );
            }

            TempData["ToastSuccess"] = "Status updated to " + status + ".";
            return RedirectToAction(nameof(Details), new { id });
        }
    }
}
