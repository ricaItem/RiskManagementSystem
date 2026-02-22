namespace Web_Sentro.Areas.Client.Models
{
    public class RiskMonitoringViewModel
    {
        public string ProjectName { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }

        public double Temperature { get; set; }
        public string WeatherCondition { get; set; }
        public double WindSpeed { get; set; }

        public int ActiveRisksCount { get; set; }
        public List<RiskIdentificationViewModel> HighPriorityRisks { get; set; }
    }
}