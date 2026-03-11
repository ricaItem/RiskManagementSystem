using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WEB_Sentro.Models.Identity;
using WEB_Sentro.Services;
using Web_Sentro.Areas.Client.Models;

namespace Web_Sentro.Areas.Client.Controllers
{
    [Area("Client")]
    [Authorize]
    public class RisksController : Controller
    {
        private readonly RiskService _riskService;
        private readonly RiskEvaluationService _evaluationService;
        private readonly RiskAttachmentService _attachmentService;
        private readonly ITenantDbFactory _tenantDbFactory;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly MonitoringHubService _monitoringHub;
        private readonly IOpenWeatherService _openWeather;
        private readonly IWebHostEnvironment _env;
        private readonly IConfiguration _config;
        private readonly INotificationService _notificationService;
        private readonly RiskExportService _exportService;
        private readonly IRiskVersionService _versionService;
        private readonly IProcurementOverdueService _procurementOverdueService;
        private readonly IIncidentService _incidentService;

        public RisksController(RiskService riskService, RiskEvaluationService evaluationService, RiskAttachmentService attachmentService, ITenantDbFactory tenantDbFactory, UserManager<ApplicationUser> userManager, MonitoringHubService monitoringHub, IOpenWeatherService openWeather, IWebHostEnvironment env, IConfiguration config, INotificationService notificationService, RiskExportService exportService, IRiskVersionService versionService, IProcurementOverdueService procurementOverdueService, IIncidentService incidentService)
        {
            _riskService = riskService;
            _evaluationService = evaluationService;
            _attachmentService = attachmentService;
            _tenantDbFactory = tenantDbFactory;
            _userManager = userManager;
            _monitoringHub = monitoringHub;
            _openWeather = openWeather;
            _env = env;
            _config = config;
            _notificationService = notificationService;
            _exportService = exportService;
            _versionService = versionService;
            _procurementOverdueService = procurementOverdueService;
            _incidentService = incidentService;
        }

        private async Task<ApplicationUser?> GetCurrentUserAsync() => await _userManager.GetUserAsync(User);

        private async Task<int?> GetMyOrgIdAsync()
        {
            var me = await GetCurrentUserAsync();
            return me?.OrganizationId;
        }

        private bool IsSuperAdmin() => User.IsInRole("SuperAdmin");
        private bool IsAdmin() => User.IsInRole("Admin");
        private bool IsRiskManager() => User.IsInRole("RiskManager");
        private bool EmployeeOnly() => !IsRiskManager() && !IsAdmin();

        public async Task<IActionResult> Identification(string? search, string? status, string? category, int? siteId = null, bool showDeleted = false, int page = 1, int pageSize = 10, int? createFromIncidentId = null)
        {
            if (createFromIncidentId.HasValue)
            {
                var user = await GetCurrentUserAsync();
                if (user != null)
                {
                    var orgId = IsSuperAdmin() ? null : await GetMyOrgIdAsync();
                    if (orgId.HasValue)
                    {
                        var incident = await _incidentService.GetIncidentByIdAsync(createFromIncidentId.Value, orgId.Value);
                        if (incident != null)
                        {
                            ViewBag.NewRiskTitle = $"Risk from Incident: {incident.Title}";
                            ViewBag.NewRiskDescription = incident.Description;
                            ViewBag.NewRiskSiteId = incident.SiteId;
                            ViewBag.NewRiskCategory = "Safety";
                            ViewBag.NewRiskSourceType = "Incident";
                            ViewBag.AutoOpenNewRiskModal = true;
                        }
                    }
                }
            }
            return View();
        }

        public async Task<IActionResult> IdentificationContent(string? search, string? status, string? category, int? siteId = null, bool showDeleted = false, int page = 1, int pageSize = 10)
        {
            // Removed simulated delay for optimization
            ViewData["Title"] = "Risk Identification";
            var user = await GetCurrentUserAsync();
            if (user == null) return Challenge();

            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 5, 50);

