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

        public async Task<List<MonitoringSite>> GetSitesAsync(int orgId, CancellationToken ct = default)
        {
            await using var db = await _tenantDbFactory.CreateAsync(orgId);
            return await db.MonitoringSites.AsNoTracking().Where(s => s.OrgId == orgId).OrderBy(s => s.Name).ToListAsync(ct);
        }

        public async Task<DateTime?> GetLastSyncUtcAsync(int orgId, int siteId, CancellationToken ct = default)
        {
            await using var db = await _tenantDbFactory.CreateAsync(orgId);
            var t = await db.MonitoringAlerts.AsNoTracking().Where(a => a.OrgId == orgId && a.SiteId == siteId).MaxAsync(a => (DateTime?)a.TriggeredAt, ct);
            return t;
        }

        public async Task<List<MonitoringAlertViewModel>> GetRecentAlertsAsync(int orgId, int? siteId, int top = 20, CancellationToken ct = default)
        {
            await using var db = await _tenantDbFactory.CreateAsync(orgId);
            var q = db.MonitoringAlerts.AsNoTracking().Where(a => a.OrgId == orgId);
            if (siteId.HasValue) q = q.Where(a => a.SiteId == siteId.Value);
            var list = await q.OrderByDescending(a => a.TriggeredAt).Take(top)
                .Select(a => new MonitoringAlertViewModel { AlertId = a.AlertId, RuleName = a.RuleName, MeasuredValues = a.MeasuredValues, Severity = a.Severity, TriggeredAt = a.TriggeredAt, RiskId = a.RiskId })
                .ToListAsync(ct);
            return list;
        }

        public async Task<(DateTime? LastSyncUtc, bool ApiOk)> RunSyncForSiteAsync(int orgId, int siteId, string userId, string? simulate = null, CancellationToken ct = default)
        {
            await using var db = await _tenantDbFactory.CreateAsync(orgId);
            var site = await db.MonitoringSites.AsNoTracking().FirstOrDefaultAsync(s => s.SiteId == siteId && s.OrgId == orgId, ct);
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
                    SiteId = siteId,
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
                            $"[AUTO] {r.RuleName} - {site.Name}", r.Severity, userId, site.Name, desc, ct);
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
