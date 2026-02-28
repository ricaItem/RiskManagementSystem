namespace WEB_Sentro.Areas.Client.Models
{
    public class SiteRankingRowViewModel
    {
        public int SiteId { get; set; }
        public string SiteName { get; set; } = "";
        public int ActiveRisks { get; set; }
        public int CriticalCount { get; set; }
        public double AvgScore { get; set; }
        public bool TrendUp { get; set; }
        public int AvgCloseTimeDays { get; set; }
        public int OnTimeMitigationPercent { get; set; }
        public decimal? TotalRiskCost { get; set; }
    }
}
