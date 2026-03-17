namespace WEB_Sentro.Areas.Vendor.Models;

public class VendorDashboardViewModel
{
    public int OrganizationCount { get; set; }
    public int ActiveRisksCount { get; set; }
    public int OpenIncidentsCount { get; set; }
    public decimal PlatformHealthPercent { get; set; }
    public int CompliancePercent { get; set; }
    public decimal ComplianceTargetPercent { get; set; } = 98m;
    public DateTime? LastSnapshotAtUtc { get; set; }
    public string LastUpdatedDisplay { get; set; } = "No snapshots yet";
    public List<int> RiskVelocityPoints { get; set; } = new() { 0, 0, 0, 0, 0, 0 };
    public int RiskVelocityTotalEvents { get; set; }
    public List<VendorSeverityRowViewModel> RiskSeverity { get; set; } = new();
    public List<VendorLiveFeedItemViewModel> LiveFeed { get; set; } = new();
    public string DataFreshnessLabel { get; set; } = "No data";
    public string JobsStatusLabel { get; set; } = "Idle";
}

public class VendorSeverityRowViewModel
{
    public string Label { get; set; } = string.Empty;
    public int Count { get; set; }
    public int Percent { get; set; }
    public string ColorClass { get; set; } = "bg-slate-400";
}

public class VendorLiveFeedItemViewModel
{
    public string Title { get; set; } = string.Empty;
    public string Meta { get; set; } = string.Empty;
    public string DotClass { get; set; } = "bg-slate-400";
    public DateTime OccurredAtUtc { get; set; }
}
