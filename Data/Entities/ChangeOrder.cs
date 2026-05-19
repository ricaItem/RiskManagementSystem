using System.ComponentModel.DataAnnotations.Schema;

namespace WEB_Sentro.Data.Entities
{
    public class ChangeOrder
    {
        public int ChangeOrderId { get; set; }
        public int OrgId { get; set; }
        public int SiteId { get; set; }
        public int? ProjectId { get; set; } // Optional, for future use
        
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        
        public string Status { get; set; } = "Draft"; // Draft, Pending, Approved, Rejected
        
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public string? ApprovedBy { get; set; }

        public Site Site { get; set; } = null!;
        public Project? Project { get; set; }
        
        public ICollection<ChangeOrderLine> LineItems { get; set; } = new List<ChangeOrderLine>();

        [NotMapped]
        public decimal TotalAmount => LineItems?.Sum(l => l.Amount) ?? 0m;
    }
}
