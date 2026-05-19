using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using WEB_Sentro.Areas.Vendor.Models;
using WEB_Sentro.Data;
using WEB_Sentro.Services;

namespace WEB_Sentro.Areas.Vendor.Controllers;

[Area("Vendor")]
[Authorize(Policy = "SuperAdminOnly")]
public class DashboardController : Controller
{
    private const decimal ChartCircumference = 552.92m;
    private readonly ILogger<DashboardController> _logger;
    private readonly PlatformDbContext _platformDb;
    private readonly ITenantDbFactory _tenantDbFactory;
    private readonly IConfiguration _configuration;

    public DashboardController(
        ILogger<DashboardController> logger,
        PlatformDbContext platformDb,
        ITenantDbFactory tenantDbFactory,
        IConfiguration configuration)
    {
        _logger = logger;
        _platformDb = platformDb;
        _tenantDbFactory = tenantDbFactory;
        _configuration = configuration;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        _logger.LogInformation("Vendor dashboard requested by {UserName}", User?.Identity?.Name ?? "<null>");

        var activeOrganizations = await _platformDb.Organizations.AsNoTracking()
            .Where(o => o.Status == "Active")
            .Select(o => new { o.OrganizationId, o.OrgName })
            .ToListAsync(ct);

        var orgNameById = activeOrganizations.ToDictionary(x => x.OrganizationId, x => x.OrgName);
        var orgIds = activeOrganizations.Select(x => x.OrganizationId).ToList();

        var snapshots = orgIds.Count == 0
            ? new List<Data.Entities.OrganizationAnalyticsSnapshot>()
            : await _platformDb.OrganizationAnalyticsSnapshots.AsNoTracking()
                .Where(x => x.RangeKey == "30d" && orgIds.Contains(x.OrganizationId))
                .ToListAsync(ct);

        var tenantMetrics = await LoadTenantMetricsAsync(orgIds, orgNameById, ct);

        var compliancePercent = snapshots.Count == 0
            ? 0
            : (int)Math.Round(snapshots.Count(x => x.HealthScore >= 80) * 100.0 / snapshots.Count);

        var latestSnapshot = snapshots
            .Select(x => (DateTime?)x.SnapshotAtUtc)
            .Max();

        var model = new VendorDashboardViewModel
        {
            OrganizationCount = activeOrganizations.Count,
            ActiveRisksCount = tenantMetrics.ActiveRisksCount,
            OpenIncidentsCount = tenantMetrics.OpenIncidentsCount,
            PlatformHealthPercent = snapshots.Count == 0 ? 0m : Math.Round((decimal)snapshots.Average(x => x.HealthScore), 1),
            CompliancePercent = compliancePercent,
            LastSnapshotAtUtc = latestSnapshot,
            LastUpdatedDisplay = FormatRelativeTime(latestSnapshot, DateTime.UtcNow),
            RiskVelocityPoints = BuildRiskVelocitySeries(snapshots),
            RiskVelocityTotalEvents = snapshots.Sum(x => x.EventCountInRange),
            RiskSeverity = BuildSeverityRows(tenantMetrics),
            LiveFeed = tenantMetrics.LiveFeed
                .OrderByDescending(x => x.OccurredAtUtc)
                .Take(5)
                .ToList(),
            DataFreshnessLabel = BuildDataFreshnessLabel(latestSnapshot, DateTime.UtcNow),
            JobsStatusLabel = BuildJobsStatusLabel(latestSnapshot, DateTime.UtcNow)
        };

        ViewData["ComplianceDashOffset"] = (double)Math.Round(ChartCircumference * (100m - model.CompliancePercent) / 100m, 1);
        return View(model);
    }

