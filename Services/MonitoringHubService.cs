using Microsoft.EntityFrameworkCore;
using WEB_Sentro.Data;
using WEB_Sentro.Data.Entities;

namespace WEB_Sentro.Services
{
    public class MonitoringAlertViewModel
    {
        public int AlertId { get; set; }
        public string RuleName { get; set; } = "";
        public string? MeasuredValues { get; set; }
        public string Severity { get; set; } = "";
        public DateTime TriggeredAt { get; set; }
        public int? RiskId { get; set; }
    }

    /// <summary>Site entry for the Risk Monitoring Hub. Sourced from Sites table; may have a linked MonitoringSite.</summary>
    public class MonitoringHubSiteItem
    {
        /// <summary>When &gt; 0, existing MonitoringSiteId. When 0, this is a Site-only row (use DbSiteId to create MonitoringSite on first sync).</summary>
        public int MonitoringSiteId { get; set; }
        /// <summary>Site id from Sites table. Used when MonitoringSiteId is 0 to create a MonitoringSite on sync.</summary>
        public int DbSiteId { get; set; }
        public string Name { get; set; } = "";
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }

    public class MonitoringHubService
    {
        private readonly ITenantDbFactory _tenantDbFactory;
        private readonly IOpenWeatherService _openWeather;
        private readonly RiskService _riskService;

        public MonitoringHubService(ITenantDbFactory tenantDbFactory, IOpenWeatherService openWeather, RiskService riskService)
        {
            _tenantDbFactory = tenantDbFactory;
            _openWeather = openWeather;
            _riskService = riskService;
        }

        /// <summary>Returns sites for the Hub from the Sites table (DB), not seeded MonitoringSites. Each Site appears once; if it has a linked MonitoringSite that is used for coordinates/sync.</summary>
        public async Task<List<MonitoringHubSiteItem>> GetSitesForHubAsync(int orgId, CancellationToken ct = default)
        {
            await using var db = await _tenantDbFactory.CreateAsync(orgId);
            var sites = await db.Sites.AsNoTracking()
                .Where(s => s.OrgId == orgId && s.Status != "Archived")
                .OrderBy(s => s.SiteName)
                .Select(s => new { s.SiteId, s.SiteName, s.Latitude, s.Longitude })
                .ToListAsync(ct);
            var siteIds = sites.Select(s => s.SiteId).ToList();
            Dictionary<int, MonitoringSite> monitoringBySite;
            if (siteIds.Count == 0)
            {
                monitoringBySite = new Dictionary<int, MonitoringSite>();
            }
            else
            {
                var monList = await db.MonitoringSites.AsNoTracking()
                    .Where(m => m.OrgId == orgId && m.SiteId != null && siteIds.Contains(m.SiteId!.Value))
                    .ToListAsync(ct);
                monitoringBySite = monList.GroupBy(m => m.SiteId!.Value).ToDictionary(g => g.Key, g => g.First());
            }

            return sites.Select(s =>
            {
                var mon = monitoringBySite.GetValueOrDefault(s.SiteId);
                var lat = mon != null ? mon.Latitude : (double)(s.Latitude ?? 0);
                var lon = mon != null ? mon.Longitude : (double)(s.Longitude ?? 0);
                return new MonitoringHubSiteItem
                {
                    MonitoringSiteId = mon?.MonitoringSiteId ?? 0,
                    DbSiteId = s.SiteId,
                    Name = s.SiteName,
                    Latitude = lat,
                    Longitude = lon
                };
            }).ToList();
        }

        /// <summary>Ensures a MonitoringSite exists for the given DB Site; creates one using Site coordinates if missing. Returns the MonitoringSiteId.</summary>
        public async Task<int> EnsureMonitoringSiteForSiteAsync(int orgId, int dbSiteId, CancellationToken ct = default)
        {
            await using var db = await _tenantDbFactory.CreateAsync(orgId);
            var existing = await db.MonitoringSites.FirstOrDefaultAsync(m => m.OrgId == orgId && m.SiteId == dbSiteId, ct);
            if (existing != null) return existing.MonitoringSiteId;

            var site = await db.Sites.AsNoTracking().FirstOrDefaultAsync(s => s.SiteId == dbSiteId && s.OrgId == orgId, ct);
            if (site == null) return 0;

            var created = new MonitoringSite
            {
                OrgId = orgId,
                SiteId = dbSiteId,
                Name = site.SiteName,
                Latitude = (double)(site.Latitude ?? 0),
                Longitude = (double)(site.Longitude ?? 0)
            };
            db.MonitoringSites.Add(created);
            await db.SaveChangesAsync(ct);
            return created.MonitoringSiteId;
        }

        public async Task<List<MonitoringSite>> GetSitesAsync(int orgId, CancellationToken ct = default)
        {
            await using var db = await _tenantDbFactory.CreateAsync(orgId);
            return await db.MonitoringSites.AsNoTracking().Where(s => s.OrgId == orgId).OrderBy(s => s.Name).ToListAsync(ct);
        }

