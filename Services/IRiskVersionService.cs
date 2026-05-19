namespace WEB_Sentro.Services
{
    public interface IRiskVersionService
    {
        /// <summary>Creates a RiskVersion snapshot for the risk. Call after any risk update.</summary>
        Task SaveVersionAsync(int riskId, int orgId, string? changedByUserId, string changeSummary, CancellationToken ct = default);
        Task<IReadOnlyList<RiskVersionDto>> GetVersionsAsync(int riskId, int orgId, CancellationToken ct = default);
    }

    public class RiskVersionDto
    {
        public int RiskVersionId { get; set; }
        public int RiskId { get; set; }
        public int VersionNo { get; set; }
        public DateTime ChangedAt { get; set; }
        public string? ChangedByUserId { get; set; }
        public string? ChangedByDisplayName { get; set; }
        public string? ChangeSummary { get; set; }
        public string? SnapshotJson { get; set; }
    }
}
