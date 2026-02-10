namespace Web_Sentro.Areas.Client.Models
{
    public class RiskMonitoringViewModel
    {
        // Project Context
        public string ProjectName { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }

        // Automated Weather Data (Mocked for UI)
        public double Temperature { get; set; }
        public string WeatherCondition { get; set; }
        public double WindSpeed { get; set; }

        // Risk Aggregates
        public int ActiveRisksCount { get; set; }
        public List<RiskIdentificationViewModel> HighPriorityRisks { get; set; }
    }
}