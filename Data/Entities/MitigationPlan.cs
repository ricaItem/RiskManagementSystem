namespace WEB_Sentro.Data.Entities
{
    public class MitigationPlan
    {
        public int PlanId { get; set; }
        public int RiskId { get; set; }
        public string? CreatedByUserId { get; set; }
        public string? StrategyType { get; set; }
        public string? Summary { get; set; }
        public DateTime? TargetCloseDate { get; set; }
        public string Status { get; set; } = "Active";
        public DateTime CreatedAt { get; set; }
        public DateTime? DeletedAt { get; set; }

        public Risk Risk { get; set; } = null!;
        public ICollection<MitigationTask> Tasks { get; set; } = new List<MitigationTask>();
    }
}
