using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using WEB_Sentro.Data;
using WEB_Sentro.Data.Entities;

namespace WEB_Sentro.Services;

public class OrganizationAnalyticsSnapshotRefreshService
{
    private static readonly RangeDef[] Ranges =
    {
        new("30d", "Last 30 Days", 30),
        new("90d", "Last 90 Days", 90),
        new("1y", "Last 12 Months", 365)
    };

    private readonly PlatformDbContext _platformDb;
    private readonly ITenantDbFactory _tenantDbFactory;
    private readonly ILogger<OrganizationAnalyticsSnapshotRefreshService> _logger;

    public OrganizationAnalyticsSnapshotRefreshService(
        PlatformDbContext platformDb,
        ITenantDbFactory tenantDbFactory,
        ILogger<OrganizationAnalyticsSnapshotRefreshService> logger)
    {
        _platformDb = platformDb;
        _tenantDbFactory = tenantDbFactory;
        _logger = logger;
    }

    public async Task RefreshAsync(int maxParallelTenants, CancellationToken ct)
    {
        var nowUtc = DateTime.UtcNow;
        var orgs = await _platformDb.Organizations.AsNoTracking()
            .Select(o => new { o.OrganizationId, o.OrgName, o.PlanName })
            .ToListAsync(ct);

        if (orgs.Count == 0)
            return;

        var orgIds = orgs.Select(o => o.OrganizationId).ToList();
        var orgNameById = orgs.ToDictionary(x => x.OrganizationId, x => x.OrgName);

        var subscriptions = await _platformDb.Subscriptions.AsNoTracking()
            .Include(s => s.Plan)
            .Where(s => orgIds.Contains(s.OrganizationId))
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new
            {
                s.OrganizationId,
                s.Status,
                PlanName = s.Plan.DisplayName,
                s.CurrentPeriodEnd,
                SeatLimit = s.Plan.MaxAdminSeats
            })
            .ToListAsync(ct);
        var latestSubscriptionByOrg = subscriptions
            .GroupBy(x => x.OrganizationId)
            .ToDictionary(g => g.Key, g => g.First());