        public async Task<DateTime?> GetLastSyncUtcAsync(int orgId, int siteId, CancellationToken ct = default)
        {
            await using var db = await _tenantDbFactory.CreateAsync(orgId);
            var t = await db.MonitoringAlerts.AsNoTracking().Where(a => a.OrgId == orgId && a.MonitoringSiteId == siteId).MaxAsync(a => (DateTime?)a.TriggeredAt, ct);
            return t;
        }

        public async Task<List<MonitoringAlertViewModel>> GetRecentAlertsAsync(int orgId, int? siteId, int top = 20, CancellationToken ct = default)
        {
            await using var db = await _tenantDbFactory.CreateAsync(orgId);
            var q = db.MonitoringAlerts.AsNoTracking().Where(a => a.OrgId == orgId);
            if (siteId.HasValue) q = q.Where(a => a.MonitoringSiteId == siteId.Value);
            var list = await q.OrderByDescending(a => a.TriggeredAt).Take(top)
                .Select(a => new MonitoringAlertViewModel { AlertId = a.AlertId, RuleName = a.RuleName, MeasuredValues = a.MeasuredValues, Severity = a.Severity, TriggeredAt = a.TriggeredAt, RiskId = a.RiskId })
                .ToListAsync(ct);
            return list;
        }

        public async Task<(DateTime? LastSyncUtc, bool ApiOk)> RunSyncForSiteAsync(int orgId, int siteId, string userId, string? simulate = null, CancellationToken ct = default)
        {
            await using var db = await _tenantDbFactory.CreateAsync(orgId);
            var site = await db.MonitoringSites.AsNoTracking().FirstOrDefaultAsync(s => s.MonitoringSiteId == siteId && s.OrgId == orgId, ct);
            if (site == null) return (null, false);

            WeatherSnapshot weather;
            if (string.Equals(simulate, "wind", StringComparison.OrdinalIgnoreCase))
            {
                weather = new WeatherSnapshot
                {
                    FetchedAt = DateTime.UtcNow,
                    ApiOk = true,
                    TempC = 30,
                    WindSpeedKmh = 55,
                    Condition = "Windy",
                    WeatherId = 0,
                    Humidity = 50,
                    Rain_1h_mm = 0
                };
            }
            else if (string.Equals(simulate, "storm", StringComparison.OrdinalIgnoreCase))
            {
                weather = new WeatherSnapshot
                {
                    FetchedAt = DateTime.UtcNow,
                    ApiOk = true,
                    TempC = 28,
                    WindSpeedKmh = 30,
                    Condition = "Thunderstorm",
                    WeatherId = 211,
                    Humidity = 80,
                    Rain_1h_mm = 0
                };
            }
            else
            {
                weather = await _openWeather.GetWeatherAsync(site.Latitude, site.Longitude, ct);
            }

            var results = MonitoringRuleEngine.Evaluate(weather);
            var now = DateTime.UtcNow;

            foreach (var r in results)
            {
                var alert = new MonitoringAlert
                {
                    OrgId = orgId,
                    MonitoringSiteId = siteId,
                    RuleCode = r.RuleCode,
                    RuleName = r.RuleName,
                    MeasuredValues = r.MeasuredValues.Length > 500 ? r.MeasuredValues.Substring(0, 500) : r.MeasuredValues,
                    Severity = r.Severity,
                    TriggeredAt = now
                };

                if (r.Severity == "High" || r.Severity == "Critical")
                {
                    var existingRiskId = await _riskService.GetExistingOpenRiskIdForSiteRuleAsync(orgId, siteId, r.RuleCode, 6, ct);
                    if (existingRiskId.HasValue)
                    {
                        alert.RiskId = existingRiskId.Value;
                        await _riskService.EnsureMitigationPlanExistsAsync(existingRiskId.Value, orgId, userId, ct);
                    }
                    else
                    {
                        var desc = $"Rule: {r.RuleCode}. Measured: {r.MeasuredValues}. At {now:O}.";
                        if (desc.Length > 500) desc = desc.Substring(0, 500);
                        var risk = await _riskService.CreateRiskFromMonitoringAsync(orgId, siteId, r.RuleCode,
                            $"[AUTO] {r.RuleName} - {site.Name}", r.Severity, userId, site.Name, desc, site.SiteId, ct);
                        if (risk != null)
                        {
                            alert.RiskId = risk.RiskId;
                            await _riskService.EnsureMitigationPlanExistsAsync(risk.RiskId, orgId, userId, ct);
                            var evidence = $"Rule={r.RuleCode}, Values={r.MeasuredValues}, At={now:O}";
                            _riskService.AddAuditLog(db, orgId, userId, "MonitoringEvent", risk.RiskId, "AutoRiskCreated", evidence.Length > 255 ? evidence.Substring(0, 255) : evidence, null);
                        }
                    }
                }

                db.MonitoringAlerts.Add(alert);
            }

            await db.SaveChangesAsync(ct);
            return (now, weather.ApiOk);
        }
    }
}
