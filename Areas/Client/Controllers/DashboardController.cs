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
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

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

        public IActionResult Index()
        {
            return View();
        }

        public async Task<IActionResult> DashboardContent(DateTime? startDate = null, DateTime? endDate = null, int? siteId = null)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var orgId = user.OrganizationId > 0 ? user.OrganizationId : 1;

            // Defaults
            var start = startDate ?? DateTime.Today.AddMonths(-6);
            var end = endDate ?? DateTime.Today;

            var model = new WEB_Sentro.Areas.Client.Models.DashboardViewModel
            {
                StartDate = start,
                EndDate = end,
                SelectedSiteId = siteId
            };

            await using var db = await _tenantDbFactory.CreateAsync(orgId);

            // Populate Sites
            var sites = await db.Sites.AsNoTracking()
                .Where(s => s.OrgId == orgId && s.Status != "Archived")
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
            var incidentStats = await _incidentService.GetIncidentStatsAsync(orgId, start, end, siteId);
            model.OpenIncidentsCount = incidentStats.Open;

            // 2. Overdue Items (Mitigation Tasks)
            var tasksQuery = db.MitigationTasks.AsNoTracking()
                .Include(t => t.Plan).ThenInclude(p => p.Risk)
                .Where(t => t.Plan.Risk.OrgId == orgId);

            if (siteId.HasValue)
                tasksQuery = tasksQuery.Where(t => t.Plan.Risk.SiteId == siteId.Value);

            model.OverdueItemsCount = await tasksQuery
                .CountAsync(t => t.DueDate < DateTime.Today && t.Status != "Done" && t.Status != "Completed" && t.Status != "Closed");

            // 3. Pending Approvals (Purchase Orders)
            var poQuery = db.PurchaseOrders.AsNoTracking().Where(po => po.OrgId == orgId);
            if (siteId.HasValue)
                poQuery = poQuery.Where(po => po.SiteId == siteId.Value);
            
            poQuery = poQuery.Where(po => po.OrderDate >= start && po.OrderDate <= end);

            model.PendingApprovalsCount = await poQuery
                .CountAsync(po => po.Status == "Pending Approval");

            // 4. Health Index (Active Risks)
            var risksQuery = db.Risks.AsNoTracking()
                .Where(r => r.OrgId == orgId && r.DeletedAt == null && r.Status != "Closed_Invalid" && r.Status != "Rejected" && r.Status != "Draft");

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
                .Where(a => a.OrgId == orgId && a.Status == "Active")
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
                    .Where(s => s.OrgId == orgId && (s.DeliveryTrend == "Critical" || s.DeliveryTrend == "Poor"))
                    .Take(5)
                    .ToListAsync();
                    
                 model.SupplierAlerts = riskySuppliers.Select(s => new WEB_Sentro.Areas.Client.Models.SupplierAlert
                 {
                     PartnerName = s.Name,
                     RiskLevel = "Elevated", // Or map from DeliveryTrend
                     Status = "Performance Issue"
                 }).ToList();
            }

            // 6.5. Stale Risks (Open, High/Critical, unmodified in 14 days)
            var staleThreshold = DateTime.UtcNow.AddDays(-14);
            var staleQuery = db.Risks.AsNoTracking()
                .Include(r => r.Project)
                .Where(r => r.OrgId == orgId && r.Status == "Open" && (r.Priority == "High" || r.Priority == "Critical"))
                .Where(r => (r.UpdatedAt ?? r.CreatedAt) < staleThreshold);

            if (siteId.HasValue)
                staleQuery = staleQuery.Where(r => r.SiteId == siteId.Value);

            var staleRisks = await staleQuery.OrderBy(r => r.UpdatedAt ?? r.CreatedAt).Take(5).ToListAsync();
            model.StaleRisks = staleRisks.Select(r => new WEB_Sentro.Areas.Client.Models.StaleRiskDto
            {
                RiskId = r.RiskId,
                Title = r.Title,
                Severity = r.Priority ?? "High",
                DaysStale = (DateTime.UtcNow - (r.UpdatedAt ?? r.CreatedAt)).Days,
                ProjectName = r.Project?.Name ?? "General Site"
            }).ToList();

            // 6.6. Weather Alerts (Recent WeatherAPI risks)
            var weatherAlertsQuery = db.Risks.AsNoTracking()
                .Include(r => r.Site)
                .Where(r => r.OrgId == orgId && r.SourceType == "WeatherAPI" && r.CreatedAt >= DateTime.UtcNow.AddHours(-48));

            if (siteId.HasValue)
                weatherAlertsQuery = weatherAlertsQuery.Where(r => r.SiteId == siteId.Value);

            var recentWeather = await weatherAlertsQuery.OrderByDescending(r => r.CreatedAt).Take(3).ToListAsync();
            model.WeatherAlerts = recentWeather.Select(r => new WEB_Sentro.Areas.Client.Models.WeatherAlertDto
            {
                RiskId = r.RiskId,
                Title = r.Title,
                Condition = r.Category ?? "Weather",
                TriggeredAt = r.CreatedAt,
                SiteName = r.Site?.SiteName ?? "General Site"
            }).ToList();

            // 7. Department Efficiency (Static)
            model.DepartmentEfficiencies = new List<WEB_Sentro.Areas.Client.Models.DepartmentEfficiency>
            {
                new WEB_Sentro.Areas.Client.Models.DepartmentEfficiency { DepartmentName = "Structural Engineering", EfficiencyPercentage = 92 },
                new WEB_Sentro.Areas.Client.Models.DepartmentEfficiency { DepartmentName = "Logistics & Supply", EfficiencyPercentage = 64 }
            };

            // 8. Risk Trend (Mocked)
            model.RiskTrend = new List<WEB_Sentro.Areas.Client.Models.RiskTrendData>
            {
                 new WEB_Sentro.Areas.Client.Models.RiskTrendData { Month = "JAN", Resolved = 30, Critical = 40 },
                 new WEB_Sentro.Areas.Client.Models.RiskTrendData { Month = "FEB", Resolved = 45, Critical = 35 },
                 new WEB_Sentro.Areas.Client.Models.RiskTrendData { Month = "MAR", Resolved = 38, Critical = 45 },
                 new WEB_Sentro.Areas.Client.Models.RiskTrendData { Month = "APR", Resolved = 60, Critical = 30 },
                 new WEB_Sentro.Areas.Client.Models.RiskTrendData { Month = "MAY", Resolved = 55, Critical = 35 },
                 new WEB_Sentro.Areas.Client.Models.RiskTrendData { Month = "JUN", Resolved = 75, Critical = 25 }
            };

            return PartialView("_DashboardContent", model);
        }

        public async Task<IActionResult> ExportPdf(DateTime? startDate = null, DateTime? endDate = null, int? siteId = null)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var orgId = user.OrganizationId > 0 ? user.OrganizationId : 1;
            var start = startDate ?? DateTime.Today.AddMonths(-6);
            var end = endDate ?? DateTime.Today;

            await using var db = await _tenantDbFactory.CreateAsync(orgId);

            // 1. Incidents
            var incidentStats = await _incidentService.GetIncidentStatsAsync(orgId, start, end, siteId);
            
            // 2. Overdue Items
            var tasksQuery = db.MitigationTasks.AsNoTracking()
                .Include(t => t.Plan).ThenInclude(p => p.Risk)
                .Where(t => t.Plan.Risk.OrgId == orgId);
            if (siteId.HasValue)
                tasksQuery = tasksQuery.Where(t => t.Plan.Risk.SiteId == siteId.Value);
            int overdueItemsCount = await tasksQuery.CountAsync(t => t.DueDate < DateTime.Today && t.Status != "Done" && t.Status != "Completed" && t.Status != "Closed");

            // 3. Pending Approvals
            var poQuery = db.PurchaseOrders.AsNoTracking().Where(po => po.OrgId == orgId);
            if (siteId.HasValue)
                poQuery = poQuery.Where(po => po.SiteId == siteId.Value);
            poQuery = poQuery.Where(po => po.OrderDate >= start && po.OrderDate <= end);
            int pendingApprovals = await poQuery.CountAsync(po => po.Status == "Pending Approval");

            QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

            var document = QuestPDF.Fluent.Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(QuestPDF.Helpers.PageSizes.A4);
                    page.Margin(2, QuestPDF.Infrastructure.Unit.Centimetre);
                    page.PageColor(QuestPDF.Helpers.Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(12));

                    page.Header().Element(ComposeHeader);
                    page.Content().Element(x => ComposeContent(x, incidentStats.Open, overdueItemsCount, pendingApprovals));
                    page.Footer().AlignCenter().Text(x =>
                    {
                        x.CurrentPageNumber();
                        x.Span(" / ");
                        x.TotalPages();
                    });
                });

                void ComposeHeader(QuestPDF.Infrastructure.IContainer container)
                {
                    var logoPath = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "wwwroot", "images", "logoo.png");

                    container.Column(column =>
                    {
                        column.Item().Row(row =>
                        {
                            if (System.IO.File.Exists(logoPath))
                            {
                                row.AutoItem().Height(60).Image(logoPath).FitHeight();
                            }
                            row.RelativeItem().PaddingLeft(15).Column(textCol =>
                            {
                                textCol.Item().Text("Sentro").FontSize(28).SemiBold().FontColor(QuestPDF.Helpers.Colors.Blue.Darken3);
                                textCol.Item().Text($"Date: {DateTime.Now:MMMM dd, yyyy}").FontSize(12).FontColor(QuestPDF.Helpers.Colors.Grey.Medium);
                            });
                        });

                        column.Item().PaddingVertical(15).LineHorizontal(1).LineColor(QuestPDF.Helpers.Colors.Grey.Lighten2);

                        column.Item().Text("Dashboard Summary Report").FontSize(20).SemiBold().FontColor(QuestPDF.Helpers.Colors.Black);
                        column.Item().Text($"Date Range: {start:MMM dd, yyyy} - {end:MMM dd, yyyy}").FontSize(12).FontColor(QuestPDF.Helpers.Colors.Grey.Darken1);
                    });
                }

                void ComposeContent(QuestPDF.Infrastructure.IContainer container, int openIncidents, int overdue, int pending)
                {
                    container.PaddingVertical(1, QuestPDF.Infrastructure.Unit.Centimetre).Column(column =>
                    {
                        column.Spacing(20);
                        column.Item().Text("Key Metrics").FontSize(16).SemiBold();

                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                            });

                            table.Header(header =>
                            {
                                header.Cell().BorderBottom(1).PaddingBottom(5).Text("Open Incidents").SemiBold();
                                header.Cell().BorderBottom(1).PaddingBottom(5).Text("Overdue Items").SemiBold();
                                header.Cell().BorderBottom(1).PaddingBottom(5).Text("Pending Approvals").SemiBold();
                            });

                            table.Cell().PaddingTop(5).Text(openIncidents.ToString());
                            table.Cell().PaddingTop(5).Text(overdue.ToString());
                            table.Cell().PaddingTop(5).Text(pending.ToString());
                        });
                    });
                }
            });

            var pdfStream = new System.IO.MemoryStream();
            document.GeneratePdf(pdfStream);
            pdfStream.Position = 0;

            return File(pdfStream, "application/pdf", $"Dashboard_Report_{DateTime.Now:yyyyMMdd}.pdf");
        }
    }
}
