namespace WEB_Sentro.Data.Entities
{
    public class MitigationTask
    {
        public int TaskId { get; set; }
        public int PlanId { get; set; }
        public string? AssignedToUserId { get; set; }
        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        public DateTime? DueDate { get; set; }
        public string Status { get; set; } = "ToDo";
        public int ProgressPercent { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public MitigationPlan Plan { get; set; } = null!;
    }
}
