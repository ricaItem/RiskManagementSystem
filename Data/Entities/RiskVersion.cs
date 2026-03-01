namespace WEB_Sentro.Data.Entities
{
    /// <summary>Version history snapshot for governance; one row per update.</summary>
    public class RiskVersion
    {
        public int RiskVersionId { get; set; }
        public int RiskId { get; set; }
        public int VersionNo { get; set; }
        public DateTime ChangedAt { get; set; }
        public string? ChangedByUserId { get; set; }
        /// <summary>JSON snapshot of risk + latest evaluation at change time.</summary>
        public string? SnapshotJson { get; set; }
        public string? ChangeSummary { get; set; }

        public Risk Risk { get; set; } = null!;
    }
}
