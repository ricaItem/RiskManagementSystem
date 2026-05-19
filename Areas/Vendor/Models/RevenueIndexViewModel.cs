namespace WEB_Sentro.Areas.Vendor.Models;

public class RevenueIndexViewModel
{
    public string SelectedRange { get; set; } = "12m";
    public string GrossCollectedDisplay { get; set; } = "PHP 0";
    public string MrrDisplay { get; set; } = "PHP 0";
    public string ArrDisplay { get; set; } = "PHP 0";
    public string OutstandingArDisplay { get; set; } = "PHP 0";
    public string AtRiskMrrDisplay { get; set; } = "PHP 0";
    public string ChurnedMrrDisplay { get; set; } = "PHP 0";
    public string ExpansionMrrDisplay { get; set; } = "PHP 0";
    public int ActiveSubscriptionsCount { get; set; }
    public int RenewalRiskCount { get; set; }
    public List<RevenueTrendPointViewModel> Trend { get; set; } = new();
    public List<RevenuePlanMixRowViewModel> PlanMix { get; set; } = new();
    public List<RevenueTopOrganizationRowViewModel> TopOrganizations { get; set; } = new();
    public List<RevenueAgingRowViewModel> Aging { get; set; } = new();
}

public class RevenueTrendPointViewModel
{
    public string Label { get; set; } = string.Empty;
    public long CollectedCentavos { get; set; }
    public long MrrCentavos { get; set; }
}

public class RevenuePlanMixRowViewModel
{
    public string PlanName { get; set; } = string.Empty;
    public int SubscriptionCount { get; set; }
    public long MrrCentavos { get; set; }
}

public class RevenueTopOrganizationRowViewModel
{
    public int OrganizationId { get; set; }
    public string OrganizationName { get; set; } = string.Empty;
    public long CollectedCentavos { get; set; }
}

public class RevenueAgingRowViewModel
{
    public string Bucket { get; set; } = string.Empty;
    public int InvoiceCount { get; set; }
    public long AmountCentavos { get; set; }
}
