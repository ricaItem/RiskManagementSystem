using System.Collections.Generic;

namespace WEB_Sentro.Areas.Client.Models
{
    public class SiteBudgetDetailsViewModel
    {
        public int SiteId { get; set; }
        public string SiteName { get; set; }
        public decimal TotalBudget { get; set; }
        public decimal TotalCommitted { get; set; }
        public decimal TotalActual { get; set; }
        
        /// <summary>
        /// TotalBudget - TotalCommitted
        /// </summary>
        public decimal BudgetVariance { get; set; }

        public List<BudgetLineItemViewModel> Items { get; set; } = new List<BudgetLineItemViewModel>();
    }

    public class BudgetLineItemViewModel
    {
        public string CostCode { get; set; }
        public string Description { get; set; }
        public decimal CommittedAmount { get; set; }
        public decimal ActualAmount { get; set; }
        public decimal TotalAmount { get; set; }
    }
}
