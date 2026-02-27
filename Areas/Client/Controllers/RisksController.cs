using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
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

        public RisksController(RiskService riskService, RiskEvaluationService evaluationService, RiskAttachmentService attachmentService, ITenantDbFactory tenantDbFactory, UserManager<ApplicationUser> userManager)
        {
            _riskService = riskService;
            _evaluationService = evaluationService;
            _attachmentService = attachmentService;
            _tenantDbFactory = tenantDbFactory;
            _userManager = userManager;
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

        public async Task<IActionResult> Identification(string? search, string? status, string? category, bool showDeleted = false, int page = 1, int pageSize = 10)
        {
            ViewData["Title"] = "Risk Identification";
            var user = await GetCurrentUserAsync();
            if (user == null) return Challenge();

            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 5, 50);

            var orgId = IsSuperAdmin() ? null : await GetMyOrgIdAsync();
            var employeeOnly = EmployeeOnly();
            var includeDeleted = showDeleted && (IsAdmin() || IsSuperAdmin());
            var list = await _riskService.GetRisksForListAsync(orgId, user.Id, employeeOnly, search, status, category, includeDeleted);
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
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> IdentifyNewRisk([Bind("Title,Category,ProjectSite,Description,SourceType")] RiskIdentificationViewModel model, string? submitType)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Challenge();

            if (string.IsNullOrWhiteSpace(model?.Title))
                return RedirectToAction("Identification");

            var status = (submitType == "Submit") ? "For_Review" : "Draft";
            var risk = await _riskService.CreateRiskAsync(
                user.OrganizationId,
                user.Id,
                model.Title.Trim(),
                model.Category,
                model.SourceType,
                model.ProjectSite?.Trim(),
                model.Description?.Trim(),
                status);

            await using (var db = await _tenantDbFactory.CreateAsync(user.OrganizationId))
            {
            var auditAction = status == "For_Review" ? "RiskCreatedForReview" : "RiskCreatedDraft";
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
            var ok = await _evaluationService.SaveAssessmentAsync(
                model.RiskId,
                orgId,
                user.Id,
                model.Likelihood,
                model.Impact,
                null,
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                IsSuperAdmin());
            if (!ok) return NotFound();

            var risk = await _riskService.GetByIdForOrgAsync(model.RiskId, orgId, IsSuperAdmin());
            var status = risk?.Status ?? "";
            if (status == "MitigationRequired")
            {
                await _riskService.EnsureMitigationPlanExistsAsync(model.RiskId, orgId, user.Id);
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
        public async Task<IActionResult> UpdateRisk(int RiskId, string Title, string? Category, string? SourceType, string? Priority, string ProjectSite, string? ReportedDate)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Challenge();

            var orgId = IsSuperAdmin() ? null : await GetMyOrgIdAsync();
            var risk = await _riskService.GetByIdForOrgAsync(RiskId, orgId, IsSuperAdmin());
            if (risk == null) return NotFound();

            if (EmployeeOnly() && (risk.ReportByUserId != user.Id || risk.Status != "Draft"))
                return Forbid();

            await _riskService.UpdateRiskAsync(RiskId, orgId, Title, Category, SourceType, null, ProjectSite, IsSuperAdmin());
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
            return RedirectToAction("Identification");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Review(int id)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Challenge();
            if (!IsAdmin() && !IsSuperAdmin()) return Forbid();
            var orgId = IsSuperAdmin() ? null : await GetMyOrgIdAsync();
            var ok = await _riskService.ReviewRiskAsync(id, orgId, user.Id);
            if (!ok) return NotFound();
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
            if (!IsAdmin() && !IsSuperAdmin()) return Forbid();
            var orgId = IsSuperAdmin() ? null : await GetMyOrgIdAsync();
            var ok = await _riskService.ApproveRiskAsync(id, orgId, user.Id);
            if (!ok) return NotFound();
            if (orgId.HasValue)
            {
                await using var db = await _tenantDbFactory.CreateAsync(orgId.Value);
                _riskService.AddAuditLog(db, user.OrganizationId, user.Id, "Risk", id, "RiskApproved", "Status: Reviewed → Approved", HttpContext.Connection.RemoteIpAddress?.ToString());
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
            if (!IsAdmin() && !IsSuperAdmin()) return Forbid();
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
        public async Task<IActionResult> Monitoring()
        {
            ViewData["Title"] = "Risk Monitoring Hub";
            var user = await GetCurrentUserAsync();
            if (user == null) return Challenge();

            var orgId = IsSuperAdmin() ? null : await GetMyOrgIdAsync();
            var activeCount = await _riskService.GetActiveRisksCountAsync(orgId, IsSuperAdmin());
            var highPriority = await _riskService.GetHighPriorityRisksAsync(orgId, IsSuperAdmin(), 10);

            var model = new RiskMonitoringViewModel
            {
                ProjectName = "Sentro Tower - Davao",
                Latitude = 7.0707,
                Longitude = 125.6083,
                Temperature = 31,
                WeatherCondition = "Thunderstorm Warning",
                WindSpeed = 45.5,
                ActiveRisksCount = activeCount,
                HighPriorityRisks = highPriority
            };
            return View(model);
        }
    }
}
