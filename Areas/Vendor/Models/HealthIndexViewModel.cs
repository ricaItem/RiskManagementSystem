namespace WEB_Sentro.Areas.Vendor.Models;

public class HealthIndexViewModel
{
    public string OverallStatus { get; set; } = "Healthy";
    public string OverallStatusClass { get; set; } = "text-emerald-600";
    public DateTime CheckedAtUtc { get; set; }
    public string AppUptimeDisplay { get; set; } = "-";
    public List<HealthCheckRowViewModel> Checks { get; set; } = new();
}

public class HealthCheckRowViewModel
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Value { get; set; } = "-";
    public string Status { get; set; } = "Unknown";
    public string StatusColorClass { get; set; } = "text-slate-500";
}
