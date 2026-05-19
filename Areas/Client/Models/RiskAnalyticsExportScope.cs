namespace WEB_Sentro.Areas.Client.Models;

/// <summary>
/// Scope description for the analytics report (used in PDF header).
/// </summary>
public class RiskAnalyticsExportScope
{
    public string PeriodLabel { get; set; } = "Last 30 days";
    public string SiteLabel { get; set; } = "All sites";
    public string CategoryLabel { get; set; } = "All categories";
}
