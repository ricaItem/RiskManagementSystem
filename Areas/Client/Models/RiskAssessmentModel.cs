namespace Web_Sentro.Areas.Client.Models
{
    public class RiskAssessmentViewModel
    {
        public int RiskId { get; set; }
        public string RiskTitle { get; set; }
        public int Likelihood { get; set; } // 1 to 5
        public int Impact { get; set; }     // 1 to 5

        // Automated Property
        public int RiskScore => Likelihood * Impact;

        // Automated Logic: Determines the level based on score
        public string RiskLevel => RiskScore switch
        {
            >= 15 => "Critical",
            >= 10 => "High",
            >= 5 => "Medium",
            _ => "Low"
        };
    }
}