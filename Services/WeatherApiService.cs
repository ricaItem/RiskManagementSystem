using System.Collections.Concurrent;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WEB_Sentro.Services
{
    /// <summary>DTOs for WeatherAPI.com forecast.json response.</summary>
    internal class WeatherApiForecastDto
    {
        [JsonPropertyName("current")]
        public WeatherApiCurrentDto? Current { get; set; }
        [JsonPropertyName("alerts")]
        public WeatherApiAlertsDto? Alerts { get; set; }
    }

    internal class WeatherApiCurrentDto
    {
        [JsonPropertyName("temp_c")]
        public double TempC { get; set; }
        [JsonPropertyName("wind_kph")]
        public double WindKph { get; set; }
        [JsonPropertyName("humidity")]
        public int Humidity { get; set; }
        [JsonPropertyName("condition")]
        public WeatherApiConditionDto? Condition { get; set; }
        [JsonPropertyName("precip_mm")]
        public double PrecipMm { get; set; }
        /// <summary>Heat index in Celsius; may be omitted when not applicable.</summary>
        [JsonPropertyName("heatindex_c")]
        public double? HeatindexC { get; set; }
    }

    internal class WeatherApiConditionDto
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }
        [JsonPropertyName("code")]
        public int Code { get; set; }
    }

    internal class WeatherApiAlertsDto
    {
        [JsonPropertyName("alert")]
        public WeatherApiAlertItemDto[]? Alert { get; set; }
    }

    internal class WeatherApiAlertItemDto
    {
        [JsonPropertyName("headline")]
        public string? Headline { get; set; }
        [JsonPropertyName("event")]
        public string? Event { get; set; }
        [JsonPropertyName("desc")]
        public string? Desc { get; set; }
        [JsonPropertyName("effective")]
        public string? Effective { get; set; }
        [JsonPropertyName("expires")]
        public string? Expires { get; set; }
    }

    /// <summary>Weather provider using WeatherAPI.com forecast endpoint. Implements same contract as OpenWeather for Monitoring + rule engine.</summary>
    public class WeatherApiService : IOpenWeatherService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _config;
        private static readonly ConcurrentDictionary<string, (WeatherSnapshot s, DateTime until)> Cache = new();
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);

        private const string BaseUrl = "https://api.weatherapi.com/v1/forecast.json";

        public WeatherApiService(IHttpClientFactory httpClientFactory, IConfiguration config)
        {
            _httpClientFactory = httpClientFactory;
            _config = config;
        }

        public async Task<WeatherSnapshot> GetWeatherAsync(double lat, double lon, CancellationToken ct = default)
        {
            var key = $"{lat:F4}_{lon:F4}";
            if (Cache.TryGetValue(key, out var entry) && DateTime.UtcNow < entry.until)
                return entry.s;

            var apiKey = _config["WeatherApi:ApiKey"] ?? "";
            var snapshot = new WeatherSnapshot { FetchedAt = DateTime.UtcNow, ApiOk = false };

            if (string.IsNullOrEmpty(apiKey))
            {
                Cache.TryAdd(key, (snapshot, DateTime.UtcNow + CacheTtl));
                return snapshot;
            }

            var q = $"{lat},{lon}";
            var url = $"{BaseUrl}?key={Uri.EscapeDataString(apiKey)}&q={Uri.EscapeDataString(q)}&days=1&aqi=no&alerts=yes";

            try
            {
                var client = _httpClientFactory.CreateClient();
                var response = await client.GetAsync(url, ct);
                snapshot.ApiOk = response.IsSuccessStatusCode;
                if (!response.IsSuccessStatusCode)
                {
                    Cache.TryAdd(key, (snapshot, DateTime.UtcNow + CacheTtl));
                    return snapshot;
                }

                var json = await response.Content.ReadAsStringAsync(ct);
                var dto = JsonSerializer.Deserialize<WeatherApiForecastDto>(json);
                if (dto?.Current == null)
                {
                    Cache.TryAdd(key, (snapshot, DateTime.UtcNow + CacheTtl));
                    return snapshot;
                }

                var cur = dto.Current;
                snapshot.TempC = cur.TempC;
                snapshot.WindSpeedKmh = cur.WindKph;
                snapshot.Humidity = cur.Humidity;
                snapshot.Condition = cur.Condition?.Text;
                snapshot.WeatherId = cur.Condition?.Code ?? 0;
                snapshot.Rain_1h_mm = cur.PrecipMm;
                if (cur.HeatindexC.HasValue)
                    snapshot.HeatIndexC = cur.HeatindexC.Value;

                if (HasThunderOrStormAlert(dto.Alerts))
                    snapshot.WeatherId = 211;

                Cache[key] = (snapshot, DateTime.UtcNow + CacheTtl);
                return snapshot;
            }
            catch
            {
                Cache.TryAdd(key, (snapshot, DateTime.UtcNow + CacheTtl));
                return snapshot;
            }
        }

        private static bool HasThunderOrStormAlert(WeatherApiAlertsDto? alerts)
        {
            if (alerts?.Alert == null) return false;
            var terms = new[] { "thunder", "storm" };
            foreach (var a in alerts.Alert)
            {
                var headline = a.Headline ?? "";
                var ev = a.Event ?? "";
                var desc = a.Desc ?? "";
                var combined = $"{headline} {ev} {desc}".ToLowerInvariant();
                if (terms.Any(t => combined.Contains(t)))
                    return true;
            }
            return false;
        }
    }
}
