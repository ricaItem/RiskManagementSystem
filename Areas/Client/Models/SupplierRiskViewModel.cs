namespace Web_Sentro.Areas.Client.Models
{
    public class SupplierRiskViewModel
    {
        public int Id { get; set; }
        public string SupplierName { get; set; }
        public string ResourceType { get; set; } 
        public double ReliabilityScore { get; set; } 
        public string FinancialStatus { get; set; }
        public string DeliveryTrend { get; set; } 
        public decimal ContractValue { get; set; }
    }
}