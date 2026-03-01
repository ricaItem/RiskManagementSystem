using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WEB_Sentro.Services;
using Web_Sentro.Areas.Client.Models;

namespace Web_Sentro.Areas.Client.Controllers
{
    [Area("Client")]
    [Authorize]
    public class RiskMatrixController : Controller
    {
        private readonly IRiskMatrixService _matrixService;

        public RiskMatrixController(IRiskMatrixService matrixService)
        {
            _matrixService = matrixService;
        }

        private async Task<int?> GetOrgIdAsync()
        {
            var userId = User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return null;
            if (User?.IsInRole("SuperAdmin") == true) return null;
            var userManager = HttpContext.RequestServices.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<WEB_Sentro.Models.Identity.ApplicationUser>>();
            var user = await userManager.FindByIdAsync(userId);
            return user?.OrganizationId;
        }

        [HttpGet]
        public async Task<IActionResult> Index(CancellationToken ct)
        {
            var orgId = await GetOrgIdAsync();
            if (!orgId.HasValue) return Forbid();
            var config = await _matrixService.GetActiveConfigAsync(orgId.Value, ct);
            if (config == null) return View("Index", new RiskMatrixViewModel { OrgId = orgId.Value });
            var vm = new RiskMatrixViewModel
            {
                OrgId = orgId.Value,
                ConfigId = config.RiskMatrixConfigId,
                Name = config.Name,
                Cells = config.Cells.Select(c => new RiskMatrixCellVm { Likelihood = c.Likelihood, Impact = c.Impact, Score = c.Score }).ToList(),
                Bands = config.AppetiteBands.Select(b => new RiskAppetiteBandVm { MinScore = b.MinScore, MaxScore = b.MaxScore, BandName = b.BandName, ReviewFrequencyDays = b.ReviewFrequencyDays }).ToList(),
                Triggers = config.TreatmentTriggers.Select(t => new RiskTreatmentTriggerVm { BandName = t.BandName, RequiresJustification = t.RequiresJustification, AllowedDecisions = t.AllowedDecisions.ToList() }).ToList()
            };
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EnsureDefault(CancellationToken ct)
        {
            var orgId = await GetOrgIdAsync();
            if (!orgId.HasValue) return Forbid();
            if (!User.IsInRole("Admin") && !User.IsInRole("SuperAdmin") && !User.IsInRole("RiskManager")) return Forbid();
            await _matrixService.EnsureDefaultMatrixAsync(orgId.Value, ct);
            TempData["MatrixMessage"] = "Default risk matrix created.";
            return RedirectToAction(nameof(Index));
        }
    }
}