        var activeUsersByOrg = await _platformDb.Users.AsNoTracking()
            .Where(u => orgIds.Contains(u.OrganizationId) && u.IsActive)
            .GroupBy(u => u.OrganizationId)
            .Select(g => new { OrgId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.OrgId, x => x.Count, ct);

        var lastLoginByOrg = await _platformDb.Users.AsNoTracking()
            .Where(u => orgIds.Contains(u.OrganizationId) && u.LastLoginAt.HasValue)
            .GroupBy(u => u.OrganizationId)
            .Select(g => new { OrgId = g.Key, LastLoginAt = g.Max(x => x.LastLoginAt) })
            .Where(x => x.LastLoginAt.HasValue)
            .ToDictionaryAsync(x => x.OrgId, x => x.LastLoginAt!.Value, ct);

        var rangeLoginStats = new Dictionary<string, Dictionary<int, (int current, int previous)>>();
        foreach (var range in Ranges)
        {
            var fromUtc = nowUtc.AddDays(-range.Days);
            var previousFromUtc = fromUtc.AddDays(-range.Days);

            var currentLogins = await _platformDb.Users.AsNoTracking()
                .Where(u => orgIds.Contains(u.OrganizationId) && u.LastLoginAt.HasValue && u.LastLoginAt >= fromUtc)
                .GroupBy(u => u.OrganizationId)
                .Select(g => new { OrgId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.OrgId, x => x.Count, ct);

            var previousLogins = await _platformDb.Users.AsNoTracking()
                .Where(u => orgIds.Contains(u.OrganizationId) && u.LastLoginAt.HasValue && u.LastLoginAt >= previousFromUtc && u.LastLoginAt < fromUtc)
                .GroupBy(u => u.OrganizationId)
                .Select(g => new { OrgId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.OrgId, x => x.Count, ct);

            var merged = new Dictionary<int, (int current, int previous)>();
            foreach (var orgId in orgIds)
            {
                merged[orgId] = (currentLogins.GetValueOrDefault(orgId, 0), previousLogins.GetValueOrDefault(orgId, 0));
            }
            rangeLoginStats[range.Key] = merged;
        }

        var tenantMetricsByOrg = await ComputeTenantMetricsAsync(orgIds, nowUtc, Math.Max(1, maxParallelTenants), ct);

        var snapshotRows = new List<OrganizationAnalyticsSnapshot>(orgIds.Count * Ranges.Length);

        foreach (var orgId in orgIds)
        {
            var orgName = orgNameById.GetValueOrDefault(orgId, "-");
            var orgFallbackPlan = orgs.First(x => x.OrganizationId == orgId).PlanName;
            var latestSub = latestSubscriptionByOrg.GetValueOrDefault(orgId);
            var userCount = activeUsersByOrg.GetValueOrDefault(orgId, 0);
            var seatLimit = latestSub?.SeatLimit;
            var seatUtilization = seatLimit.HasValue && seatLimit.Value > 0
                ? Math.Max(0, (int)Math.Round(userCount * 100.0 / seatLimit.Value))
                : 0;

            var tenantMetrics = tenantMetricsByOrg.GetValueOrDefault(orgId) ?? OrgTenantMetrics.Empty;

            foreach (var range in Ranges)
            {
                var perRange = tenantMetrics.PerRange.GetValueOrDefault(range.Key) ?? TenantRangeMetrics.Empty;
                var loginStats = rangeLoginStats[range.Key].GetValueOrDefault(orgId, (0, 0));
                var currentLogins = loginStats.Item1;
                var previousLogins = loginStats.Item2;

                var featureAdoption = (int)Math.Round(perRange.CurrentAdoptedModules * 100.0 / 7);
                var previousAdoption = (int)Math.Round(perRange.PreviousAdoptedModules * 100.0 / 7);
                var activityChange = CalculatePercentChange(perRange.CurrentEventCount, perRange.PreviousEventCount);
                var userChange = CalculatePercentChange(currentLogins, previousLogins);
                var adoptionChange = CalculatePercentChange(featureAdoption, previousAdoption);
                var errorRate = perRange.CurrentEventCount == 0
                    ? 0m
                    : Math.Round(perRange.CurrentErrorCount * 100m / perRange.CurrentEventCount, 1);

                var lastActivityAt = MaxDate(tenantMetrics.LastAuditAtUtc, lastLoginByOrg.GetValueOrDefault(orgId));
                var healthScore = ComputeHealthScore(
                    perRange.CurrentEventCount,
                    currentLogins,
                    errorRate,
                    userCount,
                    seatLimit,
                    featureAdoption,
                    activityChange,
                    userChange,
                    adoptionChange,
                    lastActivityAt,
                    nowUtc);

                snapshotRows.Add(new OrganizationAnalyticsSnapshot
                {
                    OrganizationId = orgId,
                    RangeKey = range.Key,
                    SnapshotAtUtc = nowUtc,
                    OrganizationName = orgName,
                    PlanName = latestSub?.PlanName ?? orgFallbackPlan,
                    SubscriptionStatus = latestSub?.Status ?? "Unknown",
                    UserCount = userCount,
                    SeatLimit = seatLimit,
                    SeatUtilizationPercent = seatUtilization,
                    LoginsInRange = currentLogins,
                    EventCountInRange = perRange.CurrentEventCount,
                    ActivityChangePercent = activityChange,
                    UserChangePercent = userChange,
                    FeatureAdoptionPercent = featureAdoption,
                    AdoptionChangePercent = adoptionChange,
                    ErrorRatePercent = errorRate,
                    LastActivityAtUtc = lastActivityAt,
                    HealthScore = healthScore,
                    ChurnRiskLabel = GetChurnRiskLabel(healthScore),
                    SegmentLabel = DetermineSegment(healthScore, activityChange, userChange, adoptionChange, perRange.CurrentEventCount, currentLogins, lastActivityAt, nowUtc),
                    TrendLabel = FormatTrendLabel(perRange.CurrentEventCount, perRange.PreviousEventCount),
                    ActivityTrendJson = JsonSerializer.Serialize(perRange.ActivityTrendPoints),
                    RenewalDateDisplay = latestSub?.CurrentPeriodEnd.ToString("yyyy-MM-dd") ?? "-"
                });
            }
        }

        await UpsertSnapshotsAsync(snapshotRows, ct);
    }

    private async Task<Dictionary<int, OrgTenantMetrics>> ComputeTenantMetricsAsync(List<int> orgIds, DateTime nowUtc, int maxParallelTenants, CancellationToken ct)
    {
        var earliestWindowStart = nowUtc.AddDays(-730);
        var result = new Dictionary<int, OrgTenantMetrics>();
        var gate = new SemaphoreSlim(maxParallelTenants);

        var tasks = orgIds.Select(async orgId =>
        {
            await gate.WaitAsync(ct);
            try
            {
                var metrics = await ComputeSingleOrgTenantMetricsAsync(orgId, nowUtc, earliestWindowStart, ct);
                lock (result)
                {
                    result[orgId] = metrics;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed analytics snapshot aggregation for org {OrgId}", orgId);
                lock (result)
                {
                    result[orgId] = OrgTenantMetrics.Empty;
                }
            }
            finally
            {
                gate.Release();
            }
        });

        await Task.WhenAll(tasks);
        return result;
    }

    private async Task<OrgTenantMetrics> ComputeSingleOrgTenantMetricsAsync(int orgId, DateTime nowUtc, DateTime earliestWindowStart, CancellationToken ct)
    {
        await using var tenantDb = await _tenantDbFactory.CreateAsync(orgId);

        var auditWindowRows = await tenantDb.AuditLogs.AsNoTracking()
            .Where(a => a.OrgId == orgId && a.CreatedAt >= earliestWindowStart)
            .Select(a => new { a.CreatedAt, a.Level })
            .ToListAsync(ct);

        var lastAuditAt = await tenantDb.AuditLogs.AsNoTracking()
            .Where(a => a.OrgId == orgId)
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => (DateTime?)a.CreatedAt)
            .FirstOrDefaultAsync(ct);

        var riskDates = await tenantDb.Risks.AsNoTracking()
            .Where(x => x.OrgId == orgId && x.CreatedAt >= earliestWindowStart)
            .Select(x => x.CreatedAt)
            .ToListAsync(ct);
        var purchaseOrderDates = await tenantDb.PurchaseOrders.AsNoTracking()
            .Where(x => x.OrgId == orgId && x.CreatedAt >= earliestWindowStart)
            .Select(x => x.CreatedAt)
            .ToListAsync(ct);
        var expenseDates = await tenantDb.Expenses.AsNoTracking()
            .Where(x => x.OrgId == orgId && x.CreatedAt >= earliestWindowStart)
            .Select(x => x.CreatedAt)
            .ToListAsync(ct);
        var incidentDates = await tenantDb.Incidents.AsNoTracking()
            .Where(x => x.OrgId == orgId && x.ReportedAt >= earliestWindowStart)
            .Select(x => x.ReportedAt)
            .ToListAsync(ct);
        var changeOrderDates = await tenantDb.ChangeOrders.AsNoTracking()
            .Where(x => x.OrgId == orgId && x.CreatedAt >= earliestWindowStart)
            .Select(x => x.CreatedAt)
            .ToListAsync(ct);
        var projectDates = await tenantDb.Projects.AsNoTracking()
            .Where(x => x.OrgId == orgId && x.CreatedAt >= earliestWindowStart)
            .Select(x => x.CreatedAt)
            .ToListAsync(ct);
        var supplierDates = await tenantDb.Suppliers.AsNoTracking()
            .Where(x => x.OrgId == orgId && x.CreatedAt >= earliestWindowStart)
            .Select(x => x.CreatedAt)
            .ToListAsync(ct);

        var perRange = new Dictionary<string, TenantRangeMetrics>(StringComparer.OrdinalIgnoreCase);
        foreach (var range in Ranges)
        {
            var fromUtc = nowUtc.AddDays(-range.Days);
            var previousFrom = fromUtc.AddDays(-range.Days);

            var currentAuditRows = auditWindowRows.Where(x => x.CreatedAt >= fromUtc).ToList();
            var previousAuditRows = auditWindowRows.Where(x => x.CreatedAt >= previousFrom && x.CreatedAt < fromUtc).ToList();
            var currentTimestamps = currentAuditRows.Select(x => x.CreatedAt).ToList();

            var currentAdoptedModules = 0;
            if (HasAnyInWindow(riskDates, fromUtc, nowUtc)) currentAdoptedModules++;
            if (HasAnyInWindow(purchaseOrderDates, fromUtc, nowUtc)) currentAdoptedModules++;
            if (HasAnyInWindow(expenseDates, fromUtc, nowUtc)) currentAdoptedModules++;
            if (HasAnyInWindow(incidentDates, fromUtc, nowUtc)) currentAdoptedModules++;
            if (HasAnyInWindow(changeOrderDates, fromUtc, nowUtc)) currentAdoptedModules++;
            if (HasAnyInWindow(projectDates, fromUtc, nowUtc)) currentAdoptedModules++;
            if (HasAnyInWindow(supplierDates, fromUtc, nowUtc)) currentAdoptedModules++;

            var previousAdoptedModules = 0;
            if (HasAnyInWindow(riskDates, previousFrom, fromUtc)) previousAdoptedModules++;
            if (HasAnyInWindow(purchaseOrderDates, previousFrom, fromUtc)) previousAdoptedModules++;
            if (HasAnyInWindow(expenseDates, previousFrom, fromUtc)) previousAdoptedModules++;
            if (HasAnyInWindow(incidentDates, previousFrom, fromUtc)) previousAdoptedModules++;
            if (HasAnyInWindow(changeOrderDates, previousFrom, fromUtc)) previousAdoptedModules++;
            if (HasAnyInWindow(projectDates, previousFrom, fromUtc)) previousAdoptedModules++;
            if (HasAnyInWindow(supplierDates, previousFrom, fromUtc)) previousAdoptedModules++;

            perRange[range.Key] = new TenantRangeMetrics
            {
                CurrentEventCount = currentAuditRows.Count,
                PreviousEventCount = previousAuditRows.Count,
                CurrentErrorCount = currentAuditRows.Count(x => x.Level == "Error" || x.Level == "Critical"),
                CurrentAdoptedModules = currentAdoptedModules,
                PreviousAdoptedModules = previousAdoptedModules,
                ActivityTrendPoints = BuildActivityTrendPoints(currentTimestamps, fromUtc, nowUtc, 6)
            };
        }

        return new OrgTenantMetrics
        {
            LastAuditAtUtc = lastAuditAt,
            PerRange = perRange
        };
    }

    private async Task UpsertSnapshotsAsync(List<OrganizationAnalyticsSnapshot> rows, CancellationToken ct)
    {
        var orgIds = rows.Select(x => x.OrganizationId).Distinct().ToList();
        var ranges = rows.Select(x => x.RangeKey).Distinct().ToList();

        var existing = await _platformDb.OrganizationAnalyticsSnapshots
            .Where(x => orgIds.Contains(x.OrganizationId) && ranges.Contains(x.RangeKey))
            .ToListAsync(ct);

        var existingByKey = existing.ToDictionary(x => (x.OrganizationId, x.RangeKey), x => x);

        foreach (var row in rows)
        {
            if (existingByKey.TryGetValue((row.OrganizationId, row.RangeKey), out var snapshot))
            {
                snapshot.SnapshotAtUtc = row.SnapshotAtUtc;
                snapshot.OrganizationName = row.OrganizationName;
                snapshot.PlanName = row.PlanName;
                snapshot.SubscriptionStatus = row.SubscriptionStatus;
                snapshot.UserCount = row.UserCount;
                snapshot.SeatLimit = row.SeatLimit;
                snapshot.SeatUtilizationPercent = row.SeatUtilizationPercent;
                snapshot.LoginsInRange = row.LoginsInRange;
                snapshot.EventCountInRange = row.EventCountInRange;
                snapshot.ActivityChangePercent = row.ActivityChangePercent;
                snapshot.UserChangePercent = row.UserChangePercent;
                snapshot.FeatureAdoptionPercent = row.FeatureAdoptionPercent;
                snapshot.AdoptionChangePercent = row.AdoptionChangePercent;
                snapshot.ErrorRatePercent = row.ErrorRatePercent;
                snapshot.LastActivityAtUtc = row.LastActivityAtUtc;
                snapshot.HealthScore = row.HealthScore;
                snapshot.ChurnRiskLabel = row.ChurnRiskLabel;
                snapshot.SegmentLabel = row.SegmentLabel;
                snapshot.TrendLabel = row.TrendLabel;
                snapshot.ActivityTrendJson = row.ActivityTrendJson;
                snapshot.RenewalDateDisplay = row.RenewalDateDisplay;
                continue;
            }

            _platformDb.OrganizationAnalyticsSnapshots.Add(row);
        }

        await _platformDb.SaveChangesAsync(ct);
    }

    private static bool HasAnyInWindow(IEnumerable<DateTime> dates, DateTime fromInclusive, DateTime toExclusive)
        => dates.Any(d => d >= fromInclusive && d < toExclusive);

    private static string FormatTrendLabel(int current, int previous)
    {
        if (current == previous) return "Stable";
        if (previous <= 0) return current > 0 ? "New activity" : "Stable";
        if (current <= 0) return "Down 100%";

        if (current > previous)
        {
            var increasePercent = (int)Math.Round((current - previous) * 100.0 / previous);
            return increasePercent > 999 ? "Up 999%+" : $"Up {Math.Max(1, increasePercent)}%";
        }

        var decreasePercent = (int)Math.Round((previous - current) * 100.0 / previous);
        return $"Down {Math.Max(1, decreasePercent)}%";
    }

    private static DateTime? MaxDate(DateTime? first, DateTime? second)
    {
        if (!first.HasValue) return second;
        if (!second.HasValue) return first;
        return first.Value >= second.Value ? first : second;
    }

    private static int ComputeHealthScore(
        int eventsInRange,
        int loginsInRange,
        decimal errorRatePercent,
        int activeUsers,
        int? seatLimit,
        int featureAdoptionPercent,
        decimal activityChangePercent,
        decimal userChangePercent,
        decimal adoptionChangePercent,
        DateTime? lastActivityAt,
        DateTime nowUtc)
    {
        var score = 100;

        if (eventsInRange == 0) score -= 25;
        if (loginsInRange == 0) score -= 15;

        if (activityChangePercent <= -50m) score -= 20;
        else if (activityChangePercent < 0m) score -= 8;
        else if (activityChangePercent >= 20m) score += 4;

        if (userChangePercent <= -35m) score -= 18;
        else if (userChangePercent < 0m) score -= 7;
        else if (userChangePercent >= 15m) score += 4;

        if (adoptionChangePercent <= -25m) score -= 15;
        else if (adoptionChangePercent < 0m) score -= 6;
        else if (adoptionChangePercent >= 10m) score += 3;

        if (featureAdoptionPercent < 30) score -= 15;
        else if (featureAdoptionPercent < 50) score -= 8;

        if (errorRatePercent >= 5m) score -= 25;
        else if (errorRatePercent >= 2m) score -= 12;
        else if (errorRatePercent >= 1m) score -= 6;

        if (seatLimit.HasValue && seatLimit.Value > 0)
        {
            var utilization = activeUsers * 100m / seatLimit.Value;
            if (utilization > 100m) score -= 12;
            else if (utilization < 15m && activeUsers > 0) score -= 8;
        }

        if (lastActivityAt.HasValue)
        {
            var daysSinceActivity = (nowUtc - lastActivityAt.Value).TotalDays;
            if (daysSinceActivity > 30) score -= 25;
            else if (daysSinceActivity > 14) score -= 12;
        }
        else
        {
            score -= 25;
        }

        return Math.Clamp(score, 0, 100);
    }

    private static string DetermineSegment(
        int healthScore,
        decimal activityChangePercent,
        decimal userChangePercent,
        decimal adoptionChangePercent,
        int eventsInRange,
        int loginsInRange,
        DateTime? lastActivityAt,
        DateTime nowUtc)
    {
        var inactive = eventsInRange == 0 && loginsInRange == 0 && (!lastActivityAt.HasValue || (nowUtc - lastActivityAt.Value).TotalDays > 14);
        if (inactive) return "Inactive";
        if (healthScore < 55) return "At risk";
        if (healthScore >= 72 && activityChangePercent > 15m && userChangePercent > 8m && adoptionChangePercent >= 0m) return "Growing";
        return "Stable";
    }

    private static decimal CalculatePercentChange(int current, int previous)
        => CalculatePercentChange((decimal)current, (decimal)previous);

    private static decimal CalculatePercentChange(decimal current, decimal previous)
    {
        if (previous <= 0m)
        {
            if (current <= 0m) return 0m;
            return 999m;
        }

        var value = Math.Round((current - previous) * 100m / previous, 1);
        if (value > 999m) return 999m;
        if (value < -100m) return -100m;
        return value;
    }

    private static string GetChurnRiskLabel(int healthScore)
    {
        if (healthScore >= 80) return "Low";
        if (healthScore >= 55) return "Medium";
        return "High";
    }

    private static List<int> BuildActivityTrendPoints(List<DateTime> timestamps, DateTime fromUtc, DateTime toUtc, int bucketCount)
    {
        if (bucketCount <= 0) return new List<int>();

        var points = Enumerable.Repeat(0, bucketCount).ToList();
        var spanTicks = Math.Max(1, (toUtc - fromUtc).Ticks);

        foreach (var ts in timestamps)
        {
            var offset = Math.Max(0, (ts - fromUtc).Ticks);
            var index = (int)(offset * bucketCount / spanTicks);
            if (index >= bucketCount) index = bucketCount - 1;
            points[index]++;
        }

        return points;
    }

    private sealed record RangeDef(string Key, string Label, int Days);

    private sealed class OrgTenantMetrics
    {
        public DateTime? LastAuditAtUtc { get; set; }
        public Dictionary<string, TenantRangeMetrics> PerRange { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public static OrgTenantMetrics Empty => new();
    }

    private sealed class TenantRangeMetrics
    {
        public int CurrentEventCount { get; set; }
        public int PreviousEventCount { get; set; }
        public int CurrentErrorCount { get; set; }
        public int CurrentAdoptedModules { get; set; }
        public int PreviousAdoptedModules { get; set; }
        public List<int> ActivityTrendPoints { get; set; } = new();

        public static TenantRangeMetrics Empty => new() { ActivityTrendPoints = new List<int> { 0, 0, 0, 0, 0, 0 } };
    }
}
