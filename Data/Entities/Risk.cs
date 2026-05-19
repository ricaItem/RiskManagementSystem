namespace WEB_Sentro.Data.Entities
{
    public class Risk
    {
        public int RiskId { get; set; }
        public int OrgId { get; set; }
        public int? ProjectId { get; set; }
        public string ReportByUserId { get; set; } = null!;
        /// <summary>RACI R - person responsible for day-to-day management.</summary>
        public string? RiskOwnerId { get; set; }
        /// <summary>RACI A - person accountable for the risk.</summary>
        public string? AccountableId { get; set; }
        public int? LocationId { get; set; }
        public int? SiteId { get; set; }
        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        public string? Category { get; set; }
        public string? SourceType { get; set; }
        public int? SupplierId { get; set; }
        public string? MonitoringRuleCode { get; set; }
        public string Status { get; set; } = "Draft";
        public string? Priority { get; set; }
        public string? ProjectSite { get; set; }
        /// <summary>ISO/ERM: Mitigate | Transfer | Accept | Avoid</summary>
        public string? TreatmentDecision { get; set; }
        /// <summary>Required for Accept/Transfer when so configured.</summary>
        public string? TreatmentJustification { get; set; }
        public DateTime? TreatmentSelectedAt { get; set; }
        public string? TreatmentSelectedByUserId { get; set; }
        /// <summary>Required for governance; next scheduled review.</summary>
        public DateTime? NextReviewDate { get; set; }
        public DateTime? LastReviewedAt { get; set; }
        /// <summary>True when NextReviewDate is in the past.</summary>
        public bool OverdueFlag { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? DeletedAt { get; set; }

        public Site? Site { get; set; }
        public Project? Project { get; set; }
        public Supplier? Supplier { get; set; }
        public ICollection<RiskEvaluation> Evaluations { get; set; } = new List<RiskEvaluation>();
        public ICollection<Attachment> Attachments { get; set; } = new List<Attachment>();
        public MitigationPlan? MitigationPlan { get; set; }
        public ICollection<Expense> Expenses { get; set; } = new List<Expense>();
        public ICollection<RiskVersion> Versions { get; set; } = new List<RiskVersion>();
        public ICollection<RiskControl> RiskControls { get; set; } = new List<RiskControl>();
    }
}
