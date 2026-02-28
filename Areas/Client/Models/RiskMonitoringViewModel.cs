namespace Web_Sentro.Areas.Client.Models
{
    public class RiskMonitoringViewModel
    {
        public string ProjectName { get; set; } = "";
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Temperature { get; set; }
        public string WeatherCondition { get; set; } = "";
        public double WindSpeed { get; set; }
        public int ActiveRisksCount { get; set; }
        public List<RiskIdentificationViewModel> HighPriorityRisks { get; set; } = new();

        public List<MonitoringSiteItemViewModel> Sites { get; set; } = new();
        public int SelectedSiteId { get; set; }
        public List<MonitoringAlertItemViewModel> SystemAlerts { get; set; } = new();
        public DateTime? LastSyncUtc { get; set; }
        public bool ApiHealthOk { get; set; }
    }

    public class MonitoringSiteItemViewModel
    {
        public int SiteId { get; set; }
        public string Name { get; set; } = "";
        public string? SiteName { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }

    public class MonitoringAlertItemViewModel
    {
        public int AlertId { get; set; }
        public string RuleName { get; set; } = "";
        public string? MeasuredValues { get; set; }
        public string Severity { get; set; } = "";
        public DateTime TriggeredAt { get; set; }
        public int? RiskId { get; set; }
    }
}
