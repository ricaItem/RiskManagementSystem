using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WEB_Sentro.Areas.Client.Models;
using WEB_Sentro.Models.Identity;
using WEB_Sentro.Services;
using Microsoft.AspNetCore.Identity;

namespace WEB_Sentro.Areas.Client.Controllers
{
    [Area("Client")]
    [Authorize]
    public class RiskAnalyticsController : Controller
    {
        private readonly RiskAnalyticsService _analyticsService;
        private readonly UserManager<ApplicationUser> _userManager;

        public RiskAnalyticsController(RiskAnalyticsService analyticsService, UserManager<ApplicationUser> userManager)
        {
            _analyticsService = analyticsService;
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
            return View(model);
        }
    }
}
