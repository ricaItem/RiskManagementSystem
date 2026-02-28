using Microsoft.AspNetCore.Mvc.Rendering;

namespace WEB_Sentro.Areas.Client.Models
{
    public class RiskAnalyticsViewModel
    {
        public string? LastUpdatedHumanized { get; set; }
        public List<SelectListItem> Sites { get; set; } = new();
        public List<SelectListItem> Categories { get; set; } = new();
        public RiskAnalyticsKpisViewModel? Kpis { get; set; }
        public RiskAnalyticsMitigationViewModel? Mitigation { get; set; }
        public List<SiteRankingRowViewModel> SiteRankings { get; set; } = new();
        public List<TopRiskRowViewModel> TopRisks { get; set; } = new();
    }

    public class RiskAnalyticsKpisViewModel
    {
        public int ActiveRisks { get; set; }
        public int CriticalRisks { get; set; }
        public int CreatedInPeriod { get; set; }
        public int WeatherTriggered { get; set; }
        public int AvgTimeToCloseDays { get; set; }
        public int AvgRiskReductionPercent { get; set; }
    }

    public class RiskAnalyticsMitigationViewModel
    {
        public double AvgInitialScore { get; set; }
        public double AvgResidualScore { get; set; }
        public int AvgReductionPercent { get; set; }
        public int ReassessedPercent { get; set; }
    }
}
