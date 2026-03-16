using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WEB_Sentro.Areas.Client.Models;
using WEB_Sentro.Models.Identity;
using WEB_Sentro.Services;
using Microsoft.AspNetCore.Identity;

namespace WEB_Sentro.Areas.Client.Controllers
{
    [Area("Client")]
    [Authorize(Policy = "ClientReports")]
    public class RiskAnalyticsController : Controller
    {
        private readonly RiskAnalyticsService _analyticsService;
        private readonly RiskAnalyticsPdfService _pdfService;
        private readonly UserManager<ApplicationUser> _userManager;

        public RiskAnalyticsController(RiskAnalyticsService analyticsService, RiskAnalyticsPdfService pdfService, UserManager<ApplicationUser> userManager)
        {
            _analyticsService = analyticsService;
            _pdfService = pdfService;
            _userManager = userManager;
        }

        private async Task<int?> GetMyOrgIdAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            return user?.OrganizationId;
        }

        private bool IsSuperAdmin() => User.IsInRole("SuperAdmin");

        public async Task<IActionResult> Index(int? dateRange, int? siteId, string? category, string? severity, string? source, string? status)
        {
            ViewData["Title"] = "Risk Analytics";
            return View();
        }

        public async Task<IActionResult> IndexContent(int? dateRange, int? siteId, string? category, string? severity, string? source, string? status)
        {
            ViewData["Title"] = "Risk Analytics";
            var orgId = IsSuperAdmin() ? null : await GetMyOrgIdAsync();
            var effectiveOrgId = orgId ?? 1;
            var dateRangeDays = dateRange switch { 7 => 7, 90 => 90, _ => 30 };
            var model = await _analyticsService.GetAnalyticsAsync(
                effectiveOrgId,
                dateRangeDays,
                siteId,
                category,
                severity,
                source,
                status,
                HttpContext.RequestAborted);
            return PartialView("_IndexContent", model);
        }

        [HttpGet]
        public async Task<IActionResult> ExportPdf(int? dateRange, int? siteId, string? category, string? severity, string? source, string? status)
        {
            var orgId = IsSuperAdmin() ? null : await GetMyOrgIdAsync();
            var effectiveOrgId = orgId ?? 1;
            var dateRangeDays = dateRange switch { 7 => 7, 90 => 90, _ => 30 };
            var model = await _analyticsService.GetAnalyticsAsync(
                effectiveOrgId,
                dateRangeDays,
                siteId,
                category,
                severity,
                source,
                status,
                HttpContext.RequestAborted);

            var periodLabel = dateRange switch { 7 => "Last 7 days", 90 => "Last 90 days", _ => "Last 30 days" };
            var siteLabel = siteId.HasValue
                ? (model.Sites?.FirstOrDefault(s => s.Value == siteId.Value.ToString())?.Text ?? "Selected site")
                : "All sites";
            var categoryLabel = !string.IsNullOrEmpty(category)
                ? (model.Categories?.FirstOrDefault(c => c.Value == category)?.Text ?? category)
                : "All categories";

            var scope = new RiskAnalyticsExportScope
            {
                PeriodLabel = periodLabel,
                SiteLabel = siteLabel,
                CategoryLabel = categoryLabel
            };

            var pdfBytes = _pdfService.GeneratePdf(model, scope);
            var fileName = $"Risk_Analytics_Report_{DateTime.UtcNow:yyyy-MM-dd}.pdf";
            return File(pdfBytes, "application/pdf", fileName);
        }
    }
}
