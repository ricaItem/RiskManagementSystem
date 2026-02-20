using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WEB_Sentro.Data;
using WEB_Sentro.Models.Identity;

namespace Web_Sentro.Areas.Client.Controllers
{
    [Area("Client")]
    [Authorize(Policy = "AdminOrVendor")] // SuperAdmin (vendor) OR Admin (org admin)
    public class EmployeesController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public EmployeesController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
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

        // GET: /Client/Employees
        public async Task<IActionResult> Index()
        {
            var q = await TenantUsersQueryAsync();

            // Show active employees by default (archive = inactive)
            var users = await q.AsNoTracking()
                .Where(u => u.IsActive)
                .OrderBy(u => u.LastName)
                .ThenBy(u => u.FirstName)
                .ToListAsync();

            // If your UI needs Role/Department etc:
            // You can fetch roles per user (slower, but OK for small lists).

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

        // POST: /Client/Employees/Deploy
        // This creates a NEW login account + assigns role + org id
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Deploy(string firstName, string lastName, string email, string role, string department)
        {
            if (string.IsNullOrWhiteSpace(firstName) ||
                string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(role))
            {
                TempData["Alert"] = "Missing required fields (first name, email, role).";
                return RedirectToAction(nameof(Index));
            }

            email = email.Trim().ToLowerInvariant();
            var fName = firstName.Trim();
            var lName = (lastName ?? "").Trim();

            var existing = await _userManager.FindByEmailAsync(email);
            if (existing != null)
            {
                TempData["Alert"] = "Email is already used by another account.";
                return RedirectToAction(nameof(Index));
            }

            var me = await GetMeAsync();
            if (me == null) return Challenge();

            // Tenant enforcement: Admin can only create inside their org
            var orgId = IsVendor() ? 0 : me.OrganizationId;

            // Create user
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

            // TEMP password  you can change this to something generated.
            // If your UI collects password, pass it instead.
            var tempPassword = "Temp@12345"; // change later
            var createRes = await _userManager.CreateAsync(user, tempPassword);

            if (!createRes.Succeeded)
            {
                TempData["Alert"] = string.Join(" | ", createRes.Errors.Select(e => e.Description));
                return RedirectToAction(nameof(Index));
            }

            // Ensure role is valid (optional safety)
            // Only allow these roles to be assigned by Admin.
            // SuperAdmin can assign anything.
            if (!IsVendor())
            {
                var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {  "Manager", "Employee", "ProcurementOfficer" };

                if (!allowed.Contains(role))
                {
                    TempData["Alert"] = "You are not allowed to assign that role.";
                    await _userManager.DeleteAsync(user);
                    return RedirectToAction(nameof(Index));
                }
            }

            await _userManager.AddToRoleAsync(user, role);

            var displayName = string.IsNullOrWhiteSpace(lName) ? fName : $"{fName} {lName}";
            TempData["Alert"] =
                $"Created employee account for {displayName}. Temporary password: {tempPassword}";

            return RedirectToAction(nameof(Index));
        }

        // POST: /Client/Employees/UpdateEmployee
        // Update profile basics
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateEmployee(string id, string name, string email, string role, string bio)
        {
            // bio/department not stored yet; ignore unless you add fields/table
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

            // Update name
            if (!string.IsNullOrWhiteSpace(name))
            {
                var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                target.FirstName = parts.Length > 0 ? parts[0] : target.FirstName;
                target.LastName = parts.Length > 1 ? string.Join(" ", parts.Skip(1)) : target.LastName;
            }

            // Update email
            if (!string.IsNullOrWhiteSpace(email))
            {
                var newEmail = email.Trim().ToLowerInvariant();

                // prevent duplicates
                var existing = await _userManager.FindByEmailAsync(newEmail);
                if (existing != null && existing.Id != target.Id)
                {
                    TempData["Alert"] = "Email is already used by another account.";
                    return RedirectToAction(nameof(Index));
                }

                target.Email = newEmail;
                target.UserName = newEmail;
            }

            // Update role (single role model)
            if (!string.IsNullOrWhiteSpace(role))
            {
                if (!IsVendor())
                {
                    var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    { "Admin", "Manager", "Employee", "ProcurementOfficer" };

                    if (!allowed.Contains(role))
                    {
                        TempData["Alert"] = "You are not allowed to assign that role.";
                        return RedirectToAction(nameof(Index));
                    }
                }

                var currentRoles = await _userManager.GetRolesAsync(target);
                // remove all then add one (your system seems like one primary role per user)
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
                return RedirectToAction(nameof(Index));
            }

            TempData["Alert"] = "Employee updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        // POST: /Client/Employees/UpdateStatus
        // Archive/activate (soft delete)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(string id, string newStatus, string reason)
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

            // Treat "Inactive" as archive
            if (!string.IsNullOrWhiteSpace(newStatus) &&
                newStatus.Equals("Inactive", StringComparison.OrdinalIgnoreCase))
            {
                target.IsActive = false;
            }
            else
            {
                target.IsActive = true;
            }

            var updateRes = await _userManager.UpdateAsync(target);
            if (!updateRes.Succeeded)
            {
                TempData["Alert"] = string.Join(" | ", updateRes.Errors.Select(e => e.Description));
                return RedirectToAction(nameof(Index));
            }

            TempData["Alert"] = $"Employee status updated: {(target.IsActive ? "Active" : "Inactive")}.";
            return RedirectToAction(nameof(Index));
        }

        // Optional: Archive page list
        // GET: /Client/Employees/Archive
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

    // Simple VM for your UI table
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