using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WEB_Sentro.Areas.Vendor.Models;
using WEB_Sentro.Data;
using WEB_Sentro.Services;

namespace WEB_Sentro.Areas.Vendor.Controllers
{
    [Area("Vendor")]
    [Authorize(Policy = "SuperAdminOnly")]
    public class HealthController : Controller
    {
        private static readonly DateTime ProcessStartedAtUtc = DateTime.UtcNow;
        private readonly PlatformDbContext _platformDb;
        private readonly ITenantDbFactory _tenantDbFactory;

        public HealthController(PlatformDbContext platformDb, ITenantDbFactory tenantDbFactory)
        {
            _platformDb = platformDb;
            _tenantDbFactory = tenantDbFactory;
        }

        public async Task<IActionResult> Index(CancellationToken ct = default)
        {
            var checks = new List<HealthCheckRowViewModel>();

            var appUptime = DateTime.UtcNow - ProcessStartedAtUtc;
            checks.Add(new HealthCheckRowViewModel
            {
                Name = "Application Uptime",
                Description = "Current running process uptime",
                Value = FormatDuration(appUptime),
                Status = "Healthy",
                StatusColorClass = "text-emerald-600"
            });

            try
            {
                var canConnect = await _platformDb.Database.CanConnectAsync(ct);
                checks.Add(new HealthCheckRowViewModel
                {
                    Name = "Platform Database",
                    Description = "Shared platform DB connectivity",
                    Value = canConnect ? "Connected" : "Unavailable",
                    Status = canConnect ? "Healthy" : "Critical",
                    StatusColorClass = canConnect ? "text-emerald-600" : "text-rose-500"
                });
            }
            catch
            {
                checks.Add(new HealthCheckRowViewModel
                {
                    Name = "Platform Database",
                    Description = "Shared platform DB connectivity",
                    Value = "Error",
                    Status = "Critical",
                    StatusColorClass = "text-rose-500"
                });
            }

            var activeOrgs = await _platformDb.Organizations.AsNoTracking()
                .Where(o => o.Status == "Active")
                .Select(o => o.OrganizationId)
                .ToListAsync(ct);

            var tenantHealthy = 0;
            var tenantUnhealthy = 0;
            foreach (var orgId in activeOrgs.Take(10))
            {
                try
                {
                    await using var tenantDb = await _tenantDbFactory.CreateAsync(orgId);
                    var connected = await tenantDb.Database.CanConnectAsync(ct);
                    if (connected) tenantHealthy++;
                    else tenantUnhealthy++;
                }
                catch
                {
                    tenantUnhealthy++;
                }
            }

            checks.Add(new HealthCheckRowViewModel
            {
                Name = "Tenant DB Sample",
                Description = "Connectivity sample across active tenants",
                Value = $"{tenantHealthy} healthy / {tenantUnhealthy} failed",
                Status = tenantUnhealthy == 0 ? "Healthy" : tenantHealthy > 0 ? "Warning" : "Critical",
                StatusColorClass = tenantUnhealthy == 0 ? "text-emerald-600" : tenantHealthy > 0 ? "text-amber-500" : "text-rose-500"
            });

            var failedAuditEventsLastHour = 0;
            foreach (var orgId in activeOrgs.Take(10))
            {
                try
                {
                    await using var tenantDb = await _tenantDbFactory.CreateAsync(orgId);
                    failedAuditEventsLastHour += await tenantDb.AuditLogs.AsNoTracking()
                        .CountAsync(a => a.OrgId == orgId && a.CreatedAt >= DateTime.UtcNow.AddHours(-1) && (a.Level == "Critical" || a.Level == "Error"), ct);
                }
                catch
                {
                }
            }

            checks.Add(new HealthCheckRowViewModel
            {
                Name = "Critical Events (1h)",
                Description = "Critical or error events in sampled tenants",
                Value = failedAuditEventsLastHour.ToString(),
                Status = failedAuditEventsLastHour == 0 ? "Healthy" : failedAuditEventsLastHour < 20 ? "Warning" : "Critical",
                StatusColorClass = failedAuditEventsLastHour == 0 ? "text-emerald-600" : failedAuditEventsLastHour < 20 ? "text-amber-500" : "text-rose-500"
            });

            var overallStatus = checks.Any(c => c.Status == "Critical")
                ? "Critical"
                : checks.Any(c => c.Status == "Warning")
                    ? "Warning"
                    : "Healthy";

            var model = new HealthIndexViewModel
            {
                OverallStatus = overallStatus,
                OverallStatusClass = overallStatus == "Healthy" ? "text-emerald-600" : overallStatus == "Warning" ? "text-amber-500" : "text-rose-500",
                CheckedAtUtc = DateTime.UtcNow,
                AppUptimeDisplay = FormatDuration(appUptime),
                Checks = checks
            };

            return View(model);
        }

        private static string FormatDuration(TimeSpan duration)
        {
            if (duration.TotalDays >= 1)
            {
                return $"{(int)duration.TotalDays}d {duration.Hours}h {duration.Minutes}m";
            }

            if (duration.TotalHours >= 1)
            {
                return $"{duration.Hours}h {duration.Minutes}m";
            }

            return $"{duration.Minutes}m {duration.Seconds}s";
        }
    }
}
