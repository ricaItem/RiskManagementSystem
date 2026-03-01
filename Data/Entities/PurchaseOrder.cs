namespace WEB_Sentro.Data.Entities
{
    public class PurchaseOrder
    {
        public int PurchaseOrderId { get; set; }
        public int OrgId { get; set; }
        public int SiteId { get; set; }
        public int SupplierId { get; set; }
        public string OrderNumber { get; set; } = null!;
        public DateTime OrderDate { get; set; }
        /// <summary>e.g. Draft, Sent, Received, Cancelled</summary>
        public string Status { get; set; } = "Draft";
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public Site Site { get; set; } = null!;
        public Supplier Supplier { get; set; } = null!;
        public ICollection<PurchaseOrderLine> LineItems { get; set; } = new List<PurchaseOrderLine>();
        public ICollection<Expense> Expenses { get; set; } = new List<Expense>();
    }
}
