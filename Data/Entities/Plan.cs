namespace WEB_Sentro.Data.Entities
{
    /// <summary>
    /// Plan catalog: Basic, Professional, Enterprise. Price, interval, optional seat limits.
    /// </summary>
    public class Plan
    {
        public int PlanId { get; set; }
        public string Code { get; set; } = null!;
        public string DisplayName { get; set; } = null!;
        public long AmountCentavos { get; set; }
        public string Currency { get; set; } = "PHP";
        public string BillingInterval { get; set; } = "month";
        public int? MaxAdminSeats { get; set; }
        public bool IsActive { get; set; } = true;
        public int SortOrder { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
