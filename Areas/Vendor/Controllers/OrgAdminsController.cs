using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WEB_Sentro.Areas.Vendor.Models;
using WEB_Sentro.Data;
using WEB_Sentro.Models.Identity;
using WEB_Sentro.Services;

namespace WEB_Sentro.Areas.Vendor.Controllers
{
    [Area("Vendor")]
    [Authorize(Policy = "SuperAdminOnly")]
    public class OrgAdminsController : Controller
    {
        private readonly PlatformDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IOrganizationGovernanceService _governanceService;

        public OrgAdminsController(PlatformDbContext db, UserManager<ApplicationUser> userManager, IOrganizationGovernanceService governanceService)
        {
            _db = db;
            _userManager = userManager;
            _governanceService = governanceService;
        }

        private async Task<string> GetActorIdAsync()
        {
            var me = await _userManager.GetUserAsync(User);
            return me?.Id ?? "system";
        }

        public async Task<IActionResult> Index(string? search, int? orgId, CancellationToken ct = default)
        {
            var adminRole = await _db.Roles.FirstOrDefaultAsync(r => r.Name == "Admin", ct);
            if (adminRole == null)
            {
                return View(new OrgAdminsIndexViewModel
                {
                    OrganizationOptions = await _db.Organizations.AsNoTracking().OrderBy(o => o.OrgName).Select(o => new OrganizationOptionViewModel { OrganizationId = o.OrganizationId, OrgName = o.OrgName }).ToListAsync(ct)
                });
            }

            var adminUserIds = await _db.UserRoles.Where(ur => ur.RoleId == adminRole.Id).Select(ur => ur.UserId).ToListAsync(ct);
            var query = _db.Users.AsNoTracking()
                .Where(u => adminUserIds.Contains(u.Id))
                .Where(u => u.OrganizationId > 0);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                query = query.Where(u =>
                    (u.FirstName != null && u.FirstName.Contains(s)) ||
                    (u.LastName != null && u.LastName.Contains(s)) ||
                    (u.Email != null && u.Email.Contains(s)));
            }
            if (orgId.HasValue && orgId.Value > 0)
                query = query.Where(u => u.OrganizationId == orgId.Value);

            var users = await query.OrderBy(u => u.LastName).ThenBy(u => u.FirstName).ToListAsync(ct);
            var orgIds = users.Select(u => u.OrganizationId).Distinct().ToList();
            var orgs = await _db.Organizations.AsNoTracking().Where(o => orgIds.Contains(o.OrganizationId)).ToDictionaryAsync(o => o.OrganizationId, o => o.OrgName, ct);
            var orgOptions = await _db.Organizations.AsNoTracking().OrderBy(o => o.OrgName).Select(o => new OrganizationOptionViewModel { OrganizationId = o.OrganizationId, OrgName = o.OrgName }).ToListAsync(ct);

            var admins = users.Select(u => new AdminRowViewModel
            {
                UserId = u.Id,
                Name = string.IsNullOrWhiteSpace($"{u.FirstName ?? ""} {u.LastName ?? ""}".Trim())
                    ? (u.Email ?? u.UserName ?? "—")
                    : $"{u.FirstName ?? ""} {u.LastName ?? ""}".Trim(),
                Email = u.Email ?? "—",
                OrganizationId = u.OrganizationId,
                OrganizationName = orgs.GetValueOrDefault(u.OrganizationId, "—"),
                Role = "Admin",
                LastLoginDisplay = u.LastLoginAt.HasValue ? FormatRelativeTime(u.LastLoginAt.Value) : "—",
                IsActive = u.IsActive
            }).ToList();

            var model = new OrgAdminsIndexViewModel
            {
                Search = search,
                OrganizationIdFilter = orgId,
                Admins = admins,
                OrganizationOptions = orgOptions
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int organizationId, string fullName, string email, CancellationToken ct = default)
        {
            var result = await _governanceService.CreateOrgAdminAsync(
                organizationId,
                fullName,
                email,
                await GetActorIdAsync(),
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                ct);

            TempData[result.Succeeded ? "Success" : "Error"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(string userId, CancellationToken ct = default)
        {
            var result = await _governanceService.ToggleOrgAdminStatusAsync(
                userId,
                await GetActorIdAsync(),
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                ct);

            TempData[result.Succeeded ? "Success" : "Error"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(string userId)
        {
            var result = await _governanceService.SendOrgAdminPasswordResetAsync(
                userId,
                await GetActorIdAsync(),
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                HttpContext.RequestAborted);

            TempData[result.Succeeded ? "Success" : "Error"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        private static string FormatRelativeTime(DateTime value)
        {
            var diff = DateTime.UtcNow - value.ToUniversalTime();
            if (diff.TotalMinutes < 1) return "Just now";
            if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes} min(s) ago";
            if (diff.TotalHours < 24) return $"{(int)diff.TotalHours} hour(s) ago";
            if (diff.TotalDays < 7) return $"{(int)diff.TotalDays} day(s) ago";
            return value.ToString("yyyy-MM-dd");
        }
    }
}
