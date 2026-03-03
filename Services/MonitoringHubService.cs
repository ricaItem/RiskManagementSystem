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
        public string Status { get; set; } = "Active";
        public DateTime TriggeredAt { get; set; }
        public DateTime? ResolvedAtUtc { get; set; }
        public DateTime? AcknowledgedAtUtc { get; set; }
        public string? AcknowledgedByUserId { get; set; }
        public string? AcknowledgedByDisplayName { get; set; }
        public int? RiskId { get; set; }
    }

    public class MonitoringHubSiteItem
    {
        public int MonitoringSiteId { get; set; }
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
        private const int AutoRiskDedupeHours = 12;

        public MonitoringHubService(ITenantDbFactory tenantDbFactory, IOpenWeatherService openWeather, RiskService riskService)
        {
            _tenantDbFactory = tenantDbFactory;
            _openWeather = openWeather;
            _riskService = riskService;
        }

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
                monitoringBySite = new Dictionary<int, MonitoringSite>();
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
            var fromSnap = await db.MonitoringSnapshots.AsNoTracking()
                .Where(s => s.OrgId == orgId && s.MonitoringSiteId == siteId)
                .MaxAsync(s => (DateTime?)s.CapturedAtUtc, ct);
            if (fromSnap.HasValue) return fromSnap.Value;
            return await db.MonitoringAlerts.AsNoTracking()
                .Where(a => a.OrgId == orgId && a.MonitoringSiteId == siteId)
                .MaxAsync(a => (DateTime?)a.TriggeredAt, ct);
        }

        public async Task<List<MonitoringAlertViewModel>> GetRecentAlertsAsync(int orgId, int? siteId, int top = 20, CancellationToken ct = default)
        {
            await using var db = await _tenantDbFactory.CreateAsync(orgId);
            var q = db.MonitoringAlerts.AsNoTracking().Where(a => a.OrgId == orgId);
            if (siteId.HasValue) q = q.Where(a => a.MonitoringSiteId == siteId.Value);
            var list = await q.OrderByDescending(a => a.TriggeredAt).Take(top)
                .Select(a => new MonitoringAlertViewModel
                {
                    AlertId = a.AlertId,
                    RuleName = a.RuleName,
                    MeasuredValues = a.MeasuredValues,
                    Severity = a.Severity,
                    Status = a.Status ?? "Active",
                    TriggeredAt = a.TriggeredAt,
                    ResolvedAtUtc = a.ResolvedAtUtc,
                    AcknowledgedAtUtc = a.AcknowledgedAtUtc,
                    AcknowledgedByUserId = a.AcknowledgedByUserId,
                    RiskId = a.RiskId
                }).ToListAsync(ct);
            return list;
        }

        public async Task EnsureDefaultRulesAsync(TenantDbContext db, int orgId, CancellationToken ct = default)
        {
            if (await db.MonitoringRules.AnyAsync(r => r.OrgId == orgId, ct)) return;

            var defaults = new[]
            {
                new MonitoringRule { OrgId = orgId, Name = "High wind speed", Metric = "WindSpeed", Threshold = 40, Operator = ">", Severity = "High", CooldownMinutes = 60, Enabled = true },
                new MonitoringRule { OrgId = orgId, Name = "Critical wind speed", Metric = "WindSpeed", Threshold = 60, Operator = ">", Severity = "Critical", CooldownMinutes = 60, Enabled = true },
                new MonitoringRule { OrgId = orgId, Name = "Heavy rain", Metric = "RainMm", Threshold = 10, Operator = ">=", Severity = "High", CooldownMinutes = 60, Enabled = true },
                new MonitoringRule { OrgId = orgId, Name = "High heat index", Metric = "HeatIndex", Threshold = 40, Operator = ">=", Severity = "High", CooldownMinutes = 120, Enabled = true },
                new MonitoringRule { OrgId = orgId, Name = "Critical heat index", Metric = "HeatIndex", Threshold = 45, Operator = ">=", Severity = "Critical", CooldownMinutes = 120, Enabled = true }
            };
            foreach (var r in defaults)
                db.MonitoringRules.Add(r);
            await db.SaveChangesAsync(ct);
        }

        private static decimal GetMetricValue(WeatherSnapshot w, string metric)
        {
            switch (metric)
            {
                case "Temperature": return (decimal)w.TempC;
                case "WindSpeed": return (decimal)w.WindSpeedKmh;
                case "Humidity": return (decimal)w.Humidity;
                case "RainMm": return (decimal)w.Rain_1h_mm;
                case "HeatIndex":
                    var hi = w.HeatIndexC ?? MonitoringRuleEngine.HeatIndexC(w.TempC, w.Humidity);
                    return (decimal)hi;
                default: return 0;
            }
        }

        private static bool EvalRule(decimal value, string op, decimal threshold)
        {
            return op switch
            {
                ">" => value > threshold,
                ">=" => value >= threshold,
                "<" => value < threshold,
                "<=" => value <= threshold,
                "=" => value == threshold,
                _ => value > threshold
            };
        }

        public async Task<(DateTime? LastSyncUtc, bool ApiOk)> RunSyncForSiteAsync(int orgId, int siteId, string userId, string? simulate = null, CancellationToken ct = default)
        {
            await using var db = await _tenantDbFactory.CreateAsync(orgId);
            var site = await db.MonitoringSites.AsNoTracking().FirstOrDefaultAsync(s => s.MonitoringSiteId == siteId && s.OrgId == orgId, ct);
            if (site == null) return (null, false);

            WeatherSnapshot weather;
            if (string.Equals(simulate, "wind", StringComparison.OrdinalIgnoreCase))
                weather = new WeatherSnapshot { FetchedAt = DateTime.UtcNow, ApiOk = true, TempC = 30, WindSpeedKmh = 55, Condition = "Windy", WeatherId = 0, Humidity = 50, Rain_1h_mm = 0 };
            else if (string.Equals(simulate, "storm", StringComparison.OrdinalIgnoreCase))
                weather = new WeatherSnapshot { FetchedAt = DateTime.UtcNow, ApiOk = true, TempC = 28, WindSpeedKmh = 30, Condition = "Thunderstorm", WeatherId = 211, Humidity = 80, Rain_1h_mm = 0 };
            else
                weather = await _openWeather.GetWeatherAsync(site.Latitude, site.Longitude, ct);

            var now = DateTime.UtcNow;
            var snapshot = new MonitoringSnapshot
            {
                OrgId = orgId,
                MonitoringSiteId = siteId,
                CapturedAtUtc = now,
                Temperature = (decimal)weather.TempC,
                WindSpeed = (decimal)weather.WindSpeedKmh,
                Humidity = weather.Humidity,
                RainMm = (decimal)weather.Rain_1h_mm,
                Condition = weather.Condition,
                RawJson = null
            };
            db.MonitoringSnapshots.Add(snapshot);

            await EnsureDefaultRulesAsync(db, orgId, ct);

            var triggeredByRule = new List<(int? RuleId, string RuleCode, string RuleName, string Severity, string MeasuredValues)>();
            var dbRules = await db.MonitoringRules.AsNoTracking().Where(r => r.OrgId == orgId && r.Enabled).ToListAsync(ct);
            foreach (var rule in dbRules)
            {
                var value = GetMetricValue(weather, rule.Metric);
                if (!EvalRule(value, rule.Operator, rule.Threshold)) continue;
                var ruleCode = $"Rule_{rule.RuleId}";
                var measured = $"{rule.Metric}={value:F1}, threshold {rule.Operator} {rule.Threshold}";
                triggeredByRule.Add((rule.RuleId, ruleCode, rule.Name, rule.Severity, measured));
            }

            var staticResults = MonitoringRuleEngine.Evaluate(weather);
            foreach (var r in staticResults)
            {
                if (triggeredByRule.Any(t => t.RuleName == r.RuleName)) continue;
                triggeredByRule.Add((null, r.RuleCode, r.RuleName, r.Severity, r.MeasuredValues.Length > 500 ? r.MeasuredValues.Substring(0, 500) : r.MeasuredValues));
            }

            var since3h = now.AddHours(-3);
            var snapshots3h = await db.MonitoringSnapshots.AsNoTracking()
                .Where(s => s.OrgId == orgId && s.MonitoringSiteId == siteId && s.CapturedAtUtc >= since3h)
                .OrderBy(s => s.CapturedAtUtc)
                .Select(s => new { s.RainMm, s.CapturedAtUtc })
                .ToListAsync(ct);
            var rainAccumulation3h = snapshots3h.Sum(s => s.RainMm ?? 0);
            var rainSnapshotCount = snapshots3h.Count(s => (s.RainMm ?? 0) > 0);
            if (rainAccumulation3h >= 30 || rainSnapshotCount >= 3)
            {
                var floodMeasured = $"RainAccumulationLast3Hours={rainAccumulation3h:F1}mm (threshold 30mm); rainy snapshots={rainSnapshotCount}";
                if (!triggeredByRule.Any(t => t.RuleCode == "Flood_risk_likely"))
                    triggeredByRule.Add((null, "Flood_risk_likely", "Flood risk likely (next 3 hours)", "High", floodMeasured.Length > 500 ? floodMeasured.Substring(0, 500) : floodMeasured));
            }

            var activeAlertsForSite = await db.MonitoringAlerts
                .Where(a => a.OrgId == orgId && a.MonitoringSiteId == siteId && a.Status == "Active")
                .ToListAsync(ct);

            foreach (var t in triggeredByRule)
            {
                var ruleId = t.RuleId;
                var ruleName = t.RuleName;
                var ruleCode = t.RuleCode;
                var cooldownMins = dbRules.FirstOrDefault(r => r.RuleId == ruleId)?.CooldownMinutes ?? 60;
                var cooldownUntil = now.AddMinutes(-cooldownMins);
                var existing = activeAlertsForSite.FirstOrDefault(a =>
                    (ruleId.HasValue && a.RuleId == ruleId) || (!ruleId.HasValue && a.RuleName == ruleName));
                if (existing != null)
                {
                    if (existing.TriggeredAt >= cooldownUntil)
                    {
                        existing.MeasuredValues = t.MeasuredValues;
                        existing.TriggeredAt = now;
                    }
                }
                else
                {
                    var alert = new MonitoringAlert
                    {
                        OrgId = orgId,
                        MonitoringSiteId = siteId,
                        RuleId = ruleId,
                        RuleCode = ruleCode,
                        RuleName = ruleName,
                        MeasuredValues = t.MeasuredValues.Length > 500 ? t.MeasuredValues.Substring(0, 500) : t.MeasuredValues,
                        Severity = t.Severity,
                        Status = "Active",
                        TriggeredAt = now
                    };
                    if (t.Severity == "High" || t.Severity == "Critical")
                    {
                        var existingRiskId = await _riskService.GetExistingOpenRiskIdForSiteRuleAsync(orgId, siteId, ruleCode, AutoRiskDedupeHours, ct);
                        if (existingRiskId.HasValue)
                        {
                            alert.RiskId = existingRiskId.Value;
                            var risk = await db.Risks.FirstOrDefaultAsync(r => r.RiskId == existingRiskId.Value, ct);
                            if (risk != null)
                            {
                                risk.UpdatedAt = now;
                                risk.Status = "MitigationRequired";
                                var desc = $"Rule: {ruleCode}. Measured: {t.MeasuredValues}. At {now:O}.";
                                risk.Description = desc.Length > 500 ? desc.Substring(0, 500) : desc;
                            }
                            await _riskService.EnsureAutoRiskEvaluationForRiskAsync(existingRiskId.Value, orgId, t.Severity, t.MeasuredValues, userId, ct);
                            await _riskService.EnsureMitigationPlanExistsAsync(existingRiskId.Value, orgId, userId, t.Severity, ct);
                        }
                        else
                        {
                            var title = $"[AUTO] {ruleName} - {site.Name}";
                            var desc = $"Rule: {ruleCode}. Measured: {t.MeasuredValues}. At {now:O}.";
                            if (desc.Length > 500) desc = desc.Substring(0, 500);
                            var risk = await _riskService.CreateRiskFromMonitoringAsync(orgId, siteId, ruleCode, title, t.Severity, userId, site.Name, desc, site.SiteId, ct);
                            if (risk != null)
                            {
                                alert.RiskId = risk.RiskId;
                                await _riskService.EnsureMitigationPlanExistsAsync(risk.RiskId, orgId, userId, t.Severity, ct);
                                _riskService.AddAuditLog(db, orgId, userId, "MonitoringEvent", risk.RiskId, "AutoRiskCreated", desc.Length > 255 ? desc.Substring(0, 255) : desc, null);
                            }
                        }
                    }
                    db.MonitoringAlerts.Add(alert);
                    activeAlertsForSite.Add(alert);
                }
            }

            var triggeredRuleKeys = triggeredByRule.Select(x => x.RuleId.HasValue ? $"R{x.RuleId}" : x.RuleName).ToHashSet();
            foreach (var alert in activeAlertsForSite.Where(a => a.AlertId != 0))
            {
                var key = alert.RuleId.HasValue ? $"R{alert.RuleId}" : alert.RuleName;
                if (triggeredRuleKeys.Contains(key)) continue;
                alert.Status = "Resolved";
                alert.ResolvedAtUtc = now;
            }

            await db.SaveChangesAsync(ct);
            return (now, weather.ApiOk);
        }

        public async Task<bool> AcknowledgeAlertAsync(int orgId, int alertId, string userId, CancellationToken ct = default)
        {
            await using var db = await _tenantDbFactory.CreateAsync(orgId);
            var alert = await db.MonitoringAlerts.FirstOrDefaultAsync(a => a.AlertId == alertId && a.OrgId == orgId && a.Status == "Active", ct);
            if (alert == null) return false;
            alert.AcknowledgedAtUtc = DateTime.UtcNow;
            alert.AcknowledgedByUserId = userId;
            _riskService.AddAuditLog(db, orgId, userId, "MonitoringAlert", alertId, "AlertAcknowledged", $"Alert {alert.RuleName} acknowledged", null);
            await db.SaveChangesAsync(ct);
            return true;
        }

        public async Task<MonitoringAlert?> GetAlertAsync(int orgId, int alertId, CancellationToken ct = default)
        {
            await using var db = await _tenantDbFactory.CreateAsync(orgId);
            return await db.MonitoringAlerts.AsNoTracking().FirstOrDefaultAsync(a => a.AlertId == alertId && a.OrgId == orgId, ct);
        }

        public async Task<int?> CreateRiskFromAlertAndLinkAsync(int orgId, int alertId, string userId, CancellationToken ct = default)
        {
            await using var db = await _tenantDbFactory.CreateAsync(orgId);
            var alert = await db.MonitoringAlerts.FirstOrDefaultAsync(a => a.AlertId == alertId && a.OrgId == orgId, ct);
            if (alert == null || alert.RiskId.HasValue) return null;
            var site = await db.MonitoringSites.AsNoTracking().FirstOrDefaultAsync(s => s.MonitoringSiteId == alert.MonitoringSiteId && s.OrgId == orgId, ct);
            var siteName = site?.Name ?? "";
            var risk = await _riskService.CreateRiskFromMonitoringAsync(orgId, alert.MonitoringSiteId, alert.RuleCode,
                $"[AUTO] {alert.RuleName} - {siteName}", alert.Severity, userId, siteName, alert.MeasuredValues, site?.SiteId, ct);
            if (risk == null) return null;
            alert.RiskId = risk.RiskId;
            await db.SaveChangesAsync(ct);
            return risk.RiskId;
        }

        public async Task<List<MonitoringMapItem>> GetMapDataAsync(int orgId, CancellationToken ct = default)
        {
            await using var db = await _tenantDbFactory.CreateAsync(orgId);
            
            // Get all monitoring sites
            var sites = await db.MonitoringSites.AsNoTracking()
                .Where(s => s.OrgId == orgId)
                .Select(s => new { s.MonitoringSiteId, s.Name, s.Latitude, s.Longitude })
                .ToListAsync(ct);
                
            // Get active alerts grouped by site
            var activeAlerts = await db.MonitoringAlerts.AsNoTracking()
                .Where(a => a.OrgId == orgId && a.Status == "Active")
                .Select(a => new { a.MonitoringSiteId, a.Severity })
                .ToListAsync(ct);

            var siteIds = sites.Select(s => s.MonitoringSiteId).ToList();
            var riskCountBySite = siteIds.Count == 0 ? new Dictionary<int, int>() : await db.Risks.AsNoTracking()
                .Where(r => r.OrgId == orgId && r.LocationId != null && siteIds.Contains(r.LocationId.Value) && r.DeletedAt == null
                    && r.Status != "Closed_Invalid" && r.Status != "Rejected")
                .GroupBy(r => r.LocationId!.Value)
                .Select(g => new { SiteId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.SiteId, x => x.Count, ct);

            // Get latest snapshot for each site for current weather
            // Using a group by approach to get the latest snapshot per site is inefficient in EF Core 3.x/5.x sometimes, 
            // so we'll fetch recent snapshots and process in memory or use a window function if raw SQL. 
            // For simplicity and safety across providers, we'll fetch the last snapshot for each site.
            // Optimized: Get max ID per site (assuming ID increases with time) or Max CapturedAt
            var latestSnapshots = await db.MonitoringSnapshots.AsNoTracking()
                .Where(s => s.OrgId == orgId)
                .GroupBy(s => s.MonitoringSiteId)
                .Select(g => g.OrderByDescending(s => s.CapturedAtUtc).FirstOrDefault())
                .ToListAsync(ct);
                
            var mapItems = new List<MonitoringMapItem>();
            
            foreach (var site in sites)
            {
                var siteAlerts = activeAlerts.Where(a => a.MonitoringSiteId == site.MonitoringSiteId).ToList();
                var maxSeverity = siteAlerts.Any(a => a.Severity == "Critical") ? "Critical" 
                                : siteAlerts.Any(a => a.Severity == "High") ? "High" 
                                : siteAlerts.Any() ? "Medium" : "None";
                                
                var snap = latestSnapshots.FirstOrDefault(s => s?.MonitoringSiteId == site.MonitoringSiteId);
                
                mapItems.Add(new MonitoringMapItem
                {
                    SiteId = site.MonitoringSiteId,
                    Name = site.Name,
                    Latitude = site.Latitude,
                    Longitude = site.Longitude,
                    ActiveAlertCount = siteAlerts.Count,
                    ActiveRiskCount = riskCountBySite.GetValueOrDefault(site.MonitoringSiteId, 0),
                    MaxSeverity = maxSeverity,
                    TempC = snap?.Temperature,
                    Condition = snap?.Condition,
                    LastSyncUtc = snap?.CapturedAtUtc
                });
            }
            
            return mapItems;
        }
        
        public async Task<MonitoringSiteDetailsDto?> GetSiteDetailsAsync(int orgId, int siteId, CancellationToken ct = default)
        {
            await using var db = await _tenantDbFactory.CreateAsync(orgId);
            var site = await db.MonitoringSites.AsNoTracking().FirstOrDefaultAsync(s => s.MonitoringSiteId == siteId && s.OrgId == orgId, ct);
            if (site == null) return null;
            
            var alerts = await GetRecentAlertsAsync(orgId, siteId, 20, ct);
            var snapshots = await db.MonitoringSnapshots.AsNoTracking()
                .Where(s => s.OrgId == orgId && s.MonitoringSiteId == siteId && s.CapturedAtUtc >= DateTime.UtcNow.AddHours(-6))
                .OrderBy(s => s.CapturedAtUtc)
                .Select(s => new MonitoringSnapshotDto { CapturedAtUtc = s.CapturedAtUtc, TempC = s.Temperature, WindKmh = s.WindSpeed, RainMm = s.RainMm })
                .ToListAsync(ct);

            var lastSync = await GetLastSyncUtcAsync(orgId, siteId, ct);
            var apiHealthOk = lastSync.HasValue && (DateTime.UtcNow - lastSync.Value).TotalMinutes <= 30;

            return new MonitoringSiteDetailsDto
            {
                SiteId = site.MonitoringSiteId,
                Name = site.Name,
                Alerts = alerts,
                History = snapshots,
                LastSyncUtc = lastSync,
                ApiHealthOk = apiHealthOk
            };
        }

        public async Task<bool> ResolveAlertAsync(int orgId, int alertId, string userId, CancellationToken ct = default)
        {
            await using var db = await _tenantDbFactory.CreateAsync(orgId);
            var alert = await db.MonitoringAlerts.FirstOrDefaultAsync(a => a.AlertId == alertId && a.OrgId == orgId && a.Status == "Active", ct);
            if (alert == null) return false;
            alert.Status = "Resolved";
            alert.ResolvedAtUtc = DateTime.UtcNow;
            _riskService.AddAuditLog(db, orgId, userId, "MonitoringAlert", alertId, "AlertResolved", $"Alert {alert.RuleName} resolved manually", null);
            await db.SaveChangesAsync(ct);
            return true;
        }
    }
    
    public class MonitoringMapItem
    {
        public int SiteId { get; set; }
        public string Name { get; set; } = "";
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public int ActiveAlertCount { get; set; }
        public int ActiveRiskCount { get; set; }
        public string MaxSeverity { get; set; } = "None";
        public decimal? TempC { get; set; }
        public string? Condition { get; set; }
        public DateTime? LastSyncUtc { get; set; }
    }
    
    public class MonitoringSiteDetailsDto
    {
        public int SiteId { get; set; }
        public string Name { get; set; } = "";
        public List<MonitoringAlertViewModel> Alerts { get; set; } = new();
        public List<MonitoringSnapshotDto> History { get; set; } = new();
        public DateTime? LastSyncUtc { get; set; }
        public bool ApiHealthOk { get; set; }
    }
    
    public class MonitoringSnapshotDto
    {
        public DateTime CapturedAtUtc { get; set; }
        public decimal? TempC { get; set; }
        public decimal? WindKmh { get; set; }
        public decimal? RainMm { get; set; }
    }
}

