using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WEB_Sentro.Data;
using WEB_Sentro.Models.Identity;

namespace Web_Sentro.Areas.Client.Controllers
{
    [Area("Client")]
    [Authorize(Policy = "AdminOrVendor")]
    public class EmployeesController : Controller
    {
        private readonly PlatformDbContext _platformDb;
        private readonly UserManager<ApplicationUser> _userManager;

        public EmployeesController(PlatformDbContext platformDb, UserManager<ApplicationUser> userManager)
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
            return me?.OrganizationId;
        }

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

        public async Task<IActionResult> Index()
        {
            var q = await TenantUsersQueryAsync();

            var users = await q.AsNoTracking()
                .Where(u => u.IsActive)
                .OrderBy(u => u.LastName)
                .ThenBy(u => u.FirstName)
                .ToListAsync();

            var data = new List<EmployeeRowVm>();
            foreach (var u in users)
            {
                var roles = await _userManager.GetRolesAsync(u);
                data.Add(new EmployeeRowVm
                {
                    UserId = u.Id,
                    Name = $"{u.FirstName} {u.LastName}".Trim(),
                    Email = u.Email ?? "",
                    Role = roles.FirstOrDefault() ?? "Employee",
                    Status = u.IsActive ? "Active" : "Inactive",
                    OrganizationId = u.OrganizationId,
                    CreatedAt = u.CreatedAt
                });
            }

            return View(data);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Deploy(string firstName, string lastName, string email, string role, string department)
        {
            if (string.IsNullOrWhiteSpace(firstName) ||
                string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(role))
            {
                TempData["Alert"] = "Missing required fields (first name, email, role).";
                TempData["AlertType"] = "error";
                return RedirectToAction(nameof(Index));
            }

            email = email.Trim().ToLowerInvariant();
            var fName = firstName.Trim();
            var lName = (lastName ?? "").Trim();

            var existing = await _userManager.FindByEmailAsync(email);
            if (existing != null)
            {
                TempData["Alert"] = "Email is already used by another account.";
                TempData["AlertType"] = "error";
                return RedirectToAction(nameof(Index));
            }

            var me = await GetMeAsync();
            if (me == null) return Challenge();

            // NOTE: If SuperAdmin (vendor) creates users, decide the org assignment rule.
            // For now: keep orgId = 0 for vendor-created users (as your current logic does).
            var orgId = IsVendor() ? 0 : me.OrganizationId;

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                OrganizationId = orgId,
                FirstName = fName,
                LastName = lName,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            var tempPassword = "Temp@12345";
            var createRes = await _userManager.CreateAsync(user, tempPassword);

            if (!createRes.Succeeded)
            {
                TempData["Alert"] = string.Join(" | ", createRes.Errors.Select(e => e.Description));
                TempData["AlertType"] = "error";
                return RedirectToAction(nameof(Index));
            }

            if (!IsVendor())
            {
                var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "Manager", "Employee", "ProcurementOfficer" };

                if (!allowed.Contains(role))
                {
                    TempData["Alert"] = "You are not allowed to assign that role.";
                    TempData["AlertType"] = "error";
                    await _userManager.DeleteAsync(user);
                    return RedirectToAction(nameof(Index));
                }
            }

            await _userManager.AddToRoleAsync(user, role);

            var displayName = string.IsNullOrWhiteSpace(lName) ? fName : $"{fName} {lName}";
            TempData["Alert"] = $"Created employee account for {displayName}. Temporary password: {tempPassword}";
            TempData["AlertType"] = "success";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateEmployee(string id, string name, string email, string role)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                TempData["Alert"] = "Invalid employee.";
                TempData["AlertType"] = "error";
                return RedirectToAction(nameof(Index));
            }

            var target = await _platformDb.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (target == null)
            {
                TempData["Alert"] = "Employee not found.";
                TempData["AlertType"] = "error";
                return RedirectToAction(nameof(Index));
            }

            if (!await CanTouchUserAsync(target))
                return Forbid();

