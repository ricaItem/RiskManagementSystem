using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WEB_Sentro.Areas.Vendor.Models;
using WEB_Sentro.Data;

namespace WEB_Sentro.Areas.Vendor.Controllers
{
    [Area("Vendor")]
    [Authorize(Policy = "SuperAdminOnly")]
    public class BillingController : Controller
    {
        private readonly PlatformDbContext _db;

        public BillingController(PlatformDbContext db)
        {
            _db = db;
        }

        public async Task<IActionResult> Index(CancellationToken ct = default)
        {
            var subscriptions = await _db.Subscriptions.AsNoTracking()
                .Include(s => s.Organization)
                .Include(s => s.Plan)
                .OrderByDescending(s => s.CurrentPeriodEnd)
                .ToListAsync(ct);

            var currentSubscriptions = subscriptions
                .GroupBy(s => s.OrganizationId)
                .Select(g => g
                    .OrderByDescending(s => string.Equals(s.Status, "Active", StringComparison.OrdinalIgnoreCase))
                    .ThenByDescending(s => s.CurrentPeriodEnd)
                    .ThenByDescending(s => s.CreatedAt)
                    .First())
                .ToList();

            var totalRevenueCentavos = await _db.Payments.AsNoTracking()
                .Where(p => p.Status == "Succeeded")
                .SumAsync(p => p.AmountCentavos, ct);

            var mrrCentavos = currentSubscriptions
                .Where(s => string.Equals(s.Status, "Active", StringComparison.OrdinalIgnoreCase))
                .Sum(s => s.Plan.AmountCentavos);

            var pendingRenewals = currentSubscriptions.Count(s =>
                s.CurrentPeriodEnd <= DateTime.UtcNow.AddDays(30) &&
                string.Equals(s.Status, "Active", StringComparison.OrdinalIgnoreCase));

            var usersByOrg = await _db.Users.AsNoTracking()
                .Where(u => u.IsActive)
                .GroupBy(u => u.OrganizationId)
                .Select(g => new { OrgId = g.Key, Count = g.Count() })
                .ToListAsync(ct);
            var userCountLookup = usersByOrg.ToDictionary(x => x.OrgId, x => x.Count);

            var rows = currentSubscriptions.Select(s => new BillingSubscriptionRowViewModel
            {
                OrganizationId = s.OrganizationId,
                OrganizationName = s.Organization.OrgName,
                PlanName = s.Plan.DisplayName,
                ActiveUsers = userCountLookup.GetValueOrDefault(s.OrganizationId, 0),
                SeatLimit = s.Plan.MaxAdminSeats,
                NextRenewalAt = s.CurrentPeriodEnd,
                NextRenewalDisplay = s.CurrentPeriodEnd.ToString("yyyy-MM-dd"),
                AmountDisplay = FormatCurrency(s.Plan.AmountCentavos),
                SubscriptionStatus = s.Status
            }).ToList();

            var model = new BillingIndexViewModel
            {
                TotalRevenueDisplay = FormatCurrency(totalRevenueCentavos),
                MonthlyRecurringRevenueDisplay = FormatCurrency(mrrCentavos),
                ActiveSubscriptionsCount = currentSubscriptions.Count(s => string.Equals(s.Status, "Active", StringComparison.OrdinalIgnoreCase)),
                PendingRenewalsCount = pendingRenewals,
                Subscriptions = rows
            };

            return View(model);
        }

        private static string FormatCurrency(long centavos)
        {
            if (centavos % 100 == 0)
            {
                return $"PHP {centavos / 100:N0}";
            }

            return $"PHP {centavos / 100.0:N2}";
        }
    }
}
