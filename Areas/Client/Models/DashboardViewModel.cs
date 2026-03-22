using System.Collections.Generic;

namespace WEB_Sentro.Areas.Client.Models
{
    public class DashboardViewModel
    {
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int? SelectedSiteId { get; set; }
        public List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem> Sites { get; set; } = new();

        public int OpenIncidentsCount { get; set; }
        public int OverdueItemsCount { get; set; }
        public int PendingApprovalsCount { get; set; }
        public decimal HealthIndex { get; set; }

        public List<RiskTrendData> RiskTrend { get; set; } = new();
        public List<RiskCategoryData> RiskCategories { get; set; } = new();
        public List<SupplierAlert> SupplierAlerts { get; set; } = new();
        public List<DepartmentEfficiency> DepartmentEfficiencies { get; set; } = new();
        public List<StaleRiskDto> StaleRisks { get; set; } = new();
        public List<WeatherAlertDto> WeatherAlerts { get; set; } = new();
    }

    public class StaleRiskDto
    {
        public int RiskId { get; set; }
        public string Title { get; set; }
        public string Severity { get; set; }
        public int DaysStale { get; set; }
        public string ProjectName { get; set; }
    }

    public class WeatherAlertDto
    {
        public int RiskId { get; set; }
        public string Title { get; set; }
        public string Condition { get; set; }
        public DateTime TriggeredAt { get; set; }
        public string SiteName { get; set; }
    }

    public class RiskTrendData
    {
        public string Month { get; set; }
        public int Resolved { get; set; }
        public int Critical { get; set; }
    }

    public class RiskCategoryData
    {
        public string Category { get; set; }
        public int Count { get; set; }
    }

    public class SupplierAlert
    {
        public string PartnerName { get; set; }
        public string RiskLevel { get; set; } // "Critical", "Elevated"
        public string Status { get; set; }
    }

    public class DepartmentEfficiency
    {
        public string DepartmentName { get; set; }
        public int EfficiencyPercentage { get; set; }
    }
}
