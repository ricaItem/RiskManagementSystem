namespace WEB_Sentro.Data.Entities
{
    public class RiskAppetiteBand
    {
        public int RiskAppetiteBandId { get; set; }
        public int RiskMatrixConfigId { get; set; }
        public int MinScore { get; set; }
        public int MaxScore { get; set; }
        public string BandName { get; set; } = null!;
        public int? ReviewFrequencyDays { get; set; }
        /// <summary>e.g. MitigationRequired, AcceptAllowed, etc.</summary>
        public string? TreatmentTrigger { get; set; }

        public RiskMatrixConfig Config { get; set; } = null!;
    }
}
