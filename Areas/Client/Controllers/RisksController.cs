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
        private readonly UserManager<ApplicationUser> _userManager;

        public RisksController(RiskService riskService, RiskEvaluationService evaluationService, RiskAttachmentService attachmentService, UserManager<ApplicationUser> userManager)
        {
            _riskService = riskService;
            _evaluationService = evaluationService;
            _attachmentService = attachmentService;
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

        public async Task<IActionResult> Identification(string? search, string? status, string? category)
        {
            ViewData["Title"] = "Risk Identification";
            var user = await GetCurrentUserAsync();
            if (user == null) return Challenge();

            var orgId = IsSuperAdmin() ? null : await GetMyOrgIdAsync();
            var employeeOnly = EmployeeOnly();
            var list = await _riskService.GetRisksForListAsync(orgId, user.Id, employeeOnly, search, status, category);
            ViewBag.CurrentUserId = user.Id;
            ViewBag.IsEmployeeOnly = employeeOnly;
            return View(list);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> IdentifyNewRisk([Bind("Title,Category,ProjectSite,Description,SourceType")] RiskIdentificationViewModel model, string? submitType)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Challenge();

            if (string.IsNullOrWhiteSpace(model?.Title))
                return RedirectToAction("Identification");

            var status = (submitType == "Submit") ? "Submitted" : "Draft";
            var risk = await _riskService.CreateRiskAsync(
                user.OrganizationId,
                user.Id,
                model.Title.Trim(),
                model.Category,
                model.SourceType,
                model.ProjectSite?.Trim(),
                model.Description?.Trim(),
                status);

            var auditAction = status == "Submitted" ? "RiskCreatedSubmitted" : "RiskCreatedDraft";
            _riskService.AddAuditLog(user.OrganizationId, user.Id, "Risk", risk.RiskId, auditAction, $"Risk created: {model.Title}", HttpContext.Connection.RemoteIpAddress?.ToString());
            await _riskService.SaveChangesAsync();

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

            _riskService.AddAuditLog(user.OrganizationId, user.Id, "Risk", RiskId, "RiskSubmitted", "Risk submitted", HttpContext.Connection.RemoteIpAddress?.ToString());
            await _riskService.SaveChangesAsync();
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
        public async Task<IActionResult> UpdateRisk(int RiskId, string Title, string? Category, string? SourceType, string Priority, string ProjectSite, string? ReportedDate)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Challenge();

            var orgId = IsSuperAdmin() ? null : await GetMyOrgIdAsync();
            var risk = await _riskService.GetByIdForOrgAsync(RiskId, orgId, IsSuperAdmin());
            if (risk == null) return NotFound();

            if (EmployeeOnly() && (risk.ReportByUserId != user.Id || risk.Status != "Draft"))
                return Forbid();

            await _riskService.UpdateRiskAsync(RiskId, orgId, Title, Category, SourceType, Priority, ProjectSite, IsSuperAdmin());
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
