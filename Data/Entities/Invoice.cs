namespace WEB_Sentro.Data.Entities
{
    /// <summary>
    /// Billing document per period or ad-hoc. Payments (PayMongo) attach to an invoice.
    /// </summary>
    public class Invoice
    {
        public int InvoiceId { get; set; }
        public int OrganizationId { get; set; }
        public int? SubscriptionId { get; set; }
        public string InvoiceNumber { get; set; } = null!;
        public string Status { get; set; } = "Draft";
        public long AmountDueCentavos { get; set; }
        public string Currency { get; set; } = "PHP";
        public DateTime? PeriodStart { get; set; }
        public DateTime? PeriodEnd { get; set; }
        public DateTime DueDate { get; set; }
        public DateTime? PaidAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public string? CreatedByUserId { get; set; }

        public Organization Organization { get; set; } = null!;
        public Subscription? Subscription { get; set; }
    }
}
