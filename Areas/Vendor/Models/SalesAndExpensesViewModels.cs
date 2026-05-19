using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace WEB_Sentro.Areas.Vendor.Models
{
    public class SalesAndExpensesIndexViewModel
    {
        public string TotalSalesDisplay { get; set; } = string.Empty;
        public string TotalExpensesDisplay { get; set; } = string.Empty;
        public string ProfitDisplay { get; set; } = string.Empty;
        public bool IsProfitPositive { get; set; } = true;

        // Filtering
        public int? SelectedMonth { get; set; }
        public int? SelectedYear { get; set; }
        public List<SelectListItem> MonthOptions { get; set; } = new();
        public List<SelectListItem> YearOptions { get; set; } = new();

        public string ChartTitle { get; set; } = "Financial Analytics";

        public List<string> AnalyticsLabels { get; set; } = new();
        public List<decimal> AnalyticsIncomeData { get; set; } = new();
        public List<decimal> AnalyticsExpenseData { get; set; } = new();

        public List<TransactionViewModel> RecentTransactions { get; set; } = new();
        public List<PlatformExpenseViewModel> Expenses { get; set; } = new();
    }

    public class TransactionViewModel
    {
        public string Type { get; set; } = string.Empty; // "Income" or "Expense"
        public string Description { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public string AmountDisplay { get; set; } = string.Empty;
        public bool IsPositive { get; set; }
    }

    public class PlatformExpenseViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Description is required")]
        [StringLength(255, ErrorMessage = "Description cannot exceed 255 characters")]
        public string Description { get; set; } = string.Empty;

        [Required(ErrorMessage = "Amount is required")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than zero")]
        public decimal Amount { get; set; }

        [Required(ErrorMessage = "Date is required")]
        public DateTime ExpenseDate { get; set; }

        public string AmountDisplay { get; set; } = string.Empty;
        public string DateDisplay { get; set; } = string.Empty;
    }
}