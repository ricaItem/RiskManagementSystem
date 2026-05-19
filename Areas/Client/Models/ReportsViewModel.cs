using System.Collections.Generic;

namespace WEB_Sentro.Areas.Client.Models
{
    public class ReportsViewModel
    {
        // Global Filters
        public string DateRange { get; set; } = "Last 30 Days";
        public string Site { get; set; } = "All Sites";

        // Financials (Chart: Bar/Line)
        public decimal TotalSpend { get; set; }
        public decimal BudgetUtilization { get; set; }
        public List<ChartDataPoint> SpendByCategory { get; set; } = new();
        public List<ChartDataPoint> MonthlySpendTrend { get; set; } = new();

        // Safety (Chart: Line)
        public int TotalIncidents { get; set; }
        public int OpenIncidents { get; set; }
        public List<ChartDataPoint> IncidentsOverTime { get; set; } = new();
        
        // Supplier Risk (Chart: Doughnut)
        public int TotalSuppliers { get; set; }
        public int CriticalSuppliers { get; set; }
        public List<ChartDataPoint> SupplierRiskDistribution { get; set; } = new();

        // Compliance (Chart: Area/Line)
        public decimal AuditComplianceScore { get; set; }
        public List<ChartDataPoint> AuditIssuesTrend { get; set; } = new();
    }

    public class ChartDataPoint
    {
        public string Label { get; set; }
        public decimal Value { get; set; }
        public string Color { get; set; } // Hex code for chart
    }
}
