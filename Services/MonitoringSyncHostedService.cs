using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WEB_Sentro.Data;
using WEB_Sentro.Models.Identity;

namespace WEB_Sentro.Services
{
    public class MonitoringSyncHostedService : BackgroundService
    {
        private readonly IServiceProvider _services;
        private readonly IConfiguration _config;
        private readonly ILogger<MonitoringSyncHostedService> _logger;

        public MonitoringSyncHostedService(IServiceProvider services, IConfiguration config, ILogger<MonitoringSyncHostedService> logger)
        {
            _services = services;
            _config = config;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var intervalMinutes = _config.GetValue("Monitoring:SyncIntervalMinutes", 15);
            if (intervalMinutes <= 0)
            {
                _logger.LogInformation("Monitoring sync disabled (SyncIntervalMinutes <= 0).");
                return;
            }

            var orgIds = _config.GetSection("Monitoring:SyncOrgIds").Get<int[]>() ?? new[] { 1 };
            _logger.LogInformation("Monitoring sync started. Interval: {Interval} min, Orgs: {OrgIds}.", intervalMinutes, string.Join(",", orgIds));

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RunSyncForAllOrgsAsync(orgIds, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Monitoring sync cycle failed.");
                }

                await Task.Delay(TimeSpan.FromMinutes(intervalMinutes), stoppingToken);
            }
        }

        private async Task RunSyncForAllOrgsAsync(int[] orgIds, CancellationToken ct)
        {
            using var scope = _services.CreateScope();
            var hub = scope.ServiceProvider.GetRequiredService<MonitoringHubService>();
            var platformDb = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var tenantFactory = scope.ServiceProvider.GetRequiredService<ITenantDbFactory>();
            var riskService = scope.ServiceProvider.GetRequiredService<RiskService>();

            foreach (var orgId in orgIds)
            {
                try
                {
                    var systemUserId = await GetSystemUserIdAsync(platformDb, userManager, orgId, ct);
                    if (string.IsNullOrEmpty(systemUserId))
                    {
                        _logger.LogWarning("No Admin/RiskManager user for org {OrgId}; skipping sync.", orgId);
                        continue;
                    }

                    var sites = await hub.GetSitesForHubAsync(orgId, ct);
                    foreach (var site in sites)
                    {
                        var monitoringSiteId = site.MonitoringSiteId;
                        if (monitoringSiteId <= 0)
                            monitoringSiteId = await hub.EnsureMonitoringSiteForSiteAsync(orgId, site.DbSiteId, ct);
                        if (monitoringSiteId <= 0) continue;

                        try
                        {
                            var (lastSync, apiOk) = await hub.RunSyncForSiteAsync(orgId, monitoringSiteId, systemUserId, null, ct);
                            await using var db = await tenantFactory.CreateAsync(orgId);
                            riskService.AddAuditLog(db, orgId, systemUserId, "MonitoringEvent", monitoringSiteId, "BackgroundSync", $"Sync completed. ApiOk={apiOk}, LastSync={lastSync:O}", null);
                            await db.SaveChangesAsync(ct);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Sync failed for org {OrgId} site {SiteId}.", orgId, monitoringSiteId);
                            try
                            {
                                await using var db = await tenantFactory.CreateAsync(orgId);
                                riskService.AddAuditLog(db, orgId, systemUserId, "MonitoringEvent", monitoringSiteId, "BackgroundSyncFailed", ex.Message?.Length > 255 ? ex.Message.Substring(0, 255) : ex.Message, null);
                                await db.SaveChangesAsync(ct);
                            }
                            catch { /* best effort */ }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Sync failed for org {OrgId}.", orgId);
                }
            }
        }

        private static async Task<string?> GetSystemUserIdAsync(PlatformDbContext platformDb, UserManager<ApplicationUser> userManager, int orgId, CancellationToken ct)
        {
            var users = await platformDb.Users
                .Where(u => u.OrganizationId == orgId && u.IsActive)
                .ToListAsync(ct);
            foreach (var u in users)
            {
                var roles = await userManager.GetRolesAsync(u);
                if (roles.Any(r => string.Equals(r, "Admin", StringComparison.OrdinalIgnoreCase) || string.Equals(r, "RiskManager", StringComparison.OrdinalIgnoreCase)))
                    return u.Id;
            }
            return null;
        }
    }
}
