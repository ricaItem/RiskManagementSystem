namespace WEB_Sentro.Areas.Vendor.Models;

public class OrganizationsIndexViewModel
{
    public string? Search { get; set; }
    public string? PlanFilter { get; set; }
    public string? StatusFilter { get; set; }
    public List<OrganizationRowViewModel> Organizations { get; set; } = new();
    public int TotalCount { get; set; }
    public string TotalRevenueDisplay { get; set; } = "₱0";
    public int ActiveTenantsCount { get; set; }
    public string SystemAvailabilityDisplay { get; set; } = "—";
}

public class OrganizationRowViewModel
{
    public int OrganizationId { get; set; }
    public string OrgCode { get; set; } = null!;
    public string OrgName { get; set; } = null!;
    public string PlanName { get; set; } = null!;
    public int AdminCount { get; set; }
    public int? RiskLoad { get; set; }
    public string Status { get; set; } = null!;
    public string StatusColor { get; set; } = "emerald";
    public string SubscriptionStatus { get; set; } = "No Subscription";
    public string NextBillingDisplay { get; set; } = "-";
}
