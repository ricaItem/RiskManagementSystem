using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using WEB_Sentro.Data;
using WEB_Sentro.Services;
using WEB_Sentro.Models.Identity;

namespace Web_Sentro.Areas.Client.Controllers
{
    [Area("Client")]
    [Authorize(Policy = "MainAdminOnly")]
    public class EmployeesController : Controller
    {
        private readonly PlatformDbContext _platformDb;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAuditService _auditService;

        public EmployeesController(PlatformDbContext platformDb, UserManager<ApplicationUser> userManager, IAuditService auditService)
        {
            _platformDb = platformDb;
            _userManager = userManager;
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
                    ProfileImagePath = u.ProfileImagePath,
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
        [Authorize(Policy = "MainAdminOnly")]
        public async Task<IActionResult> Deploy(string firstName, string lastName, string email, string role, string department)
        {
            if (string.IsNullOrWhiteSpace(firstName) ||
                string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(role))
            {
                TempData["ToastError"] = "Missing required fields (first name, email, role).";
                return RedirectToAction(nameof(Index));
            }

            email = email.Trim().ToLowerInvariant();
            var fName = firstName.Trim();
            var lName = (lastName ?? "").Trim();

            var existing = await _userManager.FindByEmailAsync(email);
            if (existing != null)
            {
                TempData["ToastError"] = "Email is already used by another account.";
                return RedirectToAction(nameof(Index));
            }

            var me = await GetMeAsync();
            if (me == null) return Challenge();

            // NOTE: If SuperAdmin (vendor) creates users, decide the org assignment rule.
            // For now: keep orgId = 0 for vendor-created users (as your current logic does).
            var orgId = IsVendor() ? 0 : me.OrganizationId;

            // Enforce Plan Limits (Max Seats)
            if (!IsVendor() && orgId > 0)
            {
                var org = await _platformDb.Organizations.AsNoTracking()
                    .FirstOrDefaultAsync(o => o.OrganizationId == orgId);

                if (org != null)
                {
                    // Find the plan by code (PlanName)
                    var plan = await _platformDb.Plans.AsNoTracking()
                        .FirstOrDefaultAsync(p => p.Code == org.PlanName);

                    if (plan != null && plan.MaxAdminSeats.HasValue)
                    {
                        // Count active users in this org
                        var currentCount = await _platformDb.Users
                            .CountAsync(u => u.OrganizationId == orgId && u.IsActive);

                        if (currentCount >= plan.MaxAdminSeats.Value)
                        {
                            TempData["ToastError"] = $"Plan limit reached. Your current plan ({plan.DisplayName}) allows a maximum of {plan.MaxAdminSeats.Value} active users. Please upgrade your plan to add more employees.";
                            return RedirectToAction(nameof(Index));
                        }
                    }
                }
            }

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

            var tempPassword = "Temp@12345678";
            var createRes = await _userManager.CreateAsync(user, tempPassword);

            if (!createRes.Succeeded)
            {
                TempData["ToastError"] = string.Join(" | ", createRes.Errors.Select(e => e.Description));
                return RedirectToAction(nameof(Index));
            }

            if (!IsVendor())
            {
                var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "Admin", "Manager", "Employee", "ProcurementOfficer", "RiskManager" };

                if (!allowed.Contains(role))
                {
                    TempData["ToastError"] = "You are not allowed to assign that role.";
                    await _userManager.DeleteAsync(user);
                    return RedirectToAction(nameof(Index));
                }
            }

            await _userManager.AddToRoleAsync(user, role);

            await _auditService.LogAsync(
                user.OrganizationId,
                me.Id,
                "Employee",
                0, // No int ID for users
                "EmployeeCreated",
                $"Created employee {user.Email} with role {role}",
                "Info",
                HttpContext.Connection.RemoteIpAddress?.ToString()
            );

            var displayName = string.IsNullOrWhiteSpace(lName) ? fName : $"{fName} {lName}";
            TempData["ToastSuccess"] = $"Created employee account for {displayName}. Temporary password: {tempPassword}";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "MainAdminOnly")]
        public async Task<IActionResult> UpdateEmployee(string id, string name, string email, string role)
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
                    TempData["ToastError"] = "Email is already used by another account.";
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
                    { "Admin", "Manager", "Employee", "ProcurementOfficer", "RiskManager" };

                    if (!allowed.Contains(role))
                    {
                        TempData["ToastError"] = "You are not allowed to assign that role.";
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
                TempData["ToastError"] = string.Join(" | ", updateRes.Errors.Select(e => e.Description));
                return RedirectToAction(nameof(Index));
            }

            var me = await GetMeAsync();
            if (me != null)
            {
                await _auditService.LogAsync(
                    target.OrganizationId,
                    me.Id,
                    "Employee",
                    0,
                    "EmployeeUpdated",
                    $"Updated employee {target.Email}",
                    "Info",
                    HttpContext.Connection.RemoteIpAddress?.ToString()
                );
            }

            TempData["ToastSuccess"] = "Employee updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "MainAdminOnly")]
        public async Task<IActionResult> UpdateStatus(string id, string newStatus, string reason)
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

            bool intendedActive = !(
                !string.IsNullOrWhiteSpace(newStatus) &&
                newStatus.Equals("Inactive", StringComparison.OrdinalIgnoreCase)
            );

            if (intendedActive && !target.IsActive)
            {
                // We are activating an inactive user. Check Plan Limits.
                if (!IsVendor() && target.OrganizationId > 0)
                {
                    var org = await _platformDb.Organizations.AsNoTracking()
                        .FirstOrDefaultAsync(o => o.OrganizationId == target.OrganizationId);

                    if (org != null)
                    {
                        var plan = await _platformDb.Plans.AsNoTracking()
                            .FirstOrDefaultAsync(p => p.Code == org.PlanName);

                        if (plan != null && plan.MaxAdminSeats.HasValue)
                        {
                            var currentCount = await _platformDb.Users
                                .CountAsync(u => u.OrganizationId == target.OrganizationId && u.IsActive);

                            if (currentCount >= plan.MaxAdminSeats.Value)
                            {
                                TempData["ToastError"] = $"Plan limit reached. Cannot activate user. Your plan ({plan.DisplayName}) allows max {plan.MaxAdminSeats.Value} active users.";
                                return RedirectToAction(nameof(Index));
                            }
                        }
                    }
                }
            }

            target.IsActive = intendedActive;

            var updateRes = await _userManager.UpdateAsync(target);
            if (!updateRes.Succeeded)
            {
                TempData["ToastError"] = string.Join(" | ", updateRes.Errors.Select(e => e.Description));
                return RedirectToAction(nameof(Index));
            }

            var me = await GetMeAsync();
            if (me != null)
            {
                await _auditService.LogAsync(
                    target.OrganizationId,
                    me.Id,
                    "Employee",
                    0,
                    "EmployeeStatusChanged",
                    $"Status changed to {target.IsActive} for {target.Email}",
                    "Info",
                    HttpContext.Connection.RemoteIpAddress?.ToString()
                );
            }

            TempData["ToastSuccess"] = $"Employee status updated: {(target.IsActive ? "Active" : "Inactive")}.";
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Policy = "MainAdminOnly")]
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
                    ProfileImagePath = u.ProfileImagePath,
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
        public string? ProfileImagePath { get; set; }
        public string Role { get; set; } = "";
        public string Status { get; set; } = "";
        public int OrganizationId { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
