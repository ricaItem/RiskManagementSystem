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
                snapshot.WeatherId = MapToOpenWeatherCode(cur.Condition?.Code ?? 0);
                snapshot.Rain_1h_mm = cur.PrecipMm;
                if (cur.HeatindexC.HasValue)
                    snapshot.HeatIndexC = cur.HeatindexC.Value;

                Cache[key] = (snapshot, DateTime.UtcNow + CacheTtl);
                return snapshot;
            }
            catch
            {
                Cache.TryAdd(key, (snapshot, DateTime.UtcNow + CacheTtl));
                return snapshot;
            }
        }

        private static int MapToOpenWeatherCode(int code)
        {
            return code switch
            {
                1000 => 800, // Sunny
                1003 => 801, // Partly cloudy
                1006 => 803, // Cloudy
                1009 => 804, // Overcast
                1030 => 701, // Mist
                1063 => 500, // Patchy rain possible
                1066 => 600, // Patchy snow possible
                1069 => 611, // Patchy sleet possible
                1072 => 511, // Patchy freezing drizzle possible
                1087 => 200, // Thundery outbreaks possible
                1114 => 601, // Blowing snow
                1117 => 602, // Blizzard
                1135 => 741, // Fog
                1147 => 741, // Freezing fog
                1150 => 300, // Patchy light drizzle
                1153 => 300, // Light drizzle
                1168 => 511, // Freezing drizzle
                1171 => 511, // Heavy freezing drizzle
                1180 => 500, // Patchy light rain
                1183 => 500, // Light rain
                1186 => 501, // Moderate rain at times
                1189 => 501, // Moderate rain
                1192 => 502, // Heavy rain at times
                1195 => 502, // Heavy rain
                1198 => 511, // Light freezing rain
                1201 => 511, // Moderate or heavy freezing rain
                1204 => 611, // Light sleet
                1207 => 611, // Moderate or heavy sleet
                1210 => 600, // Patchy light snow
                1213 => 600, // Light snow
                1216 => 601, // Patchy moderate snow
                1219 => 601, // Moderate snow
                1222 => 602, // Patchy heavy snow
                1225 => 602, // Heavy snow
                1237 => 611, // Ice pellets
                1240 => 520, // Light rain shower
                1243 => 521, // Moderate or heavy rain shower
                1246 => 522, // Torrential rain shower
                1249 => 612, // Light sleet showers
                1252 => 612, // Moderate or heavy sleet showers
                1255 => 620, // Light snow showers
                1258 => 621, // Moderate or heavy snow showers
                1261 => 611, // Light showers of ice pellets
                1264 => 611, // Moderate or heavy showers of ice pellets
                1273 => 200, // Patchy light rain with thunder
                1276 => 201, // Moderate or heavy rain with thunder
                1279 => 230, // Patchy light snow with thunder
                1282 => 231, // Moderate or heavy snow with thunder
                _ => 800     // Default to clear if unknown
            };
        }
    }
}