            if (!string.IsNullOrWhiteSpace(name))
            {
                var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                target.FirstName = parts.Length > 0 ? parts[0] : target.FirstName;
                target.LastName = parts.Length > 1 ? string.Join(" ", parts.Skip(1)) : target.LastName;
            }

            if (!string.IsNullOrWhiteSpace(email))
            {
                var newEmail = email.Trim().ToLowerInvariant();

                var existing = await _userManager.FindByEmailAsync(newEmail);
                if (existing != null && existing.Id != target.Id)
                {
                    TempData["Alert"] = "Email is already used by another account.";
                    TempData["AlertType"] = "error";
                    return RedirectToAction(nameof(Index));
                }

                target.Email = newEmail;
                target.UserName = newEmail;
            }

            if (!string.IsNullOrWhiteSpace(role))
            {
                if (!IsVendor())
                {
                    var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    { "Admin", "Manager", "Employee", "ProcurementOfficer" };

                    if (!allowed.Contains(role))
                    {
                        TempData["Alert"] = "You are not allowed to assign that role.";
                        TempData["AlertType"] = "error";
                        return RedirectToAction(nameof(Index));
                    }
                }

                var currentRoles = await _userManager.GetRolesAsync(target);
                if (!currentRoles.Contains(role))
                {
                    if (currentRoles.Count > 0)
                        await _userManager.RemoveFromRolesAsync(target, currentRoles);

                    await _userManager.AddToRoleAsync(target, role);
                }
            }

            var updateRes = await _userManager.UpdateAsync(target);
            if (!updateRes.Succeeded)
            {
                TempData["Alert"] = string.Join(" | ", updateRes.Errors.Select(e => e.Description));
                TempData["AlertType"] = "error";
                return RedirectToAction(nameof(Index));
            }

            TempData["Alert"] = "Employee updated successfully.";
            TempData["AlertType"] = "success";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(string id, string newStatus, string reason)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                TempData["Alert"] = "Invalid employee.";
                TempData["AlertType"] = "error";
                return RedirectToAction(nameof(Index));
            }

            var target = await _platformDb.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (target == null)
            {
                TempData["Alert"] = "Employee not found.";
                TempData["AlertType"] = "error";
                return RedirectToAction(nameof(Index));
            }

            if (!await CanTouchUserAsync(target))
                return Forbid();

            target.IsActive = !(
                !string.IsNullOrWhiteSpace(newStatus) &&
                newStatus.Equals("Inactive", StringComparison.OrdinalIgnoreCase)
            );

            var updateRes = await _userManager.UpdateAsync(target);
            if (!updateRes.Succeeded)
            {
                TempData["Alert"] = string.Join(" | ", updateRes.Errors.Select(e => e.Description));
                TempData["AlertType"] = "error";
                return RedirectToAction(nameof(Index));
            }

            TempData["Alert"] = $"Employee status updated: {(target.IsActive ? "Active" : "Inactive")}.";
            TempData["AlertType"] = "success";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Archive()
        {
            var q = await TenantUsersQueryAsync();

            var users = await q.AsNoTracking()
                .Where(u => !u.IsActive)
                .OrderBy(u => u.LastName)
                .ThenBy(u => u.FirstName)
                .ToListAsync();

            var data = new List<EmployeeRowVm>();
            foreach (var u in users)
            {
                var roles = await _userManager.GetRolesAsync(u);
                data.Add(new EmployeeRowVm
                {
                    UserId = u.Id,
                    Name = $"{u.FirstName} {u.LastName}".Trim(),
                    Email = u.Email ?? "",
                    Role = roles.FirstOrDefault() ?? "Employee",
                    Status = u.IsActive ? "Active" : "Inactive",
                    OrganizationId = u.OrganizationId,
                    CreatedAt = u.CreatedAt
                });
            }

            return View(data);
        }
    }

    public class EmployeeRowVm
    {
        public string UserId { get; set; } = "";
        public string Name { get; set; } = "";
        public string Email { get; set; } = "";
        public string Role { get; set; } = "";
        public string Status { get; set; } = "";
        public int OrganizationId { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}