using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WEB_Sentro.Data.Entities
{
    public class CostCode
    {
        public int CostCodeId { get; set; }
        public int OrgId { get; set; }
        
        [MaxLength(50)]
        public string Code { get; set; } = null!; // e.g. "03-300"
        
        [MaxLength(150)]
        public string Description { get; set; } = null!; // e.g. "Cast-in-Place Concrete"
        
        public int? ParentCostCodeId { get; set; } // For hierarchy e.g. "03-000" -> "03-300"

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Navigation
        public CostCode? ParentCostCode { get; set; }
        public ICollection<CostCode> ChildCostCodes { get; set; } = new List<CostCode>();
        
        // Relationships to Financials
        public ICollection<Expense> Expenses { get; set; } = new List<Expense>();
        public ICollection<PurchaseOrderLine> PurchaseOrderLines { get; set; } = new List<PurchaseOrderLine>();
        public ICollection<ChangeOrderLine> ChangeOrderLines { get; set; } = new List<ChangeOrderLine>();
    }
}
