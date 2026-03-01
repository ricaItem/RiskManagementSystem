namespace WEB_Sentro.Data.Entities
{
    public class Site
    {
        public int SiteId { get; set; }
        public int OrgId { get; set; }
        public string SiteCode { get; set; } = null!;
        public string SiteName { get; set; } = null!;
        public string Status { get; set; } = "Active";
        public string? AddressLine { get; set; }
        public string? City { get; set; }
        public string? Province { get; set; }
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public string? ProjectManagerUserId { get; set; }
        public decimal? BudgetAllocated { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public ICollection<Risk> Risks { get; set; } = new List<Risk>();
        public ICollection<MonitoringSite> MonitoringSites { get; set; } = new List<MonitoringSite>();
        public ICollection<PurchaseOrder> PurchaseOrders { get; set; } = new List<PurchaseOrder>();
        public ICollection<Expense> Expenses { get; set; } = new List<Expense>();
    }
}