            var orgId = IsSuperAdmin() ? null : await GetMyOrgIdAsync();
            var employeeOnly = EmployeeOnly();
            var includeDeleted = showDeleted && (IsAdmin() || IsSuperAdmin());
            var list = await _riskService.GetRisksForListAsync(orgId, user.Id, employeeOnly, search, status, category, siteId, includeDeleted);

            ViewBag.KpiTotalRisks = list.Count;
            ViewBag.KpiForReview = list.Count(r => r.Status == "For_Review" || r.Status == "Submitted" || r.Status == "Reviewed");
            ViewBag.KpiCritical = list.Count(r => r.Priority == "Critical");
            ViewBag.KpiSitePins = list.Count(r => r.SiteId.HasValue || !string.IsNullOrWhiteSpace(r.ProjectSite));

            if (orgId.HasValue)
            {
                await using var db = await _tenantDbFactory.CreateAsync(orgId.Value);
                var sites = await db.Sites.AsNoTracking().Where(s => s.OrgId == orgId.Value && s.Status != "Archived").OrderBy(s => s.SiteName).Select(s => new { s.SiteId, s.SiteName, s.SiteCode }).ToListAsync();
                var siteOptions = sites.Select(s => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = s.SiteId.ToString(), Text = $"{s.SiteName} ({s.SiteCode})" }).ToList();
                var filterList = new List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem> { new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = "", Text = "All Sites" } };
                filterList.AddRange(siteOptions);
                ViewBag.SiteFilterList = filterList;
                var formList = new List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem> { new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = "", Text = "Unassigned" } };
                formList.AddRange(siteOptions);
                ViewBag.SitesForRiskForm = formList;
            }
            else
            {
                ViewBag.SiteFilterList = new List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem>();
                ViewBag.SitesForRiskForm = new List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem>();
            }
            ViewBag.SelectedSiteId = siteId;
            var totalCount = list.Count;
            var items = list.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            var model = new PagedResult<RiskIdentificationViewModel>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = page,
                PageSize = pageSize
            };
            ViewBag.CurrentUserId = user.Id;
            ViewBag.IsEmployeeOnly = employeeOnly;
            ViewBag.IsAdmin = IsAdmin() || IsSuperAdmin();
            ViewBag.IsRiskManager = IsRiskManager() || IsAdmin() || IsSuperAdmin();
            ViewBag.ShowDeleted = includeDeleted;
            return PartialView("_IdentificationContent", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> IdentifyNewRisk([Bind("Title,Category,ProjectSite,Description,SourceType,SiteId")] RiskIdentificationViewModel model, string? submitType)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Challenge();

            if (string.IsNullOrWhiteSpace(model?.Title))
                return RedirectToAction("Identification");

            var status = string.Equals(submitType?.Trim(), "Submit", StringComparison.OrdinalIgnoreCase)
              ? "Submitted"
              : "Draft";
            var risk = await _riskService.CreateRiskAsync(
                user.OrganizationId,
                user.Id,
                model.Title.Trim(),
                model.Category,
                model.SourceType,
                model.ProjectSite?.Trim(),
                model.Description?.Trim(),
                status,
                model.SiteId);

            await using (var db = await _tenantDbFactory.CreateAsync(user.OrganizationId))
            {
            var auditAction = status == "Submitted" ? "RiskCreatedSubmitted" : "RiskCreatedDraft";
                _riskService.AddAuditLog(db, user.OrganizationId, user.Id, "Risk", risk.RiskId, auditAction, $"Risk created: {model.Title}", HttpContext.Connection.RemoteIpAddress?.ToString());
                await _riskService.SaveChangesAsync(db);
            }

            var attachmentFiles = Request.Form.Files.Where(f => f.Name == "Attachments").ToList();
            if (attachmentFiles.Count > 0)
            {
                var attachResult = await _attachmentService.SaveAttachmentsAsync(risk.RiskId, user.OrganizationId, user.Id, attachmentFiles, HttpContext.Connection.RemoteIpAddress?.ToString());
                if (!attachResult.Ok && !string.IsNullOrEmpty(attachResult.Error))
                    TempData["AttachmentError"] = attachResult.Error;
            }
            TempData["SuccessMessage"] = status == "Submitted" ? "Risk submitted successfully." : "Risk draft saved.";
            return RedirectToAction("Identification");
        }

        [HttpGet]
        public async Task<IActionResult> Assess(int id)
        {
            ViewData["Title"] = "Risk Assessment";
            var user = await GetCurrentUserAsync();
            if (user == null) return Challenge();

            var orgId = IsSuperAdmin() ? null : await GetMyOrgIdAsync();
            var vm = await _evaluationService.GetAssessmentViewModelAsync(id, orgId, IsSuperAdmin());
            if (vm == null) return NotFound();
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveAssessment(RiskAssessmentViewModel model)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Challenge();

            if (!IsRiskManager() && !IsAdmin())
                return Forbid();

            var orgId = user.OrganizationId;
            var (ok, error) = await _evaluationService.SaveAssessmentAsync(
                model.RiskId,
                orgId,
                user.Id,
                model.Likelihood,
                model.Impact,
                model.IsInherent,
                null,
                model.TreatmentDecision,
                model.TreatmentJustification,
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                IsSuperAdmin());
            if (!ok)
            {
                if (error != null) TempData["AssessmentError"] = error;
                return RedirectToAction("Assess", new { id = model.RiskId });
            }

            var risk = await _riskService.GetByIdForOrgAsync(model.RiskId, orgId, IsSuperAdmin());
            var status = risk?.Status ?? "";
            if (status == "MitigationRequired")
            {
                await _riskService.EnsureMitigationPlanExistsAsync(model.RiskId, orgId, user.Id);
                if (risk != null)
                    await _notificationService.NotifyRiskEventAsync(orgId, "MitigationRequired", model.RiskId, "Mitigation required", $"Risk '{risk.Title}' (High/Critical) requires a mitigation plan.", risk.ReportByUserId, HttpContext.RequestAborted);
                return RedirectToAction("Board", "Mitigation", new { area = "Client", riskId = model.RiskId });
            }
            return RedirectToAction("Identification");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitRisk(int RiskId)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Challenge();

            var orgId = IsSuperAdmin() ? null : await GetMyOrgIdAsync();
            var submitted = await _riskService.SubmitRiskAsync(RiskId, orgId, user.Id, EmployeeOnly());
            if (!submitted) return NotFound();

            if (orgId.HasValue)
            {
                await using var db = await _tenantDbFactory.CreateAsync(orgId.Value);
                _riskService.AddAuditLog(db, user.OrganizationId, user.Id, "Risk", RiskId, "RiskSubmitted", "Risk submitted", HttpContext.Connection.RemoteIpAddress?.ToString());
                await _riskService.SaveChangesAsync(db);
                var risk = await _riskService.GetByIdForOrgAsync(RiskId, orgId, IsSuperAdmin());
                if (risk != null)
                    await _notificationService.NotifyRiskEventAsync(orgId.Value, "Submitted", RiskId, "Risk submitted", $"Risk '{risk.Title}' has been submitted for review.", risk.ReportByUserId, HttpContext.RequestAborted);
            }
            return RedirectToAction("Identification");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAttachment(int id)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Challenge();

            var orgId = IsSuperAdmin() ? null : await GetMyOrgIdAsync();
            var deleted = await _attachmentService.DeleteAttachmentAsync(id, orgId, user.Id, IsAdmin() || IsSuperAdmin());
            if (!deleted) return NotFound();
            return RedirectToAction("Identification");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateRisk(int RiskId, string Title, string? Category, string? SourceType, string? Priority, string ProjectSite, string? ReportedDate, int? SiteId)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Challenge();

            var orgId = IsSuperAdmin() ? null : await GetMyOrgIdAsync();
            var risk = await _riskService.GetByIdForOrgAsync(RiskId, orgId, IsSuperAdmin());
            if (risk == null) return NotFound();

            if (EmployeeOnly() && (risk.ReportByUserId != user.Id || risk.Status != "Draft"))
                return Forbid();

            await _riskService.UpdateRiskAsync(RiskId, orgId, Title, Category, SourceType, null, ProjectSite, SiteId, IsSuperAdmin(), changedByUserId: user.Id);
            TempData["SuccessMessage"] = "Risk updated successfully.";
            return RedirectToAction("Identification");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SoftDelete(int id)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Challenge();
            var orgId = IsSuperAdmin() ? null : await GetMyOrgIdAsync();
            var risk = await _riskService.GetByIdForOrgAsync(id, orgId, IsSuperAdmin());
            if (risk == null) return NotFound();
            if (risk.Status == "Draft") return Forbid(); // Draft uses HardDelete only
            if (EmployeeOnly() && risk.ReportByUserId != user.Id) return Forbid();
            await _riskService.SoftDeleteAsync(id, orgId, IsSuperAdmin());
            if (orgId.HasValue)
            {
                await using var db = await _tenantDbFactory.CreateAsync(orgId.Value);
                _riskService.AddAuditLog(db, risk.OrgId, user.Id, "Risk", id, "RiskSoftDeleted", "Risk moved to trash", HttpContext.Connection.RemoteIpAddress?.ToString());
                await _riskService.SaveChangesAsync(db);
            }
            TempData["SuccessMessage"] = "Risk moved to trash.";
            return RedirectToAction("Identification");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Restore(int id)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Challenge();
            if (!IsAdmin() && !IsSuperAdmin()) return Forbid();
            var orgId = IsSuperAdmin() ? null : await GetMyOrgIdAsync();
            await _riskService.RestoreAsync(id, orgId, IsSuperAdmin());
            if (orgId.HasValue)
            {
                await using var db = await _tenantDbFactory.CreateAsync(orgId.Value);
                _riskService.AddAuditLog(db, user.OrganizationId, user.Id, "Risk", id, "RiskRestored", "Risk restored from trash", HttpContext.Connection.RemoteIpAddress?.ToString());
                await _riskService.SaveChangesAsync(db);
            }
            TempData["SuccessMessage"] = "Risk restored successfully.";
            return RedirectToAction("Identification");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> HardDelete(int id, int orgId)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Challenge();
            var scopeOrgId = IsSuperAdmin() ? null : (int?)await GetMyOrgIdAsync();
            if (scopeOrgId.HasValue && scopeOrgId.Value != orgId) return Forbid();
            var risk = await _riskService.GetByIdForOrgAsync(id, scopeOrgId, IsSuperAdmin());
            if (risk == null) return NotFound();
            if (risk.Status != "Draft") return Forbid();
            if (risk.ReportByUserId != user.Id && !IsAdmin() && !IsSuperAdmin()) return Forbid();
            await _attachmentService.DeleteAllAttachmentsForRiskAsync(id, orgId);
            var ok = await _riskService.HardDeleteAsync(id, scopeOrgId, user.Id, IsSuperAdmin(), allowOnlyDraft: true);
            if (!ok) return NotFound();
            await using (var db = await _tenantDbFactory.CreateAsync(orgId))
            {
                _riskService.AddAuditLog(db, orgId, user.Id, "Risk", id, "RiskHardDeleted", "Risk permanently deleted", HttpContext.Connection.RemoteIpAddress?.ToString());
                await _riskService.SaveChangesAsync(db);
            }
            TempData["SuccessMessage"] = "Risk permanently deleted.";
            return RedirectToAction("Identification");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Review(int id)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Challenge();
            if (!IsRiskManager() && !IsAdmin() && !IsSuperAdmin()) return Forbid();
            var orgId = IsSuperAdmin() ? null : await GetMyOrgIdAsync();
            var ok = await _riskService.ReviewRiskAsync(id, orgId, user.Id);
            if (!ok)
            {
                var risk = await _riskService.GetByIdForOrgAsync(id, orgId, IsSuperAdmin());
                if (risk != null && risk.ReportByUserId == user.Id)
                {
                    TempData["ReviewError"] = "You cannot review a risk you created.";
                    return RedirectToAction("Identification");
                }
                return NotFound();
            }
            if (orgId.HasValue)
            {
                await using var db = await _tenantDbFactory.CreateAsync(orgId.Value);
                _riskService.AddAuditLog(db, user.OrganizationId, user.Id, "Risk", id, "RiskReviewed", "Status: Submitted → Reviewed", HttpContext.Connection.RemoteIpAddress?.ToString());
                await _riskService.SaveChangesAsync(db);
            }
            return RedirectToAction("Identification");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Challenge();
            if (!IsRiskManager() && !IsAdmin() && !IsSuperAdmin()) return Forbid();
            var orgId = IsSuperAdmin() ? null : await GetMyOrgIdAsync();
            var ok = await _riskService.ApproveRiskAsync(id, orgId, user.Id);
            if (!ok)
            {
                var risk = await _riskService.GetByIdForOrgAsync(id, orgId, IsSuperAdmin());
                if (risk != null && risk.ReportByUserId == user.Id)
                {
                    TempData["ReviewError"] = "You cannot approve a risk you created.";
                    return RedirectToAction("Identification");
                }
                return NotFound();
            }
            if (orgId.HasValue)
            {
                await using var db = await _tenantDbFactory.CreateAsync(orgId.Value);
                _riskService.AddAuditLog(db, user.OrganizationId, user.Id, "Risk", id, "RiskApproved", "Status: Reviewed → Approved", HttpContext.Connection.RemoteIpAddress?.ToString());
                await _riskService.SaveChangesAsync(db);
            }
            return RedirectToAction("Identification");
        }

        [HttpGet]
        public async Task<IActionResult> ExportExcel(CancellationToken ct)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Challenge();
            var orgId = IsSuperAdmin() ? null : await GetMyOrgIdAsync();
            if (!orgId.HasValue) return Forbid();
            var bytes = await _exportService.ExportToExcelAsync(orgId.Value, user.Id, EmployeeOnly(), ct);
            var fileName = $"RiskRegister_{orgId.Value}_{DateTime.UtcNow:yyyyMMdd}.xlsx";
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkReviewed(int id)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Challenge();
            if (!IsRiskManager() && !IsAdmin() && !IsSuperAdmin()) return Forbid();
            var orgId = IsSuperAdmin() ? null : await GetMyOrgIdAsync();
            var ok = await _riskService.MarkReviewedAsync(id, orgId, user.Id, IsSuperAdmin());
            if (!ok) return NotFound();
            if (orgId.HasValue)
            {
                await using var db = await _tenantDbFactory.CreateAsync(orgId.Value);
                _riskService.AddAuditLog(db, user.OrganizationId, user.Id, "Risk", id, "RiskMarkedReviewed", "Next review date set from band frequency", HttpContext.Connection.RemoteIpAddress?.ToString());
                await _riskService.SaveChangesAsync(db);
            }
            return RedirectToAction("Identification");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id, string? RejectRemarks)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Challenge();
            if (!IsRiskManager() && !IsAdmin() && !IsSuperAdmin()) return Forbid();
            if (string.IsNullOrWhiteSpace(RejectRemarks)) { TempData["RejectError"] = "Remarks are required when rejecting."; return RedirectToAction("Identification"); }
            var orgId = IsSuperAdmin() ? null : await GetMyOrgIdAsync();
            var ok = await _riskService.RejectRiskAsync(id, orgId, user.Id, RejectRemarks.Trim());
            if (!ok) return NotFound();
            if (orgId.HasValue)
            {
                await using var db = await _tenantDbFactory.CreateAsync(orgId.Value);
                _riskService.AddAuditLog(db, user.OrganizationId, user.Id, "Risk", id, "RiskRejected", $"Status set to Rejected. Remarks: {RejectRemarks.Trim()}", HttpContext.Connection.RemoteIpAddress?.ToString());
                await _riskService.SaveChangesAsync(db);
            }
            return RedirectToAction("Identification");
        }

        [HttpGet]
        public async Task<IActionResult> Monitoring(int? siteId = null)
        {
            ViewData["Title"] = "Risk Monitoring Hub";
            var user = await GetCurrentUserAsync();
            if (user == null) return Challenge();

            var orgId = IsSuperAdmin() ? null : await GetMyOrgIdAsync();
            if (!orgId.HasValue) return View(new RiskMonitoringViewModel());

            await _procurementOverdueService.CheckOverduePurchaseOrdersAsync(orgId.Value, user.Id, HttpContext.RequestAborted);

            var hubSites = await _monitoringHub.GetSitesForHubAsync(orgId.Value);
            var siteList = hubSites.Select(s => new MonitoringSiteItemViewModel
            {
                SiteId = s.MonitoringSiteId > 0 ? s.MonitoringSiteId : -s.DbSiteId,
                Name = s.Name,
                SiteName = null,
                Latitude = s.Latitude,
                Longitude = s.Longitude
            }).ToList();
            var selectedId = siteId ?? siteList.FirstOrDefault()?.SiteId ?? 0;
            var selectedSite = siteList.FirstOrDefault(s => s.SiteId == selectedId) ?? siteList.FirstOrDefault();

            var temperature = 0.0;
            var weatherCondition = "—";
            var windSpeed = 0.0;
            var apiOk = false;
            if (selectedSite != null)
            {
                var weather = await _openWeather.GetWeatherAsync(selectedSite.Latitude, selectedSite.Longitude);
                temperature = weather.TempC;
                weatherCondition = weather.Condition ?? (weather.WeatherId > 0 ? $"Id {weather.WeatherId}" : "—");
                windSpeed = weather.WindSpeedKmh;
                apiOk = weather.ApiOk;
            }

            var monitoringSiteIdForAlerts = selectedId > 0 ? selectedId : (int?)null;
            var systemAlerts = await _monitoringHub.GetRecentAlertsAsync(orgId.Value, monitoringSiteIdForAlerts, 20);
            var lastSync = monitoringSiteIdForAlerts.HasValue ? await _monitoringHub.GetLastSyncUtcAsync(orgId.Value, monitoringSiteIdForAlerts.Value) : null;
            if (TempData["LastSyncUtc"] is DateTime tdSync) lastSync = tdSync;
            if (TempData["ApiHealthOk"] is bool tdApi) apiOk = (bool)tdApi;

            var activeCount = await _riskService.GetActiveRisksCountAsync(orgId, IsSuperAdmin());
            var highPriority = await _riskService.GetHighPriorityRisksAsync(orgId, IsSuperAdmin(), 10);

            var ackUserIds = systemAlerts.Where(a => !string.IsNullOrEmpty(a.AcknowledgedByUserId)).Select(a => a.AcknowledgedByUserId!).Distinct().ToList();
            var ackUserNames = new Dictionary<string, string>();
            if (ackUserIds.Count > 0)
            {
                foreach (var uid in ackUserIds)
                {
                    var u = await _userManager.FindByIdAsync(uid);
                    ackUserNames[uid] = u != null ? $"{u.FirstName} {u.LastName}".Trim() : "Unknown";
                }
            }


            var posture = new SiteRiskPostureViewModel
            {
                ActiveAlertsCount = systemAlerts.Count(a => a.Status == "Active"),
                CriticalAlertsCount = systemAlerts.Count(a => a.Severity == "Critical" && a.Status == "Active"),
                OpenCriticalRisksCount = monitoringSiteIdForAlerts.HasValue ? await _riskService.GetOpenCriticalRisksCountForSiteAsync(orgId.Value, monitoringSiteIdForAlerts.Value) : 0,
                OverdueMitigationTasksCount = monitoringSiteIdForAlerts.HasValue ? await _riskService.GetOverdueMitigationTasksCountForSiteAsync(orgId.Value, monitoringSiteIdForAlerts) : 0
            };

            var forecastChips = new List<ForecastChipViewModel>();
            if (selectedSite != null)
            {
                forecastChips.Add(new ForecastChipViewModel { Label = "Wind peak", Value = $"{windSpeed:F0} km/h (current)" });
                forecastChips.Add(new ForecastChipViewModel { Label = "Rain next 6h", Value = "—" });
            }

            var model = new RiskMonitoringViewModel
            {
                ProjectName = selectedSite?.Name ?? "Select site",
                Latitude = selectedSite?.Latitude ?? 7.0707,
                Longitude = selectedSite?.Longitude ?? 125.6083,
                Temperature = temperature,
                WeatherCondition = weatherCondition,
                WindSpeed = windSpeed,
                ActiveRisksCount = activeCount,
                HighPriorityRisks = highPriority,
                Sites = siteList,
                SelectedSiteId = selectedId,
                SystemAlerts = systemAlerts.Select(a => new MonitoringAlertItemViewModel
                {
                    AlertId = a.AlertId,
                    RuleName = a.RuleName,
                    MeasuredValues = a.MeasuredValues,
                    Severity = a.Severity,
                    Status = a.Status,
                    TriggeredAt = a.TriggeredAt,
                    ResolvedAtUtc = a.ResolvedAtUtc,
                    AcknowledgedAtUtc = a.AcknowledgedAtUtc,
                    AcknowledgedByDisplayName = a.AcknowledgedByUserId != null && ackUserNames.TryGetValue(a.AcknowledgedByUserId, out var dn) ? dn : null,
                    RiskId = a.RiskId
                }).ToList(),
                LastSyncUtc = lastSync,
                ApiHealthOk = apiOk,
                SiteRiskPosture = posture,
                ForecastChips = forecastChips
            };
            ViewData["EnableSimulation"] = _env.IsDevelopment() || string.Equals(_config["Monitoring:EnableSimulation"], "true", StringComparison.OrdinalIgnoreCase);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MonitoringSync(int siteId, string? simulate = null)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Challenge();
            var orgId = IsSuperAdmin() ? null : await GetMyOrgIdAsync();
            if (!orgId.HasValue) return RedirectToAction(nameof(Monitoring));

            int monitoringSiteId;
            if (siteId <= 0)
            {
                var dbSiteId = -siteId;
                monitoringSiteId = await _monitoringHub.EnsureMonitoringSiteForSiteAsync(orgId.Value, dbSiteId, HttpContext.RequestAborted);
                if (monitoringSiteId == 0) return RedirectToAction(nameof(Monitoring));
            }
            else
            {
                monitoringSiteId = siteId;
            }

            var (lastSync, apiOk) = await _monitoringHub.RunSyncForSiteAsync(orgId.Value, monitoringSiteId, user.Id, simulate, HttpContext.RequestAborted);
            if (lastSync.HasValue) TempData["LastSyncUtc"] = lastSync.Value;
            TempData["ApiHealthOk"] = apiOk;
            return RedirectToAction(nameof(Monitoring), new { siteId = monitoringSiteId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AcknowledgeAlert(int alertId, int? siteId = null)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Challenge();
            var orgId = IsSuperAdmin() ? null : await GetMyOrgIdAsync();
            if (!orgId.HasValue) return RedirectToAction(nameof(Monitoring));
            var ok = await _monitoringHub.AcknowledgeAlertAsync(orgId.Value, alertId, user.Id, HttpContext.RequestAborted);
            if (!ok) return NotFound();
            return RedirectToAction(nameof(Monitoring), new { siteId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResolveAlert(int alertId, int? siteId = null)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Challenge();
            var orgId = IsSuperAdmin() ? null : await GetMyOrgIdAsync();
            if (!orgId.HasValue) return RedirectToAction(nameof(Monitoring));
            var ok = await _monitoringHub.ResolveAlertAsync(orgId.Value, alertId, user.Id, HttpContext.RequestAborted);
            if (!ok) return NotFound();
            return RedirectToAction(nameof(Monitoring), new { siteId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateMitigationPlanFromAlert(int alertId)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Challenge();
            var orgId = IsSuperAdmin() ? null : await GetMyOrgIdAsync();
            if (!orgId.HasValue) return RedirectToAction(nameof(Monitoring));
            var alert = await _monitoringHub.GetAlertAsync(orgId.Value, alertId, HttpContext.RequestAborted);
            if (alert == null) return NotFound();
            int riskId;
            if (alert.RiskId.HasValue)
            {
                riskId = alert.RiskId.Value;
                await _riskService.EnsureAutoRiskEvaluationForRiskAsync(riskId, orgId.Value, alert.Severity, alert.MeasuredValues, user.Id, HttpContext.RequestAborted);
            }
            else
            {
                var created = await _monitoringHub.CreateRiskFromAlertAndLinkAsync(orgId.Value, alertId, user.Id, HttpContext.RequestAborted);
                if (!created.HasValue) return NotFound();
                riskId = created.Value;
            }
            await _riskService.EnsureMitigationPlanExistsAsync(riskId, orgId.Value, user.Id, alert.Severity, HttpContext.RequestAborted);
            return RedirectToAction("Board", "Mitigation", new { area = "Client", riskId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OpenRiskFromAlert(int alertId)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Challenge();
            var orgId = IsSuperAdmin() ? null : await GetMyOrgIdAsync();
            if (!orgId.HasValue) return RedirectToAction(nameof(Monitoring));

            var alert = await _monitoringHub.GetAlertAsync(orgId.Value, alertId, HttpContext.RequestAborted);
            if (alert == null) return NotFound();

            int riskId;
            if (alert.RiskId.HasValue)
            {
                riskId = alert.RiskId.Value;
            }
            else
            {
                var created = await _monitoringHub.CreateRiskFromAlertAndLinkAsync(orgId.Value, alertId, user.Id, HttpContext.RequestAborted);
                if (!created.HasValue) return NotFound();
                riskId = created.Value;
            }

            return RedirectToAction(nameof(Assess), new { id = riskId });
        }

        [HttpGet]
        public async Task<IActionResult> GetMapData(CancellationToken ct)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Unauthorized();
            var orgId = IsSuperAdmin() ? null : await GetMyOrgIdAsync();
            if (!orgId.HasValue) return Ok(new List<object>());
            
            var data = await _monitoringHub.GetMapDataAsync(orgId.Value, ct);
            return Ok(data);
        }

        [HttpGet]
        public async Task<IActionResult> GetSiteDetails(int id, CancellationToken ct)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Unauthorized();
            var orgId = IsSuperAdmin() ? null : await GetMyOrgIdAsync();
            if (!orgId.HasValue) return NotFound();
            
            var details = await _monitoringHub.GetSiteDetailsAsync(orgId.Value, id, ct);
            if (details == null) return NotFound();
            return Ok(details);
        }

        [HttpGet]
        public async Task<IActionResult> GetRiskVersions(int id)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Unauthorized();
            var orgId = IsSuperAdmin() ? null : await GetMyOrgIdAsync();
            if (!orgId.HasValue) return BadRequest();

            var versions = await _versionService.GetVersionsAsync(id, orgId.Value);
            return Json(versions);
        }

        [HttpGet]
        public async Task<IActionResult> GetRiskControls(int id)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Unauthorized();
            var orgId = IsSuperAdmin() ? null : await GetMyOrgIdAsync();
            if (!orgId.HasValue) return BadRequest();

            await using var db = await _tenantDbFactory.CreateAsync(orgId.Value);

            // Fetch linked mitigation tasks as controls
            var tasks = await db.MitigationTasks.AsNoTracking()
                .Where(t => t.Plan != null && t.Plan.RiskId == id && t.Plan.Risk.OrgId == orgId.Value)
                .OrderBy(t => t.Status == "Done" ? 1 : 0)
                .ThenBy(t => t.DueDate)
                .Select(t => new
                {
                    t.TaskId,
                    t.Title,
                    t.Status,
                    t.DueDate,
                    t.ProgressPercent
                })
                .ToListAsync();

            return Json(tasks);
        }
    }
}

