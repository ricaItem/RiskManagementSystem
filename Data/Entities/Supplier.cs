namespace WEB_Sentro.Data.Entities
{
    public class Supplier
    {
        public int SupplierId { get; set; }
        public int OrgId { get; set; }
        public string Name { get; set; } = null!;
        public string? ContactPerson { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        /// <summary>e.g. Materials, Equipment, Services</summary>
        public string? Category { get; set; }
        /// <summary>0-100, default 80</summary>
        public int ReliabilityScore { get; set; } = 80;
        /// <summary>Stable | Warning | Critical</summary>
        public string FinancialStatus { get; set; } = "Stable";
        /// <summary>OnTime | Delayed | Critical</summary>
        public string DeliveryTrend { get; set; } = "OnTime";
        public decimal ContractValue { get; set; }
        public DateTime? RiskProfileUpdatedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public ICollection<PurchaseOrder> PurchaseOrders { get; set; } = new List<PurchaseOrder>();
    }
}
