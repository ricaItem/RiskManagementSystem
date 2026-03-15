using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WEB_Sentro.Data;
using WEB_Sentro.Data.Entities;
using WEB_Sentro.Data.Seed;
using WEB_Sentro.Models.Identity;
using WEB_Sentro.Services;
using Web_Sentro.Areas.Client.Models;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Web_Sentro.Areas.Client.Controllers
{
    [Area("Client")]
    [Authorize]
    public class CostCodesController : Controller
    {
        private readonly ITenantDbFactory _tenantDbFactory;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAuditService _auditService;

        public CostCodesController(ITenantDbFactory tenantDbFactory, UserManager<ApplicationUser> userManager, IAuditService auditService)
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

        public async Task<IActionResult> Index(string? search, int page = 1, int pageSize = 20)
        {
            ViewData["Title"] = "Cost Codes";
            var user = await GetCurrentUserAsync();
            if (user == null) return Challenge();

            var orgId = IsSuperAdmin() ? null : await GetMyOrgIdAsync();
            if (!orgId.HasValue)
                 return View(new PagedResult<CostCode> { Items = new List<CostCode>(), TotalCount = 0, PageNumber = 1, PageSize = pageSize });

            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 5, 100);

            await using var db = await _tenantDbFactory.CreateAsync(orgId.Value);

            var query = db.CostCodes.AsNoTracking()
                .Where(c => c.OrgId == orgId.Value);

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(c => c.Code.Contains(search) || c.Description.Contains(search));
            }

            var totalCount = await query.CountAsync();
            var items = await query.OrderBy(c => c.Code).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            var model = new PagedResult<CostCode> { Items = items, TotalCount = totalCount, PageNumber = page, PageSize = pageSize };
            ViewBag.Search = search;
            return View(model);
        }

        public async Task<IActionResult> Create()
        {
            ViewData["Title"] = "New Cost Code";
            var orgId = IsSuperAdmin() ? null : await GetMyOrgIdAsync();
            if (!orgId.HasValue) return RedirectToAction(nameof(Index));

            await using var db = await _tenantDbFactory.CreateAsync(orgId.Value);
            ViewBag.ParentCostCodes = await db.CostCodes.AsNoTracking()
                .Where(c => c.OrgId == orgId.Value && c.ParentCostCodeId == null)
                .OrderBy(c => c.Code)
                .Select(c => new SelectListItem { Value = c.CostCodeId.ToString(), Text = $"{c.Code} - {c.Description}" })
                .ToListAsync();

            return View(new CostCode());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Code,Description,ParentCostCodeId")] CostCode model)
        {
            var orgId = IsSuperAdmin() ? null : await GetMyOrgIdAsync();
            if (!orgId.HasValue) return RedirectToAction(nameof(Index));

            if (ModelState.IsValid)
            {
                await using var db = await _tenantDbFactory.CreateAsync(orgId.Value);
                
                if (await db.CostCodes.AnyAsync(c => c.OrgId == orgId.Value && c.Code == model.Code))
                {
                    ModelState.AddModelError("Code", "Cost Code already exists.");
                }
                else
                {
                    model.OrgId = orgId.Value;
                    model.CreatedAt = DateTime.UtcNow;
                    model.UpdatedAt = DateTime.UtcNow;
                    
                    db.CostCodes.Add(model);
                    await db.SaveChangesAsync();

                    var user = await GetCurrentUserAsync();
                    await _auditService.LogAsync(orgId.Value, user?.Id, "CostCode", model.CostCodeId, "Created", $"Cost Code '{model.Code}' created.", "Info", HttpContext.Connection.RemoteIpAddress?.ToString());

                    TempData["Message"] = "Cost Code created successfully.";
                    return RedirectToAction(nameof(Index));
                }
            }

            await using var dbRetry = await _tenantDbFactory.CreateAsync(orgId.Value);
            ViewBag.ParentCostCodes = await dbRetry.CostCodes.AsNoTracking()
                .Where(c => c.OrgId == orgId.Value && c.ParentCostCodeId == null)
                .OrderBy(c => c.Code)
                .Select(c => new SelectListItem { Value = c.CostCodeId.ToString(), Text = $"{c.Code} - {c.Description}" })
                .ToListAsync();

            return View(model);
        }

        public async Task<IActionResult> Edit(int id)
        {
            ViewData["Title"] = "Edit Cost Code";
            var orgId = IsSuperAdmin() ? null : await GetMyOrgIdAsync();
            if (!orgId.HasValue) return RedirectToAction(nameof(Index));

            await using var db = await _tenantDbFactory.CreateAsync(orgId.Value);
            var costCode = await db.CostCodes.FirstOrDefaultAsync(c => c.CostCodeId == id && c.OrgId == orgId.Value);
            
            if (costCode == null) return NotFound();

            ViewBag.ParentCostCodes = await db.CostCodes.AsNoTracking()
                .Where(c => c.OrgId == orgId.Value && c.ParentCostCodeId == null && c.CostCodeId != id)
                .OrderBy(c => c.Code)
                .Select(c => new SelectListItem { Value = c.CostCodeId.ToString(), Text = $"{c.Code} - {c.Description}", Selected = c.CostCodeId == costCode.ParentCostCodeId })
                .ToListAsync();

            return View(costCode);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("CostCodeId,Code,Description,ParentCostCodeId")] CostCode model)
        {
            if (id != model.CostCodeId) return NotFound();

            var orgId = IsSuperAdmin() ? null : await GetMyOrgIdAsync();
            if (!orgId.HasValue) return RedirectToAction(nameof(Index));

            if (ModelState.IsValid)
            {
                await using var db = await _tenantDbFactory.CreateAsync(orgId.Value);
                var existing = await db.CostCodes.FirstOrDefaultAsync(c => c.CostCodeId == id && c.OrgId == orgId.Value);
                if (existing == null) return NotFound();

                if (existing.Code != model.Code && await db.CostCodes.AnyAsync(c => c.OrgId == orgId.Value && c.Code == model.Code))
                {
                    ModelState.AddModelError("Code", "Cost Code already exists.");
                }
                else
                {
                    existing.Code = model.Code;
                    existing.Description = model.Description;
                    existing.ParentCostCodeId = model.ParentCostCodeId;
                    existing.UpdatedAt = DateTime.UtcNow;

                    await db.SaveChangesAsync();

                    var user = await GetCurrentUserAsync();
                    await _auditService.LogAsync(orgId.Value, user?.Id, "CostCode", id, "Updated", $"Cost Code '{model.Code}' updated.", "Info", HttpContext.Connection.RemoteIpAddress?.ToString());

                    TempData["Message"] = "Cost Code updated successfully.";
                    return RedirectToAction(nameof(Index));
                }
            }
            
            await using var dbRetry = await _tenantDbFactory.CreateAsync(orgId.Value);
            ViewBag.ParentCostCodes = await dbRetry.CostCodes.AsNoTracking()
                .Where(c => c.OrgId == orgId.Value && c.ParentCostCodeId == null && c.CostCodeId != id)
                .OrderBy(c => c.Code)
                .Select(c => new SelectListItem { Value = c.CostCodeId.ToString(), Text = $"{c.Code} - {c.Description}", Selected = c.CostCodeId == model.ParentCostCodeId })
                .ToListAsync();

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportMasterFormat()
        {
            var orgId = IsSuperAdmin() ? null : await GetMyOrgIdAsync();
            if (!orgId.HasValue) return RedirectToAction(nameof(Index));

            await using var db = await _tenantDbFactory.CreateAsync(orgId.Value);
            
            if (await db.CostCodes.AnyAsync(c => c.OrgId == orgId.Value))
            {
                TempData["Error"] = "Cannot import MasterFormat: Cost Codes already exist.";
                return RedirectToAction(nameof(Index));
            }

            await CostCodeSeeder.SeedAsync(db, orgId.Value);
            
            var user = await GetCurrentUserAsync();
            await _auditService.LogAsync(orgId.Value, user?.Id, "CostCode", 0, "Imported", "MasterFormat Cost Codes imported.", "Info", HttpContext.Connection.RemoteIpAddress?.ToString());

            TempData["Message"] = "Standard MasterFormat Cost Codes imported successfully.";
            return RedirectToAction(nameof(Index));
        }
    }
}
