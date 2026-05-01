using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using WEB_Sentro.Data;
using WEB_Sentro.Models.Identity;
using WEB_Sentro.Services;
using WEB_Sentro.Areas.Client.Models;

namespace WEB_Sentro.Areas.Client.Controllers
{
    [Area("Client")]
    [Authorize(Policy = "MainAdminOnly")]
    public class DashboardController : Controller
    {
        private readonly IIncidentService _incidentService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ITenantDbFactory _tenantDbFactory;

        public DashboardController(IIncidentService incidentService, UserManager<ApplicationUser> userManager, ITenantDbFactory tenantDbFactory)
        {
            _incidentService = incidentService;
            _userManager = userManager;
            _tenantDbFactory = tenantDbFactory;
        }

        private async Task<int?> ResolveOrgIdAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return null;
            return user.OrganizationId > 0 ? user.OrganizationId : null;
        }

        public IActionResult Index()
        {
            return View();
        }

        public async Task<IActionResult> DashboardContent(DateTime? startDate = null, DateTime? endDate = null, int? siteId = null)
        {
            var orgId = await ResolveOrgIdAsync();
            if (!orgId.HasValue) return Forbid();

            // Defaults
            var start = startDate ?? DateTime.Today.AddMonths(-6);
            var end = endDate ?? DateTime.Today;

            var model = new WEB_Sentro.Areas.Client.Models.DashboardViewModel
            {
                StartDate = start,
                EndDate = end,
                SelectedSiteId = siteId
            };

            await using var db = await _tenantDbFactory.CreateAsync(orgId.Value);

            // Populate Sites
            var sites = await db.Sites.AsNoTracking()
                .Where(s => s.OrgId == orgId.Value && s.Status != "Archived")
                .OrderBy(s => s.SiteName)
                .Select(s => new { s.SiteId, s.SiteName })
                .ToListAsync();

            model.Sites = sites.Select(s => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
            {
                Value = s.SiteId.ToString(),
                Text = s.SiteName,
                Selected = siteId.HasValue && siteId.Value == s.SiteId
            }).ToList();
            model.Sites.Insert(0, new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = "", Text = "Global View", Selected = !siteId.HasValue });

            // 1. Incidents (supports full filtering)
            var incidentStats = await _incidentService.GetIncidentStatsAsync(orgId.Value, start, end, siteId);
            model.OpenIncidentsCount = incidentStats.Open;

            // 2. Overdue Items (Mitigation Tasks)
            var tasksQuery = db.MitigationTasks.AsNoTracking()
                .Include(t => t.Plan).ThenInclude(p => p.Risk)
                .Where(t => t.Plan.Risk.OrgId == orgId.Value);

            if (siteId.HasValue)
                tasksQuery = tasksQuery.Where(t => t.Plan.Risk.SiteId == siteId.Value);

            model.OverdueItemsCount = await tasksQuery
                .CountAsync(t => t.DueDate < DateTime.Today && t.Status != "Done" && t.Status != "Completed" && t.Status != "Closed");

            // 3. Pending Approvals (Purchase Orders)
            var poQuery = db.PurchaseOrders.AsNoTracking().Where(po => po.OrgId == orgId.Value);
            if (siteId.HasValue)
                poQuery = poQuery.Where(po => po.SiteId == siteId.Value);
            
            poQuery = poQuery.Where(po => po.OrderDate >= start && po.OrderDate <= end);

            model.PendingApprovalsCount = await poQuery
                .CountAsync(po => po.Status == "Pending Approval");

            // 4. Health Index (Active Risks)
            var risksQuery = db.Risks.AsNoTracking()
                .Where(r => r.OrgId == orgId.Value && r.DeletedAt == null && r.Status != "Closed_Invalid" && r.Status != "Rejected" && r.Status != "Draft");

            if (siteId.HasValue)
                risksQuery = risksQuery.Where(r => r.SiteId == siteId.Value);

            var totalRisks = await risksQuery.CountAsync();
            var criticalRisks = await risksQuery.CountAsync(r => r.Priority == "Critical");
            
            if (totalRisks > 0)
            {
                var penalty = (decimal)criticalRisks / totalRisks * 100;
                model.HealthIndex = Math.Max(0, 100 - penalty);
            }
            else
            {
                model.HealthIndex = 100;
            }

            // 5. Risk Segmentation
            var riskCategories = await risksQuery
                .GroupBy(r => r.Category)
                .Select(g => new { Category = g.Key, Count = g.Count() })
                .ToListAsync();

