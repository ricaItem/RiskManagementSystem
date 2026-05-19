namespace WEB_Sentro.Data.Entities
{
    /// <summary>
    /// Per-organization current plan and billing period. One active subscription per org.
    /// </summary>
    public class Subscription
    {
        public int SubscriptionId { get; set; }
        public int OrganizationId { get; set; }
        public int PlanId { get; set; }
        public int? PendingPlanId { get; set; }
        public string? PendingChangeType { get; set; }
        public DateTime? PendingChangeEffectiveAt { get; set; }
        public string Status { get; set; } = "Active";
        public DateTime CurrentPeriodStart { get; set; }
        public DateTime CurrentPeriodEnd { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? CanceledAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public Organization Organization { get; set; } = null!;
        public Plan Plan { get; set; } = null!;
        public Plan? PendingPlan { get; set; }
    }
}
