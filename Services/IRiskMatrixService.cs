namespace WEB_Sentro.Services
{
    public interface IRiskMatrixService
    {
        /// <summary>Gets the active matrix config for the org (cached).</summary>
        Task<RiskMatrixConfigDto?> GetActiveConfigAsync(int orgId, CancellationToken ct = default);
        /// <summary>Computes risk score from likelihood and impact using active config, or default L×I if no config.</summary>
        Task<int> ComputeScoreAsync(int orgId, int likelihood, int impact, CancellationToken ct = default);
        /// <summary>Gets appetite band name for the given score.</summary>
        Task<string?> GetBandForScoreAsync(int orgId, int score, CancellationToken ct = default);
        /// <summary>Gets default review frequency in days for the band (from appetite band).</summary>
        Task<int?> GetReviewFrequencyDaysAsync(int orgId, int score, CancellationToken ct = default);
        /// <summary>Allowed treatment decisions for this score/band (e.g. "Mitigate,Transfer,Accept,Avoid").</summary>
        Task<IReadOnlyList<string>> GetAllowedDecisionsAsync(int orgId, int score, CancellationToken ct = default);
        /// <summary>Whether the given decision requires justification (e.g. Accept/Transfer in high band).</summary>
        Task<bool> RequiresJustificationAsync(int orgId, int score, string decision, CancellationToken ct = default);
        /// <summary>Invalidate cache for org after config update.</summary>
        void InvalidateCache(int orgId);
        /// <summary>Ensures a default 5×5 matrix and bands exist for the org; no-op if already present.</summary>
        Task EnsureDefaultMatrixAsync(int orgId, CancellationToken ct = default);
    }

    public class RiskMatrixConfigDto
    {
        public int RiskMatrixConfigId { get; set; }
        public int OrgId { get; set; }
        public string Name { get; set; } = "";
        public IReadOnlyList<RiskMatrixCellDto> Cells { get; set; } = Array.Empty<RiskMatrixCellDto>();
        public IReadOnlyList<RiskAppetiteBandDto> AppetiteBands { get; set; } = Array.Empty<RiskAppetiteBandDto>();
        public IReadOnlyList<RiskTreatmentTriggerDto> TreatmentTriggers { get; set; } = Array.Empty<RiskTreatmentTriggerDto>();
    }

    public class RiskMatrixCellDto
    {
        public int Likelihood { get; set; }
        public int Impact { get; set; }
        public int Score { get; set; }
    }

    public class RiskAppetiteBandDto
    {
        public int MinScore { get; set; }
        public int MaxScore { get; set; }
        public string BandName { get; set; } = "";
        public int? ReviewFrequencyDays { get; set; }
    }

    public class RiskTreatmentTriggerDto
    {
        public string? BandName { get; set; }
        public int? MinScore { get; set; }
        public int? MaxScore { get; set; }
        public IReadOnlyList<string> AllowedDecisions { get; set; } = Array.Empty<string>();
        public bool RequiresJustification { get; set; }
        public bool RequiresApproval { get; set; }
    }
}
