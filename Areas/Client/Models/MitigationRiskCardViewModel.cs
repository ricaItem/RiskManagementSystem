namespace Web_Sentro.Areas.Client.Models
{
    public class MitigationRiskCardViewModel
    {
        public int RiskId { get; set; }
        public string Title { get; set; } = "";
        public string? Category { get; set; }
        public string? Priority { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
