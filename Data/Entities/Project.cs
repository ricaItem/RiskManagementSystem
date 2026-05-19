using System.ComponentModel.DataAnnotations.Schema;

namespace WEB_Sentro.Data.Entities
{
    public class Project
    {
        public int ProjectId { get; set; }
        public int OrgId { get; set; }
        public string ProjectCode { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public string Status { get; set; } = "Draft"; // Draft, Active, OnHold, Completed, Archived
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public decimal? Budget { get; set; }
        public string Currency { get; set; } = "PHP";
        public string? ManagerUserId { get; set; }
        public int? SiteId { get; set; }

        // Audit Fields
        public DateTime CreatedAt { get; set; }
        public string? CreatedByUserId { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedByUserId { get; set; }
        public DateTime? DeletedAt { get; set; }

        // Navigation
        public Site? Site { get; set; }
        public ICollection<ProjectTask> Tasks { get; set; } = new List<ProjectTask>();
        public ICollection<Risk> Risks { get; set; } = new List<Risk>();
        public ICollection<PurchaseOrder> PurchaseOrders { get; set; } = new List<PurchaseOrder>();
        public ICollection<Incident> Incidents { get; set; } = new List<Incident>();
        public ICollection<Expense> Expenses { get; set; } = new List<Expense>();
        public ICollection<ChangeOrder> ChangeOrders { get; set; } = new List<ChangeOrder>();
    }
}
