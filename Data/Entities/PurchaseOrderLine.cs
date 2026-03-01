using System.ComponentModel.DataAnnotations.Schema;

namespace WEB_Sentro.Data.Entities
{
    public class PurchaseOrderLine
    {
        public int PurchaseOrderLineId { get; set; }
        public int PurchaseOrderId { get; set; }
        public string? Description { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitCost { get; set; }
        [NotMapped]
        public decimal Total => Quantity * UnitCost;

        public PurchaseOrder PurchaseOrder { get; set; } = null!;
    }
}
