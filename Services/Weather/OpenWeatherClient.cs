using System.Text.Json;
using Microsoft.Extensions.Options;

namespace WEB_Sentro.Services.Weather
{
    public class OpenWeatherClient : IWeatherClient
    {
        private readonly HttpClient _http;
        private readonly string _apiKey;
        private const string BaseUrl = "https://api.openweathermap.org/data/2.5";

        public OpenWeatherClient(HttpClient http, IOptions<OpenWeatherOptions> options)
        {
            _http = http;
            _apiKey = options?.Value?.ApiKey ?? "";
        }

        public async Task<WeatherSnapshot> GetCurrentAsync(double lat, double lon, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
                return new WeatherSnapshot(0, "No API key", 0, 0, DateTime.UtcNow);

            var url = $"{BaseUrl}/weather?lat={lat}&lon={lon}&appid={Uri.EscapeDataString(_apiKey)}&units=metric";
            var response = await _http.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(ct);
            return ParseResponse(json);
        }

        private static WeatherSnapshot ParseResponse(string json)
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var temp = root.TryGetProperty("main", out var main) && main.TryGetProperty("temp", out var t)
                ? t.GetDouble()
                : 0;
            var windMs = root.TryGetProperty("wind", out var wind) && wind.TryGetProperty("speed", out var ws)
                ? ws.GetDouble()
                : 0;
            var windKph = windMs * 3.6;
            var condition = "Clear";
            if (root.TryGetProperty("weather", out var weather) && weather.GetArrayLength() > 0)
            {
                var w0 = weather[0];
                if (w0.TryGetProperty("description", out var desc))
                    condition = desc.GetString() ?? "Clear";
            }
            var rain1h = 0.0;
            if (root.TryGetProperty("rain", out var rain) && rain.TryGetProperty("1h", out var r1h))
                rain1h = r1h.GetDouble();
            var observed = DateTime.UtcNow;
            if (root.TryGetProperty("dt", out var dt))
                observed = DateTimeOffset.FromUnixTimeSeconds(dt.GetInt64()).UtcDateTime;
            return new WeatherSnapshot(temp, condition, windKph, rain1h, observed);
        }
    }

    public class OpenWeatherOptions
    {
        public const string SectionName = "Apis:OpenWeather";
        public string ApiKey { get; set; } = "";
    }
}
