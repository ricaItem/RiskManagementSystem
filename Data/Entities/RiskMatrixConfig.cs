namespace WEB_Sentro.Data.Entities
{
    /// <summary>Per-org risk matrix configuration (governance-grade).</summary>
    public class RiskMatrixConfig
    {
        public int RiskMatrixConfigId { get; set; }
        public int OrgId { get; set; }
        public string Name { get; set; } = null!;
        public bool IsActive { get; set; }
        public DateTime? EffectiveFrom { get; set; }
        public DateTime? EffectiveTo { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public ICollection<RiskMatrixCell> Cells { get; set; } = new List<RiskMatrixCell>();
        public ICollection<RiskAppetiteBand> AppetiteBands { get; set; } = new List<RiskAppetiteBand>();
        public ICollection<RiskTreatmentTrigger> TreatmentTriggers { get; set; } = new List<RiskTreatmentTrigger>();
    }
}
