namespace Web_Sentro.Areas.Client.Models
{
    public class RiskAssessmentViewModel
    {
        public int RiskId { get; set; }
        public string RiskTitle { get; set; }
        public int Likelihood { get; set; }
        public int Impact { get; set; }
        public bool IsInherent { get; set; }
        /// <summary>Mitigate | Transfer | Accept | Avoid</summary>
        public string? TreatmentDecision { get; set; }
        /// <summary>Required when Accept/Transfer and config requires justification.</summary>
        public string? TreatmentJustification { get; set; }

        public int RiskScore => Likelihood * Impact;

        public string RiskLevel => RiskScore switch
        {
            >= 20 => "Critical",
            >= 15 => "High",
            >= 7 => "Medium",
            _ => "Low"
        };

        public string? RejectRemarks { get; set; }
    }
}