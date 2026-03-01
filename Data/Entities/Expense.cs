namespace WEB_Sentro.Data.Entities
{
    public class Expense
    {
        public int ExpenseId { get; set; }
        public int OrgId { get; set; }
        public int SiteId { get; set; }
        public decimal Amount { get; set; }
        /// <summary>e.g. Labor, Materials, Equipment, Mitigation</summary>
        public string? Category { get; set; }
        public DateTime Date { get; set; }
        public int? RiskId { get; set; }
        public int? PurchaseOrderId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public Site Site { get; set; } = null!;
        public Risk? Risk { get; set; }
        public PurchaseOrder? PurchaseOrder { get; set; }
    }
}
