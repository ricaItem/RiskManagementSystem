namespace WEB_Sentro.Data.Entities
{
    public class ChangeOrderLine
    {
        public int ChangeOrderLineId { get; set; }
        public int ChangeOrderId { get; set; }
        public string? Description { get; set; }
        public decimal Amount { get; set; }
        
        public int? CostCodeId { get; set; }
        public CostCode? CostCode { get; set; }

        public ChangeOrder ChangeOrder { get; set; } = null!;
    }
}
