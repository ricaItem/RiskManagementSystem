namespace WEB_Sentro.Areas.Client.Models
{
    public class TopRiskRowViewModel
    {
        public int RiskId { get; set; }
        public string RiskName { get; set; } = "";
        public string RiskCode { get; set; } = "";
        public string SiteName { get; set; } = "";
        public string? Category { get; set; }
        public string? Source { get; set; }
        public string? Severity { get; set; }
        public double? CurrentScore { get; set; }
        public string? Status { get; set; }
        public string? Owner { get; set; }
        public DateTime? CreatedDate { get; set; }
        public int? DaysOpen { get; set; }
    }
}
