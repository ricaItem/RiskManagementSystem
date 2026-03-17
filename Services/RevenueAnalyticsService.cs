using Microsoft.EntityFrameworkCore;
using WEB_Sentro.Areas.Vendor.Models;
using WEB_Sentro.Data;

namespace WEB_Sentro.Services;

public interface IRevenueAnalyticsService
{
    Task<RevenueIndexViewModel> BuildAsync(string? range, CancellationToken ct = default);
}

public class RevenueAnalyticsService : IRevenueAnalyticsService
{
    private readonly PlatformDbContext _db;

    public RevenueAnalyticsService(PlatformDbContext db)
    {
        _db = db;
    }

    public async Task<RevenueIndexViewModel> BuildAsync(string? range, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var normalizedRange = NormalizeRange(range);
        var (start, buckets) = BuildWindow(normalizedRange, now);

        var subscriptions = await _db.Subscriptions.AsNoTracking()
            .Include(s => s.Organization)
            .Include(s => s.Plan)
            .ToListAsync(ct);

        var currentSubscriptions = subscriptions
            .GroupBy(s => s.OrganizationId)
            .Select(g => g
                .OrderByDescending(s => string.Equals(s.Status, "Active", StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(s => s.CurrentPeriodEnd)
                .ThenByDescending(s => s.CreatedAt)
                .First())
            .ToList();

        var activeSubscriptions = currentSubscriptions
            .Where(s => string.Equals(s.Status, "Active", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var mrrCentavos = activeSubscriptions.Sum(s => ToMonthlyCentavos(s.Plan.AmountCentavos, s.Plan.BillingInterval));
        var arrCentavos = mrrCentavos * 12;

        var atRiskMrrCentavos = currentSubscriptions
            .Where(s => string.Equals(s.Status, "Suspended", StringComparison.OrdinalIgnoreCase) ||
                        (string.Equals(s.Status, "Active", StringComparison.OrdinalIgnoreCase) && s.CurrentPeriodEnd <= now.AddDays(30)))
            .Sum(s => ToMonthlyCentavos(s.Plan.AmountCentavos, s.Plan.BillingInterval));

        var churnedMrrCentavos = currentSubscriptions
            .Where(s =>
                (string.Equals(s.Status, "Canceled", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(s.Status, "Suspended", StringComparison.OrdinalIgnoreCase)) &&
                (s.CanceledAt ?? s.UpdatedAt ?? s.CreatedAt) >= start)
            .Sum(s => ToMonthlyCentavos(s.Plan.AmountCentavos, s.Plan.BillingInterval));

        var expansionMrrCentavos = await _db.Payments.AsNoTracking()
            .Where(p => p.Status == "Succeeded" &&
                        (p.PaidAt ?? p.CreatedAt) >= start &&
                        p.PaymentMethod != null && EF.Functions.Like(p.PaymentMethod, "%Adjustment%") &&
                        EF.Functions.Like(p.GatewayPaymentIntentId, "%upgrade%"))
            .SumAsync(p => p.AmountCentavos, ct);

        var succeededPayments = await _db.Payments.AsNoTracking()
            .Where(p => p.Status == "Succeeded" && (p.PaidAt ?? p.CreatedAt) >= start)
            .Select(p => new
            {
                p.OrganizationId,
                p.AmountCentavos,
                PaidAt = p.PaidAt ?? p.CreatedAt
            })
            .ToListAsync(ct);

        var grossCollectedCentavos = succeededPayments.Sum(x => x.AmountCentavos);

        var unpaidInvoices = await _db.Invoices.AsNoTracking()
            .Where(i => i.Status != "Paid")
            .Select(i => new OverdueInvoiceRow
            {
                OrganizationId = i.OrganizationId,
                AmountDueCentavos = i.AmountDueCentavos,
                DueDate = i.DueDate
            })
            .ToListAsync(ct);

        var overdueInvoices = unpaidInvoices.Where(i => i.DueDate < now).ToList();
        var outstandingArCentavos = overdueInvoices.Sum(i => i.AmountDueCentavos);

        var orgNames = await _db.Organizations.AsNoTracking()
            .ToDictionaryAsync(o => o.OrganizationId, o => o.OrgName, ct);

        var trend = buckets.Select(b =>
        {
            var collected = succeededPayments
                .Where(p => p.PaidAt >= b.StartUtc && p.PaidAt < b.EndUtc)
                .Sum(p => p.AmountCentavos);

            return new RevenueTrendPointViewModel
            {
                Label = b.Label,
                CollectedCentavos = collected,
                MrrCentavos = mrrCentavos
            };
        }).ToList();

        var planMix = activeSubscriptions
            .GroupBy(s => s.Plan.DisplayName)
            .Select(g => new RevenuePlanMixRowViewModel
            {
                PlanName = g.Key,
                SubscriptionCount = g.Count(),
                MrrCentavos = g.Sum(x => ToMonthlyCentavos(x.Plan.AmountCentavos, x.Plan.BillingInterval))
            })
            .OrderByDescending(x => x.MrrCentavos)
            .ToList();

        var topContributors = succeededPayments
            .GroupBy(x => x.OrganizationId)
            .Select(g => new RevenueTopOrganizationRowViewModel
            {
                OrganizationId = g.Key,
                OrganizationName = orgNames.GetValueOrDefault(g.Key, $"Org #{g.Key}"),
                CollectedCentavos = g.Sum(x => x.AmountCentavos)
            })
            .OrderByDescending(x => x.CollectedCentavos)
            .Take(10)
            .ToList();

        var agingRows = new List<RevenueAgingRowViewModel>
        {
            BuildAgingRow("0-30d", overdueInvoices, now, 0, 30),
            BuildAgingRow("31-60d", overdueInvoices, now, 31, 60),
            BuildAgingRow("61+d", overdueInvoices, now, 61, null)
        };

        var renewalRiskCount = activeSubscriptions.Count(s => s.CurrentPeriodEnd <= now.AddDays(30));

        return new RevenueIndexViewModel
        {
            SelectedRange = normalizedRange,
            GrossCollectedDisplay = FormatCurrency(grossCollectedCentavos),
            MrrDisplay = FormatCurrency(mrrCentavos),
            ArrDisplay = FormatCurrency(arrCentavos),
            OutstandingArDisplay = FormatCurrency(outstandingArCentavos),
            AtRiskMrrDisplay = FormatCurrency(atRiskMrrCentavos),
            ChurnedMrrDisplay = FormatCurrency(churnedMrrCentavos),
            ExpansionMrrDisplay = FormatCurrency(expansionMrrCentavos),
            ActiveSubscriptionsCount = activeSubscriptions.Count,
            RenewalRiskCount = renewalRiskCount,
            Trend = trend,
            PlanMix = planMix,
            TopOrganizations = topContributors,
            Aging = agingRows
        };
    }

    private static RevenueAgingRowViewModel BuildAgingRow(string label, List<OverdueInvoiceRow> overdueInvoices, DateTime now, int minDays, int? maxDays)
    {
        var filtered = overdueInvoices.Where(i =>
        {
            var age = (now.Date - i.DueDate.Date).Days;
            if (age < minDays)
            {
                return false;
            }

            if (maxDays.HasValue && age > maxDays.Value)
            {
                return false;
            }

            return true;
        }).ToList();

        return new RevenueAgingRowViewModel
        {
            Bucket = label,
            InvoiceCount = filtered.Count,
            AmountCentavos = filtered.Sum(x => x.AmountDueCentavos)
        };
    }

    private static long ToMonthlyCentavos(long amountCentavos, string? billingInterval)
    {
        if (string.Equals(billingInterval, "year", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(billingInterval, "yearly", StringComparison.OrdinalIgnoreCase))
        {
            return amountCentavos / 12;
        }

        return amountCentavos;
    }

    private static string NormalizeRange(string? range)
    {
        return range?.Trim().ToLowerInvariant() switch
        {
            "7d" => "7d",
            "30d" => "30d",
            "90d" => "90d",
            _ => "12m"
        };
    }

    private static (DateTime StartUtc, List<RangeBucket> Buckets) BuildWindow(string range, DateTime now)
    {
        return range switch
        {
            "7d" => BuildDailyBuckets(now, 7),
            "30d" => BuildDailyBuckets(now, 6),
            "90d" => BuildMonthlyBuckets(now, 3),
            _ => BuildMonthlyBuckets(now, 12)
        };
    }

    private static (DateTime StartUtc, List<RangeBucket> Buckets) BuildDailyBuckets(DateTime now, int days)
    {
        var end = now.Date.AddDays(1);
        var start = end.AddDays(-days);
        var buckets = new List<RangeBucket>();

        for (var i = 0; i < days; i++)
        {
            var bucketStart = start.AddDays(i);
            var bucketEnd = bucketStart.AddDays(1);
            buckets.Add(new RangeBucket(bucketStart, bucketEnd, bucketStart.ToString("MM-dd")));
        }

        return (start, buckets);
    }

    private static (DateTime StartUtc, List<RangeBucket> Buckets) BuildMonthlyBuckets(DateTime now, int months)
    {
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var start = monthStart.AddMonths(-(months - 1));
        var buckets = new List<RangeBucket>();

        for (var i = 0; i < months; i++)
        {
            var bucketStart = start.AddMonths(i);
            var bucketEnd = bucketStart.AddMonths(1);
            buckets.Add(new RangeBucket(bucketStart, bucketEnd, bucketStart.ToString("MMM yy")));
        }

        return (start, buckets);
    }

    private static string FormatCurrency(long centavos)
    {
        return centavos % 100 == 0
            ? $"PHP {centavos / 100:N0}"
            : $"PHP {centavos / 100.0:N2}";
    }

    private readonly record struct RangeBucket(DateTime StartUtc, DateTime EndUtc, string Label);
    private readonly record struct OverdueInvoiceRow(int OrganizationId, long AmountDueCentavos, DateTime DueDate);
}
