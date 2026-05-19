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
    [Authorize(Policy = "MainAdminOnly")]
    public class ArchiveController : Controller
    {
        // ✅ Identity is now in PlatformDbContext (shared db)
        private readonly PlatformDbContext _platformDb;
        private readonly UserManager<ApplicationUser> _userManager;

        public ArchiveController(PlatformDbContext platformDb, UserManager<ApplicationUser> userManager)
        {
            _platformDb = platformDb;
            _userManager = userManager;
        }

        private bool IsVendor() => User.IsInRole("SuperAdmin");

        private async Task<ApplicationUser?> GetMeAsync()
            => await _userManager.GetUserAsync(User);

        private async Task<int?> GetMyOrgIdAsync()
        {
            var me = await GetMeAsync();
            // If OrganizationId is non-nullable int in your model, this will always be set.
            // If you later change it to int?, then this correctly returns null for SuperAdmin.
            return me?.OrganizationId;
        }

        /// <summary>
        /// Returns the queryable users visible to the current user:
        /// - Vendor/SuperAdmin: all users
        /// - Org users: only users in their organization
        /// </summary>
        private async Task<IQueryable<ApplicationUser>> TenantUsersQueryAsync()
        {
            var q = _platformDb.Users.AsQueryable();

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

        public IActionResult Index()
        {
            return View();
        }

        public async Task<IActionResult> ArchiveContent()
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

            return PartialView("_ArchiveContent", data);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RestoreEmployee(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                TempData["ToastError"] = "Invalid employee.";
                return RedirectToAction(nameof(Index));
            }

            var target = await _platformDb.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (target == null)
            {
                TempData["ToastError"] = "Employee not found.";
                return RedirectToAction(nameof(Index));
            }

            if (!await CanTouchUserAsync(target))
                return Forbid();

            target.IsActive = true;

            var updateRes = await _userManager.UpdateAsync(target);
            if (!updateRes.Succeeded)
            {
                TempData["ToastError"] = string.Join(" | ", updateRes.Errors.Select(e => e.Description));
                return RedirectToAction(nameof(Index));
            }

            TempData["ToastSuccess"] = $"{target.FirstName} {target.LastName} has been restored and is active again.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult PermanentDelete(int id)
        {
            // TODO: Implement permanent delete if you add a "hard delete user" policy/flow.
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
