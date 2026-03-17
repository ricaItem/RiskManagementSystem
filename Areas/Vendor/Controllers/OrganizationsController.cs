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
    public class OrganizationsController : Controller
    {
        private readonly PlatformDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAuditService _auditService;
        private readonly IOrganizationGovernanceService _governanceService;

        public OrganizationsController(
            PlatformDbContext db,
            UserManager<ApplicationUser> userManager,
            IAuditService auditService,
            IOrganizationGovernanceService governanceService)
        {
            _db = db;
            _userManager = userManager;
            _auditService = auditService;
            _governanceService = governanceService;
        }

        private async Task<string> GetActorIdAsync()
        {
            var me = await _userManager.GetUserAsync(User);
            return me?.Id ?? "system";
        }

        public async Task<IActionResult> Index(string? search, string? plan, string? status, CancellationToken ct = default)
        {
            var adminRole = await _db.Roles.FirstOrDefaultAsync(r => r.Name == "Admin", ct);
            var adminUserIds = adminRole != null
                ? await _db.UserRoles.Where(ur => ur.RoleId == adminRole.Id).Select(ur => ur.UserId).ToListAsync(ct)
                : new List<string>();
            var adminCountByOrg = await _db.Users
                .Where(u => adminUserIds.Contains(u.Id))
                .GroupBy(u => u.OrganizationId)
                .Select(g => new { OrgId = g.Key, Count = g.Count() })
                .ToListAsync(ct);
            var adminCountLookup = adminCountByOrg.ToDictionary(x => x.OrgId, x => x.Count);

            var totalRevenueCentavos = await _db.Payments
                .Where(p => p.Status == "Succeeded")
                .SumAsync(p => p.AmountCentavos, ct);
            var totalRevenueDisplay = totalRevenueCentavos % 100 == 0
                ? $"₱{totalRevenueCentavos / 100:N0}"
                : $"₱{totalRevenueCentavos / 100.0:N2}";

            var query = _db.Organizations.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                query = query.Where(o => o.OrgName.Contains(s) || o.OrgCode.Contains(s));
            }
            if (!string.IsNullOrWhiteSpace(plan))
                query = query.Where(o => o.PlanName == plan);
            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(o => o.Status == status);

            var orgs = await query.OrderBy(o => o.OrgName).ToListAsync(ct);
            var activeTenants = await _db.Organizations.CountAsync(o => o.Status == "Active", ct);

            var orgIds = orgs.Select(o => o.OrganizationId).ToList();
            var subscriptions = await _db.Subscriptions.AsNoTracking()
                .Include(s => s.Plan)
                .Where(s => orgIds.Contains(s.OrganizationId))
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync(ct);
            var latestSubscriptionByOrg = subscriptions
                .GroupBy(s => s.OrganizationId)
                .ToDictionary(g => g.Key, g => g.First());

            var rows = orgs.Select(o => new OrganizationRowViewModel
            {
                OrganizationId = o.OrganizationId,
                OrgCode = o.OrgCode,
                OrgName = o.OrgName,
                PlanName = o.PlanName,
                AdminCount = adminCountLookup.GetValueOrDefault(o.OrganizationId, 0),
                RiskLoad = null,
                Status = o.Status,
                StatusColor = string.Equals(o.Status, "Active", StringComparison.OrdinalIgnoreCase)
                    ? "emerald"
                    : string.Equals(o.Status, "Pending", StringComparison.OrdinalIgnoreCase)
                        ? "amber"
                        : string.Equals(o.Status, "Suspended", StringComparison.OrdinalIgnoreCase)
                            ? "rose"
                            : "slate",
                SubscriptionStatus = latestSubscriptionByOrg.TryGetValue(o.OrganizationId, out var sub)
                    ? sub.Status
                    : "No Subscription",
                NextBillingDisplay = latestSubscriptionByOrg.TryGetValue(o.OrganizationId, out var billingSub)
                    ? billingSub.CurrentPeriodEnd.ToString("yyyy-MM-dd")
                    : "-"
            }).ToList();

            var model = new OrganizationsIndexViewModel
            {
                Search = search,
                PlanFilter = plan,
                StatusFilter = status,
                Organizations = rows,
                TotalCount = rows.Count,
                TotalRevenueDisplay = totalRevenueDisplay,
                ActiveTenantsCount = activeTenants,
                SystemAvailabilityDisplay = "—"
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(OrganizationCreateViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Invalid form data.";
                return RedirectToAction(nameof(Index));
            }

            var result = await _governanceService.ProvisionOrganizationAsync(
                model,
                await GetActorIdAsync(),
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                HttpContext.RequestAborted);

            TempData[result.Succeeded ? "Success" : "Error"] = result.Message;

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateSubscriptionPlan(int organizationId, string planCode, CancellationToken ct = default)
        {
            var result = await _governanceService.UpdateSubscriptionPlanAsync(
                organizationId,
                planCode,
                await GetActorIdAsync(),
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                ct);

            TempData[result.Succeeded ? "Success" : "Error"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleSubscriptionStatus(int organizationId, CancellationToken ct = default)
        {
            var result = await _governanceService.ToggleSubscriptionStatusAsync(
                organizationId,
                await GetActorIdAsync(),
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                ct);

            TempData[result.Succeeded ? "Success" : "Error"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var org = await _db.Organizations.FindAsync(id);
            if (org == null) return NotFound();

            var model = new OrganizationEditViewModel
            {
                OrganizationId = org.OrganizationId,
                OrgName = org.OrgName,
                OrgCode = org.OrgCode,
                PlanName = org.PlanName,
                Status = org.Status,
                Website = org.Website,
                PrimaryEmail = org.PrimaryEmail,
                PrimaryPhone = org.PrimaryPhone,
                AddressLine = org.AddressLine,
                City = org.City,
                Country = org.Country,
                TaxId = org.TaxId
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(int id, OrganizationEditViewModel model)
        {
            if (id != model.OrganizationId) return BadRequest();
            
            if (!ModelState.IsValid) return View("Edit", model);

            var org = await _db.Organizations.FindAsync(id);
            if (org == null) return NotFound();

            org.OrgName = model.OrgName;
            org.OrgCode = model.OrgCode;
            org.PlanName = model.PlanName;
            org.Status = model.Status;
            org.Website = model.Website;
            org.PrimaryEmail = model.PrimaryEmail;
            org.PrimaryPhone = model.PrimaryPhone;
            org.AddressLine = model.AddressLine;
            org.City = model.City;
            org.Country = model.Country;
            org.TaxId = model.TaxId;
            org.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            await _auditService.LogAsync(
                org.OrganizationId,
                await GetActorIdAsync(),
                "Organization",
                org.OrganizationId,
                "OrganizationUpdated",
                $"Updated organization profile for {org.OrgName}",
                "Info",
                HttpContext.Connection.RemoteIpAddress?.ToString());
            TempData["Success"] = "Organization updated successfully.";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var result = await _governanceService.ToggleOrganizationStatusAsync(
                id,
                await GetActorIdAsync(),
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                HttpContext.RequestAborted);

            TempData[result.Succeeded ? "Success" : "Error"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _governanceService.ArchiveOrganizationAsync(
                id,
                await GetActorIdAsync(),
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                HttpContext.RequestAborted);

            TempData[result.Succeeded ? "Success" : "Error"] = result.Message;
            return RedirectToAction(nameof(Index));
        }
    }
}
