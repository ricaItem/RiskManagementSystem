namespace WEB_Sentro.Areas.Vendor.Models;

public class AnalyticsIndexViewModel
{
    public string RangeLabel { get; set; } = "Last 30 Days";
    public string RangeKey { get; set; } = "30d";
    public string SearchQuery { get; set; } = string.Empty;
    public string RiskFilter { get; set; } = "all";
    public string SortBy { get; set; } = "events_desc";
    public int CurrentPage { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public int TotalCount { get; set; }
    public int TotalPages { get; set; } = 1;
    public int ActiveOrganizations { get; set; }
    public int TotalAuditEventsInRange { get; set; }
    public decimal ActivityChangePercent { get; set; }
    public decimal UserEngagementChangePercent { get; set; }
    public decimal AdoptionChangePercent { get; set; }
    public int AtRiskOrganizationsCount { get; set; }
    public int GrowingOrganizationsCount { get; set; }
    public int InactiveOrganizationsCount { get; set; }
    public List<OrganizationUsageRowViewModel> TopOrganizations { get; set; } = new();
}

public class OrganizationUsageRowViewModel
{
    public int OrganizationId { get; set; }
    public string OrganizationName { get; set; } = string.Empty;
    public string PlanName { get; set; } = "-";
    public string SubscriptionStatus { get; set; } = "Unknown";
    public int UserCount { get; set; }
    public int? SeatLimit { get; set; }
    public string SeatUtilizationLabel { get; set; } = "-";
    public int LoginsInRange { get; set; }
    public int EventCountInRange { get; set; }
    public decimal ActivityChangePercent { get; set; }
    public decimal UserChangePercent { get; set; }
    public int FeatureAdoptionPercent { get; set; }
    public decimal AdoptionChangePercent { get; set; }
    public string LastActivityDisplay { get; set; } = "No activity";
    public decimal ErrorRatePercent { get; set; }
    public string RenewalDateDisplay { get; set; } = "-";
    public int HealthScore { get; set; }
    public string ChurnRiskLabel { get; set; } = "Medium";
    public string SegmentLabel { get; set; } = "Stable";
    public List<int> ActivityTrendPoints { get; set; } = new();
    public string TrendLabel { get; set; } = "Stable";
}
