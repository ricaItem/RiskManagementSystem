using WEB_Sentro.Services.Weather;

namespace WEB_Sentro.Services
{
    public class RiskMonitoringService
    {
        private readonly RiskService _riskService;
        private readonly IWeatherClient _weatherClient;
        private static readonly TimeSpan DedupeWindow = TimeSpan.FromMinutes(30);

        public RiskMonitoringService(RiskService riskService, IWeatherClient weatherClient)
        {
            _riskService = riskService;
            _weatherClient = weatherClient;
        }

        /// <summary>
        /// Fetches current weather and optionally creates risks when thresholds are exceeded.
        /// Returns the weather snapshot and whether at least one risk was auto-created.
        /// </summary>
        public async Task<(WeatherSnapshot snap, bool created)> SyncWeatherAndAutoCreateAsync(
            int orgId,
            string userId,
            string projectName,
            double lat,
            double lon,
            string siteLabel,
            string? ip,
            CancellationToken ct = default)
        {
            var snap = await _weatherClient.GetCurrentAsync(lat, lon, ct);
            var anyCreated = false;

            // Wind >= 40 kph
            if (snap.WindKph >= 40)
            {
                const string hazardKey = "Wind";
                if (!await _riskService.HasRecentAutoWeatherRiskAsync(orgId, siteLabel, hazardKey, DedupeWindow, ct))
                {
                    var title = $"[AUTO] High wind hazard - {snap.WindKph:F0} km/h at {siteLabel}";
                    var risk = await _riskService.CreateRiskAsync(orgId, userId, title, "Environmental", "WeatherAPI", siteLabel,
                        $"Automated risk: wind speed {snap.WindKph:F1} km/h exceeded 40 km/h threshold. Observed at {snap.ObservedAtUtc:u}.", "For_Review", ct);
                    _riskService.AddAuditLog(orgId, userId, "Risk", risk.RiskId, "RiskAutoCreated", title, ip);
                    anyCreated = true;
                }
            }

            // Condition contains "Thunder" (lightning hazard)
            if (snap.Condition.Contains("Thunder", StringComparison.OrdinalIgnoreCase))
            {
                const string hazardKey = "Thunderstorm";
                if (!await _riskService.HasRecentAutoWeatherRiskAsync(orgId, siteLabel, hazardKey, DedupeWindow, ct))
                {
                    var title = $"[AUTO] Thunderstorm / lightning hazard at {siteLabel}";
                    var risk = await _riskService.CreateRiskAsync(orgId, userId, title, "Environmental", "WeatherAPI", siteLabel,
                        $"Automated risk: weather condition '{snap.Condition}'. Observed at {snap.ObservedAtUtc:u}.", "For_Review", ct);
                    _riskService.AddAuditLog(orgId, userId, "Risk", risk.RiskId, "RiskAutoCreated", title, ip);
                    anyCreated = true;
                }
            }

            // Rain >= 10 mm/h (slip/flood hazard)
            if (snap.RainMm1h >= 10)
            {
                const string hazardKey = "Heavy Rain";
                if (!await _riskService.HasRecentAutoWeatherRiskAsync(orgId, siteLabel, hazardKey, DedupeWindow, ct))
                {
                    var title = $"[AUTO] Heavy rain / slip-flood hazard - {snap.RainMm1h:F0} mm/h at {siteLabel}";
                    var risk = await _riskService.CreateRiskAsync(orgId, userId, title, "Environmental", "WeatherAPI", siteLabel,
                        $"Automated risk: rain {snap.RainMm1h:F1} mm/h exceeded 10 mm/h threshold. Observed at {snap.ObservedAtUtc:u}.", "For_Review", ct);
                    _riskService.AddAuditLog(orgId, userId, "Risk", risk.RiskId, "RiskAutoCreated", title, ip);
                    anyCreated = true;
                }
            }

            if (anyCreated)
                await _riskService.SaveChangesAsync(ct);

            return (snap, anyCreated);
        }
    }
}
