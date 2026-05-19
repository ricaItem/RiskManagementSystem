namespace WEB_Sentro.Data.Entities
{
    /// <summary>
    /// One row per payment transaction: PayMongo (or other gateway), amount, status.
    /// </summary>
    public class Payment
    {
        public int PaymentId { get; set; }
        public int OrganizationId { get; set; }
        public int? InvoiceId { get; set; }
        public string Gateway { get; set; } = "PayMongo";
        public string GatewayPaymentIntentId { get; set; } = null!;
        public string? GatewayStatus { get; set; }
        public long AmountCentavos { get; set; }
        public string Currency { get; set; } = "PHP";
        public string? PaymentMethod { get; set; }
        public string Status { get; set; } = "Pending";
        public DateTime? PaidAt { get; set; }
        public string? MetadataJson { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public string? CreatedByUserId { get; set; }

        public Organization Organization { get; set; } = null!;
        public Invoice? Invoice { get; set; }
    }
}
