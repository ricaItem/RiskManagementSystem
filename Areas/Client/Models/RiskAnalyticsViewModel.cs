using Microsoft.AspNetCore.Mvc.Rendering;

namespace WEB_Sentro.Areas.Client.Models
{
    public class RiskAnalyticsViewModel
    {
        public string? LastUpdatedHumanized { get; set; }
        public List<SelectListItem> Sites { get; set; } = new();
        public List<SelectListItem> Categories { get; set; } = new();
        public RiskAnalyticsKpisViewModel? Kpis { get; set; }
        public RiskAnalyticsChartsViewModel? Charts { get; set; }
        public RiskAnalyticsMitigationViewModel? Mitigation { get; set; }
        public PredictiveInsightsViewModel? PredictiveInsights { get; set; }
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
        public List<KpiCardViewModel> KpiCards { get; set; } = new();
    }

    public class KpiCardViewModel
    {
        public string Label { get; set; } = "";
        public int Value { get; set; }
        public string DeltaText { get; set; } = "";
        public bool DeltaUp { get; set; }
    }

    public class RiskAnalyticsChartsViewModel
    {
        public List<string> RisksOverTimeLabels { get; set; } = new();
        public List<int> RisksOverTimeValues { get; set; } = new();
        public List<string> RisksByCategoryLabels { get; set; } = new();
        public List<int> RisksByCategoryValues { get; set; } = new();
        public int OpenCount { get; set; }
        public int ClosedCount { get; set; }
    }

    public class RiskAnalyticsMitigationViewModel
    {
        public double AvgInitialScore { get; set; }
        public double AvgResidualScore { get; set; }
        public int AvgReductionPercent { get; set; }
        public int ReassessedPercent { get; set; }
    }

    public class PredictiveInsightsViewModel
    {
        public int EscalationHigh { get; set; }
        public int EscalationMedium { get; set; }
        public int EscalationLow { get; set; }
        public string EscalationHint { get; set; } = "Top candidates will appear here";
        public int? FloodProbabilityPercent { get; set; }
        public string MomentumStatus { get; set; } = "Stable";
        public decimal? CostForecastAmount { get; set; }
        public List<ClosureBySeverityRowViewModel> ClosureBySeverity { get; set; } = new();
        public List<EarlyWarningRowViewModel> EarlyWarnings { get; set; } = new();

        // New properties for enhancements
        public double ExpectedIncidentsNextWeek { get; set; }
        public double WeekOverWeekTrendPercent { get; set; }
        public double DynamicRiskScore { get; set; }
        public string RiskLevel { get; set; } = "Low"; // Low, Medium, High
        public string RiskLevelExplanation { get; set; } = "";
        public string TrendDirection { get; set; } = "Stable"; // Up, Down, Stable
    }

    public class ClosureBySeverityRowViewModel
    {
        public string Severity { get; set; } = "";
        public int? AvgCloseDays { get; set; }
        public string? EtaWindow { get; set; }
    }

    public class EarlyWarningRowViewModel
    {
        public string Title { get; set; } = "";
        public string? StatusPill { get; set; }
        public bool IsWarning { get; set; }
    }
}
