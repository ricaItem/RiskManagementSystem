using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WEB_Sentro.Services
{
    public class OpenWeatherDto
    {
        [JsonPropertyName("main")]
        public MainDto? Main { get; set; }
        [JsonPropertyName("wind")]
        public WindDto? Wind { get; set; }
        [JsonPropertyName("weather")]
        public WeatherItemDto[]? Weather { get; set; }
    }

    public class MainDto
    {
        [JsonPropertyName("temp")]
        public double Temp { get; set; }
        [JsonPropertyName("humidity")]
        public int Humidity { get; set; }
    }

    public class WindDto
    {
        [JsonPropertyName("speed")]
        public double Speed { get; set; }
    }

    public class WeatherItemDto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }
        [JsonPropertyName("main")]
        public string? Main { get; set; }
    }

    public class WeatherSnapshot
    {
        public double TempC { get; set; }
        public double WindSpeedKmh { get; set; }
        public int Humidity { get; set; }
        public int WeatherId { get; set; }
        public string? Condition { get; set; }
        public DateTime FetchedAt { get; set; }
        public bool ApiOk { get; set; }
        /// <summary>Precipitation in mm. For WeatherAPI: current.precip_mm (amount in mm; interpretation varies—often recent/last period).</summary>
        public double Rain_1h_mm { get; set; }
        /// <summary>When set, rule engine uses this instead of computing from TempC+Humidity. WeatherAPI provides current.heatindex_c when applicable.</summary>
        public double? HeatIndexC { get; set; }
    }

    public interface IOpenWeatherService
    {
        Task<WeatherSnapshot> GetWeatherAsync(double lat, double lon, CancellationToken ct = default);
    }

    public class OpenWeatherService : IOpenWeatherService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _config;
        private static readonly ConcurrentDictionary<string, (WeatherSnapshot s, DateTime until)> Cache = new();
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);

        public OpenWeatherService(IHttpClientFactory httpClientFactory, IConfiguration config)
        {
            _httpClientFactory = httpClientFactory;
            _config = config;
        }

        public async Task<WeatherSnapshot> GetWeatherAsync(double lat, double lon, CancellationToken ct = default)
        {
            var key = $"{lat:F4}_{lon:F4}";
            if (Cache.TryGetValue(key, out var entry) && DateTime.UtcNow < entry.until)
                return entry.s;

            var apiKey = _config["OpenWeather:ApiKey"] ?? "";
            var baseUrl = "https://api.openweathermap.org/data/2.5/weather";
            var url = $"{baseUrl}?lat={lat}&lon={lon}&units=metric&appid={apiKey}";

            var client = _httpClientFactory.CreateClient();
            var snapshot = new WeatherSnapshot { FetchedAt = DateTime.UtcNow, ApiOk = false };

            if (string.IsNullOrEmpty(apiKey))
            {
                Cache.TryAdd(key, (snapshot, DateTime.UtcNow + CacheTtl));
                return snapshot;
            }

            try
            {
                var response = await client.GetAsync(url, ct);
                snapshot.ApiOk = response.IsSuccessStatusCode;
                if (!response.IsSuccessStatusCode)
                {
                    Cache.TryAdd(key, (snapshot, DateTime.UtcNow + CacheTtl));
                    return snapshot;
                }

                var json = await response.Content.ReadAsStringAsync(ct);
                var dto = JsonSerializer.Deserialize<OpenWeatherDto>(json);
                if (dto?.Main != null)
                {
                    snapshot.TempC = dto.Main.Temp;
                    snapshot.Humidity = dto.Main.Humidity;
                }
                if (dto?.Wind != null)
                    snapshot.WindSpeedKmh = dto.Wind.Speed * 3.6;
                if (dto?.Weather?.Length > 0)
                {
                    snapshot.WeatherId = dto.Weather[0].Id;
                    snapshot.Condition = dto.Weather[0].Main;
                }
                Cache[key] = (snapshot, DateTime.UtcNow + CacheTtl);
                return snapshot;
            }
            catch
            {
                Cache.TryAdd(key, (snapshot, DateTime.UtcNow + CacheTtl));
                return snapshot;
            }
        }
    }
}
