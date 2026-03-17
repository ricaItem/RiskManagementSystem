namespace WEB_Sentro.Services;

public class OrganizationAnalyticsSnapshotHostedService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly IConfiguration _config;
    private readonly ILogger<OrganizationAnalyticsSnapshotHostedService> _logger;

    public OrganizationAnalyticsSnapshotHostedService(
        IServiceProvider services,
        IConfiguration config,
        ILogger<OrganizationAnalyticsSnapshotHostedService> logger)
    {
        _services = services;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalMinutes = _config.GetValue("VendorAnalytics:SnapshotIntervalMinutes", 15);
        if (intervalMinutes <= 0)
        {
            _logger.LogInformation("Vendor analytics snapshot hosted service disabled.");
            return;
        }

        _logger.LogInformation("Vendor analytics snapshot hosted service started. Interval: {Minutes} minutes.", intervalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _services.CreateScope();
                var refresh = scope.ServiceProvider.GetRequiredService<OrganizationAnalyticsSnapshotRefreshService>();
                var maxParallelTenants = _config.GetValue("VendorAnalytics:MaxParallelTenants", 4);
                await refresh.RefreshAsync(maxParallelTenants, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Vendor analytics snapshot refresh cycle failed.");
            }

            await Task.Delay(TimeSpan.FromMinutes(intervalMinutes), stoppingToken);
        }
    }
}