    private async Task<TenantAggregateResult> LoadTenantMetricsAsync(
        List<int> orgIds,
        Dictionary<int, string> orgNameById,
        CancellationToken ct)
    {
        if (orgIds.Count == 0)
        {
            return new TenantAggregateResult();
        }

        var maxParallelTenants = Math.Max(1, _configuration.GetValue("VendorAnalytics:MaxParallelTenants", 4));
        var gate = new SemaphoreSlim(maxParallelTenants);
        var rows = new List<TenantOrgResult>();
        var rowsLock = new object();
        var fromUtc = DateTime.UtcNow.AddDays(-2);

        var tasks = orgIds.Select(async orgId =>
        {
            await gate.WaitAsync(ct);
            try
            {
                await using var tenantDb = await _tenantDbFactory.CreateAsync(orgId);

                var activeRisksQuery = tenantDb.Risks.AsNoTracking()
                    .Where(r =>
                        r.OrgId == orgId &&
                        r.DeletedAt == null &&
                        r.Status != "Closed_Invalid" &&
                        r.Status != "Rejected" &&
                        r.Status != "Draft");

                var riskSeverityRows = await activeRisksQuery
                    .GroupBy(r => r.Priority == "Critical" || r.Priority == "High"
                        ? "high"
                        : r.Priority == "Medium"
                            ? "medium"
                            : "low")
                    .Select(g => new { Key = g.Key, Count = g.Count() })
                    .ToListAsync(ct);

                var openIncidentsCount = await tenantDb.Incidents.AsNoTracking()
                    .CountAsync(i => i.OrgId == orgId && i.DeletedAt == null && i.Status != "Closed", ct);

                var auditRows = await tenantDb.AuditLogs.AsNoTracking()
                    .Where(a => a.OrgId == orgId && a.CreatedAt >= fromUtc)
                    .OrderByDescending(a => a.CreatedAt)
                    .Take(4)
                    .Select(a => new { a.CreatedAt, a.ActionType, a.Level })
                    .ToListAsync(ct);

                var orgResult = new TenantOrgResult
                {
                    ActiveRisksCount = riskSeverityRows.Sum(x => x.Count),
                    OpenIncidentsCount = openIncidentsCount,
                    HighRiskCount = riskSeverityRows.Where(x => x.Key == "high").Sum(x => x.Count),
                    MediumRiskCount = riskSeverityRows.Where(x => x.Key == "medium").Sum(x => x.Count),
                    LowRiskCount = riskSeverityRows.Where(x => x.Key == "low").Sum(x => x.Count)
                };

                foreach (var row in auditRows)
                {
                    orgResult.LiveFeed.Add(new VendorLiveFeedItemViewModel
                    {
                        OccurredAtUtc = row.CreatedAt,
                        Title = string.IsNullOrWhiteSpace(row.ActionType) ? "System event" : row.ActionType,
                        Meta = $"{orgNameById.GetValueOrDefault(orgId, $"Org #{orgId}")} • {FormatRelativeTime(row.CreatedAt, DateTime.UtcNow)}",
                        DotClass = GetDotClass(row.Level)
                    });
                }

                lock (rowsLock)
                {
                    rows.Add(orgResult);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Unable to load tenant dashboard metrics for org {OrgId}", orgId);
            }
            finally
            {
                gate.Release();
            }
        });

        await Task.WhenAll(tasks);

        return new TenantAggregateResult
        {
            ActiveRisksCount = rows.Sum(x => x.ActiveRisksCount),
            OpenIncidentsCount = rows.Sum(x => x.OpenIncidentsCount),
            HighRiskCount = rows.Sum(x => x.HighRiskCount),
            MediumRiskCount = rows.Sum(x => x.MediumRiskCount),
            LowRiskCount = rows.Sum(x => x.LowRiskCount),
            LiveFeed = rows.SelectMany(x => x.LiveFeed).ToList()
        };
    }

    private static List<VendorSeverityRowViewModel> BuildSeverityRows(TenantAggregateResult metrics)
    {
        var total = Math.Max(1, metrics.HighRiskCount + metrics.MediumRiskCount + metrics.LowRiskCount);

        return
        [
            new VendorSeverityRowViewModel
            {
                Label = "Critical/High",
                Count = metrics.HighRiskCount,
                Percent = (int)Math.Round(metrics.HighRiskCount * 100.0 / total),
                ColorClass = "bg-rose-500"
            },
            new VendorSeverityRowViewModel
            {
                Label = "Medium Exposure",
                Count = metrics.MediumRiskCount,
                Percent = (int)Math.Round(metrics.MediumRiskCount * 100.0 / total),
                ColorClass = "bg-amber-500"
            },
            new VendorSeverityRowViewModel
            {
                Label = "Low/Acceptable",
                Count = metrics.LowRiskCount,
                Percent = (int)Math.Round(metrics.LowRiskCount * 100.0 / total),
                ColorClass = "bg-emerald-500"
            }
        ];
    }

    private static List<int> BuildRiskVelocitySeries(List<Data.Entities.OrganizationAnalyticsSnapshot> snapshots)
    {
        if (snapshots.Count == 0)
        {
            return new List<int> { 0, 0, 0, 0, 0, 0 };
        }

        var totals = new int[6];
        foreach (var snapshot in snapshots)
        {
            if (string.IsNullOrWhiteSpace(snapshot.ActivityTrendJson))
            {
                continue;
            }

            try
            {
                var points = JsonSerializer.Deserialize<List<int>>(snapshot.ActivityTrendJson) ?? new List<int>();
                for (var i = 0; i < Math.Min(totals.Length, points.Count); i++)
                {
                    totals[i] += Math.Max(0, points[i]);
                }
            }
            catch
            {
            }
        }

        return totals.ToList();
    }

    private static string BuildDataFreshnessLabel(DateTime? lastSnapshotAt, DateTime nowUtc)
    {
        if (!lastSnapshotAt.HasValue)
        {
            return "No data";
        }

        var age = nowUtc - lastSnapshotAt.Value;
        if (age.TotalMinutes < 5) return "Fresh";
        if (age.TotalMinutes < 30) return "Recent";
        return "Stale";
    }

    private static string BuildJobsStatusLabel(DateTime? lastSnapshotAt, DateTime nowUtc)
    {
        if (!lastSnapshotAt.HasValue)
        {
            return "Idle";
        }

        var age = nowUtc - lastSnapshotAt.Value;
        return age.TotalMinutes <= 30 ? "Running" : "Needs attention";
    }

    private static string FormatRelativeTime(DateTime? date, DateTime nowUtc)
    {
        if (!date.HasValue)
        {
            return "No data";
        }

        var diff = nowUtc - date.Value;
        if (diff.TotalMinutes < 1) return "Just now";
        if (diff.TotalHours < 1) return $"{Math.Max(1, diff.Minutes)}m ago";
        if (diff.TotalDays < 1) return $"{Math.Max(1, (int)diff.TotalHours)}h ago";
        if (diff.TotalDays < 30) return $"{Math.Max(1, (int)diff.TotalDays)}d ago";
        return date.Value.ToString("yyyy-MM-dd");
    }

    private static string GetDotClass(string? level)
    {
        if (string.Equals(level, "Critical", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(level, "Error", StringComparison.OrdinalIgnoreCase))
        {
            return "bg-rose-500 shadow-[0_0_8px_rgba(244,63,94,0.6)]";
        }

        if (string.Equals(level, "Warning", StringComparison.OrdinalIgnoreCase))
        {
            return "bg-amber-500";
        }

        if (string.Equals(level, "Success", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(level, "Info", StringComparison.OrdinalIgnoreCase))
        {
            return "bg-emerald-500";
        }

        return "bg-sky-500";
    }

    private sealed class TenantOrgResult
    {
        public int ActiveRisksCount { get; set; }
        public int OpenIncidentsCount { get; set; }
        public int HighRiskCount { get; set; }
        public int MediumRiskCount { get; set; }
        public int LowRiskCount { get; set; }
        public List<VendorLiveFeedItemViewModel> LiveFeed { get; set; } = new();
    }

    private sealed class TenantAggregateResult
    {
        public int ActiveRisksCount { get; set; }
        public int OpenIncidentsCount { get; set; }
        public int HighRiskCount { get; set; }
        public int MediumRiskCount { get; set; }
        public int LowRiskCount { get; set; }
        public List<VendorLiveFeedItemViewModel> LiveFeed { get; set; } = new();
    }
}
