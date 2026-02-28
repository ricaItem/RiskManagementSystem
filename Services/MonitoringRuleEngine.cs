namespace WEB_Sentro.Services
{
    public class MonitoringRuleResult
    {
        public string RuleCode { get; set; } = null!;
        public string RuleName { get; set; } = null!;
        public string Severity { get; set; } = null!;
        public string MeasuredValues { get; set; } = null!;
    }

    public static class MonitoringRuleEngine
    {
        private const double WindThresholdKmh = 40;
        private const double HeavyRainThresholdMm = 10;
        private const double HeatIndexThreshold = 40;
        private const double TempHeatThreshold = 38;

        public static List<MonitoringRuleResult> Evaluate(WeatherSnapshot w)
        {
            var results = new List<MonitoringRuleResult>();
            if (!w.ApiOk) return results;

            if (w.WeatherId >= 200 && w.WeatherId <= 232)
            {
                results.Add(new MonitoringRuleResult
                {
                    RuleCode = "Weather_Thunderstorm",
                    RuleName = "Thunderstorm alert",
                    Severity = "Critical",
                    MeasuredValues = $"weather_id={w.WeatherId}, condition={w.Condition}"
                });
            }

            if (w.WindSpeedKmh >= WindThresholdKmh)
            {
                results.Add(new MonitoringRuleResult
                {
                    RuleCode = "Weather_HighWind",
                    RuleName = "High wind speed",
                    Severity = w.WindSpeedKmh >= 60 ? "Critical" : "High",
                    MeasuredValues = $"wind_speed_kmh={w.WindSpeedKmh:F1}"
                });
            }

            if ((w.WeatherId >= 502 && w.WeatherId <= 504) || w.Rain_1h_mm >= HeavyRainThresholdMm)
            {
                results.Add(new MonitoringRuleResult
                {
                    RuleCode = "Weather_HeavyRain",
                    RuleName = "Heavy rain",
                    Severity = "High",
                    MeasuredValues = w.Rain_1h_mm >= HeavyRainThresholdMm
                        ? $"rain_1h_mm={w.Rain_1h_mm:F1}, condition={w.Condition}"
                        : $"weather_id={w.WeatherId}, condition={w.Condition}"
                });
            }

            var heatIndex = w.HeatIndexC ?? HeatIndex(w.TempC, w.Humidity);
            if (heatIndex >= HeatIndexThreshold || w.TempC >= TempHeatThreshold)
            {
                results.Add(new MonitoringRuleResult
                {
                    RuleCode = "Weather_Heat",
                    RuleName = "High heat index",
                    Severity = heatIndex >= 45 ? "Critical" : "High",
                    MeasuredValues = $"temp_c={w.TempC:F1}, humidity={w.Humidity}, heat_index={heatIndex:F1}"
                });
            }

            return results;
        }

        private static double HeatIndex(double tempC, int humidity)
        {
            if (tempC < 27) return tempC;
            var t = tempC * 9 / 5 + 32;
            var rh = (double)humidity;
            var hi = -42.379 + 2.04901523 * t + 10.14333127 * rh - 0.22475541 * t * rh
                - 6.83783e-3 * t * t - 5.481717e-2 * rh * rh + 1.22874e-3 * t * t * rh
                + 8.5282e-4 * t * rh * rh - 1.99e-6 * t * t * rh * rh;
            return (hi - 32) * 5 / 9;
        }
    }
}