            model.RiskCategories = riskCategories.Select(x => new WEB_Sentro.Areas.Client.Models.RiskCategoryData 
            { 
                Category = x.Category ?? "Uncategorized", 
                Count = x.Count 
            }).ToList();

            // 6. Supplier Alerts
            // Apply date filter
            var alerts = await db.ProcurementAlerts
                .Include(a => a.Supplier)
                .Where(a => a.OrgId == orgId.Value && a.Status == "Active")
                .Where(a => a.TriggeredAt >= start && a.TriggeredAt <= end)
                .OrderByDescending(a => a.TriggeredAt)
                .Take(5)
                .ToListAsync();

            if (alerts.Any())
            {
                model.SupplierAlerts = alerts.Select(a => new WEB_Sentro.Areas.Client.Models.SupplierAlert
                {
                    PartnerName = a.Supplier?.Name ?? "Unknown",
                    RiskLevel = a.Severity ?? "Medium",
                    Status = a.Message
                }).ToList();
            }
            else
            {
                 var riskySuppliers = await db.Suppliers
                    .Where(s => s.OrgId == orgId.Value && (s.DeliveryTrend == "Critical" || s.DeliveryTrend == "Poor"))
                    .Take(5)
                    .ToListAsync();
                    
                 model.SupplierAlerts = riskySuppliers.Select(s => new WEB_Sentro.Areas.Client.Models.SupplierAlert
                 {
                     PartnerName = s.Name,
                     RiskLevel = "Elevated", // Or map from DeliveryTrend
                     Status = "Performance Issue"
                 }).ToList();
            }

            // 7. Department Efficiency (derived from mitigation throughput by site)
            var taskEfficiencyQuery = db.MitigationTasks.AsNoTracking()
                .Include(t => t.Plan).ThenInclude(p => p.Risk)
                .Where(t => t.Plan != null && t.Plan.Risk.OrgId == orgId.Value && t.Plan.Risk.SiteId.HasValue && t.UpdatedAt >= start && t.UpdatedAt <= end);
            if (siteId.HasValue)
                taskEfficiencyQuery = taskEfficiencyQuery.Where(t => t.Plan.Risk.SiteId == siteId.Value);

            var taskEfficiencyRows = await taskEfficiencyQuery
                .Select(t => new { SiteId = t.Plan.Risk.SiteId!.Value, t.Status })
                .ToListAsync();
            var siteNameById = sites.ToDictionary(s => s.SiteId, s => s.SiteName);
            model.DepartmentEfficiencies = taskEfficiencyRows
                .GroupBy(t => t.SiteId)
                .Select(g => new DepartmentEfficiency
                {
                    DepartmentName = siteNameById.GetValueOrDefault(g.Key, $"Site {g.Key}"),
                    EfficiencyPercentage = g.Any()
                        ? (int)Math.Round(g.Count(x => x.Status == "Done" || x.Status == "Completed") * 100.0 / g.Count())
                        : 0
                })
                .OrderByDescending(x => x.EfficiencyPercentage)
                .Take(5)
                .ToList();

            // 8. Risk Trend (data-driven over last 6 months)
            var monthStart = new DateTime(end.Year, end.Month, 1).AddMonths(-5);
            var monthBuckets = Enumerable.Range(0, 6)
                .Select(i => monthStart.AddMonths(i))
                .Select(d => new { Start = d, End = d.AddMonths(1), Label = d.ToString("MMM").ToUpperInvariant() })
                .ToList();

            var riskTrendQuery = db.Risks.AsNoTracking()
                .Where(r => r.OrgId == orgId.Value && r.DeletedAt == null && r.CreatedAt >= monthStart && r.CreatedAt < monthBuckets.Last().End);
            if (siteId.HasValue)
                riskTrendQuery = riskTrendQuery.Where(r => r.SiteId == siteId.Value);
            var riskTrendRows = await riskTrendQuery
                .Select(r => new { r.CreatedAt, r.UpdatedAt, r.Priority, r.Status })
                .ToListAsync();

            model.RiskTrend = monthBuckets.Select(m => new RiskTrendData
            {
                Month = m.Label,
                Resolved = riskTrendRows.Count(r => r.UpdatedAt.HasValue
                                                    && r.UpdatedAt.Value >= m.Start
                                                    && r.UpdatedAt.Value < m.End
                                                    && (r.Status == "Closed_Controlled" || r.Status == "Closed_Invalid")),
                Critical = riskTrendRows.Count(r => r.CreatedAt >= m.Start
                                                   && r.CreatedAt < m.End
                                                   && r.Priority == "Critical")
            }).ToList();

            return PartialView("_DashboardContent", model);
        }
    }
}
