namespace WEB_Sentro.Areas.Vendor.Models;

public class BillingIndexViewModel
{
    public string TotalRevenueDisplay { get; set; } = "PHP 0";
    public string MonthlyRecurringRevenueDisplay { get; set; } = "PHP 0";
    public int ActiveSubscriptionsCount { get; set; }
    public int PendingRenewalsCount { get; set; }
    public List<BillingSubscriptionRowViewModel> Subscriptions { get; set; } = new();
}

public class BillingSubscriptionRowViewModel
{
    public int OrganizationId { get; set; }
    public string OrganizationName { get; set; } = string.Empty;
    public string PlanName { get; set; } = string.Empty;
    public int ActiveUsers { get; set; }
    public int? SeatLimit { get; set; }
    public DateTime? NextRenewalAt { get; set; }
    public string NextRenewalDisplay { get; set; } = "-";
    public string AmountDisplay { get; set; } = "PHP 0";
    public string SubscriptionStatus { get; set; } = "Unknown";
}
