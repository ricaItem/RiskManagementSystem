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
    public class SuppliersController : Controller
    {
        private readonly ITenantDbFactory _tenantDbFactory;
        private readonly UserManager<ApplicationUser> _userManager;

        public SuppliersController(ITenantDbFactory tenantDbFactory, UserManager<ApplicationUser> userManager)
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

        public async Task<IActionResult> Index(string? category, string? search, int page = 1, int pageSize = 10)
        {
            ViewData["Title"] = "Suppliers";
            var user = await GetCurrentUserAsync();
            if (user == null) return Challenge();

            var orgId = IsSuperAdmin() ? null : await GetMyOrgIdAsync();
            if (!orgId.HasValue)
            {
                return View(new PagedResult<Supplier> { Items = new List<Supplier>(), TotalCount = 0, PageNumber = 1, PageSize = pageSize });
            }

            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 5, 50);

            await using var db = await _tenantDbFactory.CreateAsync(orgId.Value);
            var query = db.Suppliers.AsNoTracking().Where(s => s.OrgId == orgId.Value);

            if (!string.IsNullOrWhiteSpace(category))
                query = query.Where(s => s.Category == category);
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(s => s.Name.Contains(search) || (s.ContactPerson != null && s.ContactPerson.Contains(search)) || (s.Email != null && s.Email.Contains(search)));

            var totalCount = await query.CountAsync();
            var items = await query.OrderBy(s => s.Name).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            var model = new PagedResult<Supplier>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = page,
                PageSize = pageSize
            };

            ViewBag.CategoryFilter = category;
            ViewBag.Search = search;
            ViewBag.Categories = new[] { "Materials", "Equipment", "Services" };
            return View(model);
        }

        public async Task<IActionResult> Create()
        {
            ViewData["Title"] = "New Supplier";
            var orgId = IsSuperAdmin() ? null : await GetMyOrgIdAsync();
            if (!orgId.HasValue) return RedirectToAction(nameof(Index));
            return View(new Supplier { Category = "Materials" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name,ContactPerson,Email,Phone,Category")] Supplier model)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Challenge();
            var orgId = IsSuperAdmin() ? null : await GetMyOrgIdAsync();
            if (!orgId.HasValue) return RedirectToAction(nameof(Index));

            if (string.IsNullOrWhiteSpace(model?.Name))
            {
                ModelState.AddModelError("Name", "Name is required.");
                return View(model);
            }

            await using var db = await _tenantDbFactory.CreateAsync(orgId.Value);
            var entity = new Supplier
            {
                OrgId = orgId.Value,
                Name = model.Name.Trim(),
                ContactPerson = model.ContactPerson?.Trim(),
                Email = model.Email?.Trim(),
                Phone = model.Phone?.Trim(),
                Category = model.Category?.Trim() ?? "Materials",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.Suppliers.Add(entity);
            await db.SaveChangesAsync();
            TempData["Message"] = "Supplier created.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            ViewData["Title"] = "Edit Supplier";
            var orgId = IsSuperAdmin() ? null : await GetMyOrgIdAsync();
            if (!orgId.HasValue) return RedirectToAction(nameof(Index));

            await using var db = await _tenantDbFactory.CreateAsync(orgId.Value);
            var entity = await db.Suppliers.FirstOrDefaultAsync(s => s.SupplierId == id && s.OrgId == orgId.Value);
            if (entity == null) return NotFound();
            return View(entity);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("SupplierId,Name,ContactPerson,Email,Phone,Category")] Supplier model)
        {
            if (id != model.SupplierId) return NotFound();
            var orgId = IsSuperAdmin() ? null : await GetMyOrgIdAsync();
            if (!orgId.HasValue) return RedirectToAction(nameof(Index));

            if (string.IsNullOrWhiteSpace(model?.Name))
            {
                ModelState.AddModelError("Name", "Name is required.");
                return View(model);
            }

            await using var db = await _tenantDbFactory.CreateAsync(orgId.Value);
            var entity = await db.Suppliers.FirstOrDefaultAsync(s => s.SupplierId == id && s.OrgId == orgId.Value);
            if (entity == null) return NotFound();

            entity.Name = model.Name.Trim();
            entity.ContactPerson = model.ContactPerson?.Trim();
            entity.Email = model.Email?.Trim();
            entity.Phone = model.Phone?.Trim();
            entity.Category = model.Category?.Trim();
            entity.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            TempData["Message"] = "Supplier updated.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Delete(int id)
        {
            ViewData["Title"] = "Delete Supplier";
            var orgId = IsSuperAdmin() ? null : await GetMyOrgIdAsync();
            if (!orgId.HasValue) return RedirectToAction(nameof(Index));

            await using var db = await _tenantDbFactory.CreateAsync(orgId.Value);
            var entity = await db.Suppliers.AsNoTracking().FirstOrDefaultAsync(s => s.SupplierId == id && s.OrgId == orgId.Value);
            if (entity == null) return NotFound();
            return View(entity);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var orgId = IsSuperAdmin() ? null : await GetMyOrgIdAsync();
            if (!orgId.HasValue) return RedirectToAction(nameof(Index));

            await using var db = await _tenantDbFactory.CreateAsync(orgId.Value);
            var entity = await db.Suppliers.FirstOrDefaultAsync(s => s.SupplierId == id && s.OrgId == orgId.Value);
            if (entity == null) return NotFound();
            db.Suppliers.Remove(entity);
            await db.SaveChangesAsync();
            TempData["Message"] = "Supplier deleted.";
            return RedirectToAction(nameof(Index));
        }
    }
}
