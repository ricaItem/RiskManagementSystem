using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WEB_Sentro.Areas.Vendor.Models;
using WEB_Sentro.Data;

namespace WEB_Sentro.Areas.Vendor.Controllers
{
    [Area("Vendor")]
    [Authorize(Roles = "SuperAdmin")]
    public class OrganizationsController : Controller
    {
        private readonly PlatformDbContext _db;

        public OrganizationsController(PlatformDbContext db)
        {
            _db = db;
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

            var rows = orgs.Select(o => new OrganizationRowViewModel
            {
                OrganizationId = o.OrganizationId,
                OrgCode = o.OrgCode,
                OrgName = o.OrgName,
                PlanName = o.PlanName,
                AdminCount = adminCountLookup.GetValueOrDefault(o.OrganizationId, 0),
                RiskLoad = null,
                Status = o.Status,
                StatusColor = o.Status == "Active" ? "emerald" : o.Status == "Pending" ? "amber" : "rose"
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
    }
}