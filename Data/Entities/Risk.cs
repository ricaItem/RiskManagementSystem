namespace WEB_Sentro.Data.Entities
{
    public class Risk
    {
        public int RiskId { get; set; }
        public int OrgId { get; set; }
        public int? ProjectId { get; set; }
        public string ReportByUserId { get; set; } = null!;
        public int? LocationId { get; set; }
        public int? SiteId { get; set; }
        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        public string? Category { get; set; }
        public string? SourceType { get; set; }
        public string? MonitoringRuleCode { get; set; }
        public string Status { get; set; } = "Draft";
        public string? Priority { get; set; }
        public string? ProjectSite { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? DeletedAt { get; set; }

        public Site? Site { get; set; }
        public ICollection<RiskEvaluation> Evaluations { get; set; } = new List<RiskEvaluation>();
        public ICollection<Attachment> Attachments { get; set; } = new List<Attachment>();
        public MitigationPlan? MitigationPlan { get; set; }
    }
}
