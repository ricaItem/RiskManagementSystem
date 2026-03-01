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
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public ICollection<PurchaseOrder> PurchaseOrders { get; set; } = new List<PurchaseOrder>();
    }
}
