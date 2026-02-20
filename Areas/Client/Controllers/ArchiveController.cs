using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WEB_Sentro.Data;
using WEB_Sentro.Models.Identity;

namespace Web_Sentro.Areas.Client.Controllers
{
    [Area("Client")]
    [Authorize(Policy = "AdminOrVendor")]
    public class ArchiveController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public ArchiveController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        private bool IsVendor() => User.IsInRole("SuperAdmin");

        private async Task<ApplicationUser?> GetMeAsync()
            => await _userManager.GetUserAsync(User);

        private async Task<int?> GetMyOrgIdAsync()
        {
            var me = await GetMeAsync();
            return me?.OrganizationId;
        }

        private async Task<IQueryable<ApplicationUser>> TenantUsersQueryAsync()
        {
            var q = _db.Users.AsQueryable();
            if (IsVendor())
                return q;
            var orgId = await GetMyOrgIdAsync();
            if (orgId == null)
                return q.Where(u => false);
            return q.Where(u => u.OrganizationId == orgId.Value);
        }

        private async Task<bool> CanTouchUserAsync(ApplicationUser target)
        {
            if (IsVendor()) return true;
            var orgId = await GetMyOrgIdAsync();
            return orgId != null && target.OrganizationId == orgId.Value;
        }

        // GET: /Client/Archive/Index — archived employees (inactive users)
        public async Task<IActionResult> Index()
        {
            var q = await TenantUsersQueryAsync();
            var users = await q.AsNoTracking()
                .Where(u => !u.IsActive)
                .OrderBy(u => u.LastName)
                .ThenBy(u => u.FirstName)
                .ToListAsync();

            var data = new List<ArchivedEmployeeVm>();
            foreach (var u in users)
            {
                var roles = await _userManager.GetRolesAsync(u);
                data.Add(new ArchivedEmployeeVm
                {
                    UserId = u.Id,
                    Name = $"{u.FirstName} {u.LastName}".Trim(),
                    Email = u.Email ?? "",
                    Role = roles.FirstOrDefault() ?? "Employee",
                    Module = "Employee",
                    CreatedAt = u.CreatedAt
                });
            }

            return View(data);
        }

        // POST: Restore archived employee — sets IsActive = true so they reappear in Employees
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RestoreEmployee(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                TempData["Alert"] = "Invalid employee.";
                return RedirectToAction(nameof(Index));
            }

            var target = await _db.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (target == null)
            {
                TempData["Alert"] = "Employee not found.";
                return RedirectToAction(nameof(Index));
            }

            if (!await CanTouchUserAsync(target))
                return Forbid();

            target.IsActive = true;
            var updateRes = await _userManager.UpdateAsync(target);
            if (!updateRes.Succeeded)
            {
                TempData["Alert"] = string.Join(" | ", updateRes.Errors.Select(e => e.Description));
                return RedirectToAction(nameof(Index));
            }

            TempData["Alert"] = $"{target.FirstName} {target.LastName} has been restored and is active again.";
            return RedirectToAction(nameof(Index));
        }

        // POST: Permanent delete (optional; keeps mock behavior for non-employee items)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult PermanentDelete(int id)
        {
            return RedirectToAction(nameof(Index));
        }
    }

    public class ArchivedEmployeeVm
    {
        public string UserId { get; set; } = "";
        public string Name { get; set; } = "";
        public string Email { get; set; } = "";
        public string Role { get; set; } = "";
        public string Module { get; set; } = "Employee";
        public DateTime CreatedAt { get; set; }
    }
}