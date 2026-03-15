using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WEB_Sentro.Data;
using WEB_Sentro.Data.Entities;
using WEB_Sentro.Models.Identity;
using WEB_Sentro.Services;
using Web_Sentro.Areas.Client.Models;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Web_Sentro.Areas.Client.Controllers
{
    [Area("Client")]
    [Authorize]
    public class ChangeOrdersController : Controller
    {
        private readonly ITenantDbFactory _tenantDbFactory;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAuditService _auditService;

        public ChangeOrdersController(ITenantDbFactory tenantDbFactory, UserManager<ApplicationUser> userManager, IAuditService auditService)
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
            ViewData["Title"] = "Change Orders";
            var user = await GetCurrentUserAsync();
            if (user == null) return Challenge();

            var orgId = IsSuperAdmin() ? null : await GetMyOrgIdAsync();
            if (!orgId.HasValue)
                return View(new PagedResult<ChangeOrder> { Items = new List<ChangeOrder>(), TotalCount = 0, PageNumber = 1, PageSize = pageSize });

            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 5, 50);

            await using var db = await _tenantDbFactory.CreateAsync(orgId.Value);

            var sites = await db.Sites.AsNoTracking().Where(s => s.OrgId == orgId.Value && s.Status != "Archived").OrderBy(s => s.SiteName).Select(s => new { s.SiteId, s.SiteName, s.SiteCode }).ToListAsync();
            ViewBag.Sites = sites;
            ViewBag.SiteFilterList = new List<SelectListItem> { new SelectListItem { Value = "", Text = "All Sites" } };
            foreach (var s in sites)
                ((List<SelectListItem>)ViewBag.SiteFilterList).Add(new SelectListItem { Value = s.SiteId.ToString(), Text = $"{s.SiteName} ({s.SiteCode})", Selected = siteId == s.SiteId });

            var query = db.ChangeOrders.AsNoTracking()
                .Include(c => c.Site)
                .Include(c => c.Project)
                .Where(c => c.OrgId == orgId.Value);

            if (siteId.HasValue) query = query.Where(c => c.SiteId == siteId.Value);
            if (!string.IsNullOrWhiteSpace(status)) query = query.Where(c => c.Status == status);

            var totalCount = await query.CountAsync();
            var items = await query.OrderByDescending(c => c.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            var model = new PagedResult<ChangeOrder> { Items = items, TotalCount = totalCount, PageNumber = page, PageSize = pageSize };
            ViewBag.SelectedSiteId = siteId;
            ViewBag.SelectedStatus = status;
            ViewBag.Statuses = new[] { "Draft", "Pending", "Approved", "Rejected" };
            return View(model);
        }

        public async Task<IActionResult> Details(int id)
        {
            ViewData["Title"] = "Change Order Details";
            var orgId = IsSuperAdmin() ? null : await GetMyOrgIdAsync();
            if (!orgId.HasValue) return RedirectToAction(nameof(Index));

            await using var db = await _tenantDbFactory.CreateAsync(orgId.Value);
            var co = await db.ChangeOrders.AsNoTracking()
                .Include(c => c.Site)
                .Include(c => c.Project)
                .Include(c => c.LineItems).ThenInclude(l => l.CostCode)
                .FirstOrDefaultAsync(c => c.ChangeOrderId == id && c.OrgId == orgId.Value);
            
            if (co == null) return NotFound();
            
            // Get approver name if applicable
            if (!string.IsNullOrEmpty(co.ApprovedBy))
            {
                var approver = await _userManager.FindByIdAsync(co.ApprovedBy);
                ViewBag.ApproverName = approver?.UserName ?? co.ApprovedBy;
            }

            return View(co);
        }

        public async Task<IActionResult> Create()
        {
            ViewData["Title"] = "New Change Order";
            var orgId = IsSuperAdmin() ? null : await GetMyOrgIdAsync();
            if (!orgId.HasValue) return RedirectToAction(nameof(Index));

            await using var db = await _tenantDbFactory.CreateAsync(orgId.Value);
            await PopulateDropdowns(db, orgId.Value);
            
            return View(new ChangeOrder { Status = "Draft" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("SiteId,ProjectId,Title,Description,Status")] ChangeOrder model, List<ChangeOrderLine> items)
        {
            var orgId = IsSuperAdmin() ? null : await GetMyOrgIdAsync();
            if (!orgId.HasValue) return RedirectToAction(nameof(Index));

            if (ModelState.IsValid)
            {
                await using var db = await _tenantDbFactory.CreateAsync(orgId.Value);
                
                // Validate Site
                var site = await db.Sites.FirstOrDefaultAsync(s => s.SiteId == model.SiteId && s.OrgId == orgId.Value);
                if (site == null)
                {
                     ModelState.AddModelError("SiteId", "Invalid Site selected.");
                }
                else
                {
                    model.OrgId = orgId.Value;
                    model.CreatedAt = DateTime.UtcNow;
                    model.UpdatedAt = DateTime.UtcNow;
                    model.Status = model.Status ?? "Draft";
                    
                    if (items != null && items.Any())
                    {
                        foreach (var item in items)
                        {
                            if (!string.IsNullOrWhiteSpace(item.Description) && item.Amount != 0)
                            {
                                item.CostCodeId = item.CostCodeId; // Ensure mapping
                                model.LineItems.Add(item);
                            }
                        }
                    }

                    db.ChangeOrders.Add(model);
                    await db.SaveChangesAsync();

                    var user = await GetCurrentUserAsync();
                    await _auditService.LogAsync(orgId.Value, user?.Id, "ChangeOrder", model.ChangeOrderId, "Created", $"Change Order '{model.Title}' created.", "Info", HttpContext.Connection.RemoteIpAddress?.ToString());

                    TempData["Message"] = "Change Order created successfully.";
                    return RedirectToAction(nameof(Details), new { id = model.ChangeOrderId });
                }
            }

            // Reload dropdowns if failed
            await using var dbRetry = await _tenantDbFactory.CreateAsync(orgId.Value);
            await PopulateDropdowns(dbRetry, orgId.Value);
            return View(model);
        }

        public async Task<IActionResult> Edit(int id)
        {
            ViewData["Title"] = "Edit Change Order";
            var orgId = IsSuperAdmin() ? null : await GetMyOrgIdAsync();
            if (!orgId.HasValue) return RedirectToAction(nameof(Index));

            await using var db = await _tenantDbFactory.CreateAsync(orgId.Value);
            var co = await db.ChangeOrders.Include(c => c.LineItems).FirstOrDefaultAsync(c => c.ChangeOrderId == id && c.OrgId == orgId.Value);
            
            if (co == null) return NotFound();
            if (co.Status == "Approved" || co.Status == "Rejected")
            {
                TempData["Error"] = "Cannot edit a finalized Change Order.";
                return RedirectToAction(nameof(Details), new { id });
            }

            await PopulateDropdowns(db, orgId.Value);
            return View(co);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("ChangeOrderId,SiteId,ProjectId,Title,Description,Status")] ChangeOrder model, List<ChangeOrderLine> items)
        {
            if (id != model.ChangeOrderId) return NotFound();

            var orgId = IsSuperAdmin() ? null : await GetMyOrgIdAsync();
            if (!orgId.HasValue) return RedirectToAction(nameof(Index));

            await using var db = await _tenantDbFactory.CreateAsync(orgId.Value);
            var existing = await db.ChangeOrders.Include(c => c.LineItems).FirstOrDefaultAsync(c => c.ChangeOrderId == id && c.OrgId == orgId.Value);
            
            if (existing == null) return NotFound();
            if (existing.Status == "Approved" || existing.Status == "Rejected")
            {
                TempData["Error"] = "Cannot edit a finalized Change Order.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (ModelState.IsValid)
            {
                existing.SiteId = model.SiteId;
                existing.ProjectId = model.ProjectId;
                existing.Title = model.Title;
                existing.Description = model.Description;
                existing.Status = model.Status; // Can update status back to Draft or directly to Pending
                existing.UpdatedAt = DateTime.UtcNow;

                // Update Line Items
                db.ChangeOrderLines.RemoveRange(existing.LineItems);
                if (items != null && items.Any())
                {
                    foreach (var item in items)
                    {
                         if (!string.IsNullOrWhiteSpace(item.Description) && item.Amount != 0)
                        {
                            existing.LineItems.Add(new ChangeOrderLine
                            {
                                Description = item.Description,
                                Amount = item.Amount,
                                CostCodeId = item.CostCodeId
                            });
                        }
                    }
                }

                await db.SaveChangesAsync();

                var user = await GetCurrentUserAsync();
                await _auditService.LogAsync(orgId.Value, user?.Id, "ChangeOrder", id, "Updated", $"Change Order '{model.Title}' updated.", "Info", HttpContext.Connection.RemoteIpAddress?.ToString());

                TempData["Message"] = "Change Order updated successfully.";
                return RedirectToAction(nameof(Details), new { id });
            }

            await PopulateDropdowns(db, orgId.Value);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id)
        {
            return await UpdateStatus(id, "Approved");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id)
        {
            return await UpdateStatus(id, "Rejected");
        }
        
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(int id)
        {
            return await UpdateStatus(id, "Pending");
        }

        private async Task<IActionResult> UpdateStatus(int id, string newStatus)
        {
            var orgId = IsSuperAdmin() ? null : await GetMyOrgIdAsync();
            if (!orgId.HasValue) return RedirectToAction(nameof(Index));

            await using var db = await _tenantDbFactory.CreateAsync(orgId.Value);
            var co = await db.ChangeOrders.FirstOrDefaultAsync(c => c.ChangeOrderId == id && c.OrgId == orgId.Value);
            
            if (co == null) return NotFound();

            var user = await GetCurrentUserAsync();
            
            // Logic validation
            if (newStatus == "Approved" || newStatus == "Rejected")
            {
                // Simple logic: Only Pending can be Approved/Rejected
                if (co.Status != "Pending")
                {
                     TempData["Error"] = "Only Pending Change Orders can be approved or rejected.";
                     return RedirectToAction(nameof(Details), new { id });
                }
                
                co.ApprovedBy = user?.Id;
                co.ApprovedAt = DateTime.UtcNow;
            }
            
            co.Status = newStatus;
            co.UpdatedAt = DateTime.UtcNow;
            
            await db.SaveChangesAsync();

            await _auditService.LogAsync(orgId.Value, user?.Id, "ChangeOrder", id, "StatusChanged", $"Change Order status changed to {newStatus}.", "Info", HttpContext.Connection.RemoteIpAddress?.ToString());

            TempData["Message"] = $"Change Order {newStatus}.";
            return RedirectToAction(nameof(Details), new { id });
        }

        private async Task PopulateDropdowns(TenantDbContext db, int orgId)
        {
            ViewBag.Sites = await db.Sites.AsNoTracking().Where(s => s.OrgId == orgId && s.Status != "Archived").OrderBy(s => s.SiteName).ToListAsync();
            ViewBag.Projects = await db.Projects.AsNoTracking().Where(p => p.OrgId == orgId && p.Status != "Archived").OrderBy(p => p.Name).ToListAsync();
            ViewBag.CostCodes = await db.CostCodes.AsNoTracking().Where(c => c.OrgId == orgId && c.ParentCostCodeId != null).OrderBy(c => c.Code).Select(c => new { c.CostCodeId, c.Code, c.Description }).ToListAsync();
        }
    }
}
