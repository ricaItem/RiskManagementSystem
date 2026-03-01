namespace Web_Sentro.Areas.Client.Models
{
    public class MitigationRiskCardViewModel
    {
        public int RiskId { get; set; }
        public int PlanId { get; set; }
        public string Title { get; set; } = "";
        public string? Category { get; set; }
        public string? Priority { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsArchived { get; set; }
        /// <summary>Overall plan progress 0-100 (tasks done / total tasks).</summary>
        public int ProgressPercent { get; set; }
        /// <summary>Display names of users assigned to tasks in this plan (for avatar group).</summary>
        public List<string> AssignedToDisplayNames { get; set; } = new();
    }
}
