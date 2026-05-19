namespace Web_Sentro.Areas.Client.Models
{
    public class RiskIdentificationViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string Category { get; set; } = "";
        public string Priority { get; set; } = "Medium";
        public string DetectedBy { get; set; } = "";
        public string ProjectSite { get; set; } = "";
        public int? ProjectId { get; set; }
        public string? ProjectName { get; set; }
        /// <summary>Backward compatibility; used in list and edit.</summary>
        public DateTime DateLogged { get; set; }
        public DateTime? DateReported { get; set; }
        public string? SourceType { get; set; }
        public string? Status { get; set; }
        public string? ReportedBy { get; set; }
        public string? ReportByUserId { get; set; }
        public int? SiteId { get; set; }
        public string? SiteName { get; set; }
        public int OrgId { get; set; }
        public DateTime? DeletedAt { get; set; }
        public int AttachmentsCount { get; set; }
        public List<string> Attachments { get; set; } = new();
        // Phase 2 governance
        public string? RiskOwnerId { get; set; }
        public string? AccountableId { get; set; }
        public string? RiskOwnerName { get; set; }
        public string? AccountableName { get; set; }
        public string? TreatmentDecision { get; set; }
        public DateTime? NextReviewDate { get; set; }
        public bool OverdueFlag { get; set; }
        public int? RiskScore { get; set; }
        public string? AppetiteBandName { get; set; }
        public int? InherentScore { get; set; }
        public string? InherentLevel { get; set; }
        public int? ResidualScore { get; set; }
        public string? ResidualLevel { get; set; }
    }
}
