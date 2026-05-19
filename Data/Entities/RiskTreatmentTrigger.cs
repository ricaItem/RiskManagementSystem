namespace WEB_Sentro.Data.Entities
{
    /// <summary>Rules for which treatment decisions are allowed and whether justification/approval is required.</summary>
    public class RiskTreatmentTrigger
    {
        public int RiskTreatmentTriggerId { get; set; }
        public int RiskMatrixConfigId { get; set; }
        /// <summary>Band name (e.g. High, Critical) or null if by score range.</summary>
        public string? BandName { get; set; }
        public int? MinScore { get; set; }
        public int? MaxScore { get; set; }
        /// <summary>Comma-separated: Mitigate,Transfer,Accept,Avoid</summary>
        public string? AllowedDecisions { get; set; }
        public bool RequiresJustification { get; set; }
        public bool RequiresApproval { get; set; }

        public RiskMatrixConfig Config { get; set; } = null!;
    }
}
