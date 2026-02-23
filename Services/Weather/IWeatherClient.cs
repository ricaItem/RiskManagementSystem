namespace WEB_Sentro.Services.Weather
{
    public record WeatherSnapshot(
        double TempC,
        string Condition,
        double WindKph,
        double RainMm1h,
        DateTime ObservedAtUtc);

    public interface IWeatherClient
    {
        Task<WeatherSnapshot> GetCurrentAsync(double lat, double lon, CancellationToken ct = default);
    }
}
