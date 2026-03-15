using System.ComponentModel.DataAnnotations.Schema;

namespace WEB_Sentro.Data.Entities
{
    public class ProjectTask
    {
        public int ProjectTaskId { get; set; }
        public int ProjectId { get; set; }
        public int? ParentTaskId { get; set; }
        public string? WbsCode { get; set; } // 1.1, 1.2.1
        public string? TaskCode { get; set; } // T-1001
        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        public string TaskType { get; set; } = "Task"; // Phase, Milestone, Task
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string Status { get; set; } = "NotStarted"; // NotStarted, InProgress, Completed, OnHold
        public int PercentComplete { get; set; }
        public string? AssignedToUserId { get; set; }
        public int SortOrder { get; set; }
        public decimal? Budget { get; set; }

        // Audit Fields
        public DateTime CreatedAt { get; set; }
        public string? CreatedByUserId { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedByUserId { get; set; }

        // Navigation
        public Project Project { get; set; } = null!;
        public ProjectTask? ParentTask { get; set; }
        public ICollection<ProjectTask> SubTasks { get; set; } = new List<ProjectTask>();
    }
}
