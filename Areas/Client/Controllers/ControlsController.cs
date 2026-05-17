using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WEB_Sentro.Services;
using WEB_Sentro.Models.Identity;

namespace Web_Sentro.Areas.Client.Controllers
{
    [Area("Client")]
    [Authorize(Policy = "MainAdminOnly")]
    public class ControlsController : Controller
    {
        private readonly ControlService _controlService;

        public ControlsController(ControlService controlService)
        {
            _controlService = controlService;
        }

        private async Task<int?> GetOrgIdAsync()
        {
            var userId = User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return null;
            if (User?.IsInRole("SuperAdmin") == true) return null;
            var userManager = HttpContext.RequestServices.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>>();
            var user = await userManager.FindByIdAsync(userId);
            return user?.OrganizationId;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? search, CancellationToken ct)
        {
            var orgId = await GetOrgIdAsync();
            if (!orgId.HasValue) return Forbid();
            var list = await _controlService.GetControlsAsync(orgId.Value, search, ct);
            return View(list);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string Name, string? Description, string? Frequency, string? Type, CancellationToken ct)
        {
            var orgId = await GetOrgIdAsync();
            if (!orgId.HasValue) return Forbid();
            if (string.IsNullOrWhiteSpace(Name)) { TempData["ToastError"] = "Name is required."; return RedirectToAction(nameof(Index)); }
            await _controlService.CreateAsync(orgId.Value, Name.Trim(), Description?.Trim(), null, Frequency?.Trim(), Type?.Trim(), ct);
            TempData["ToastSuccess"] = "Control created.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, CancellationToken ct)
        {
            var orgId = await GetOrgIdAsync();
            if (!orgId.HasValue) return Forbid();
            var ok = await _controlService.DeleteAsync(id, orgId.Value, ct);
            if (ok) TempData["ToastSuccess"] = "Control removed."; else TempData["ToastError"] = "Control not found.";
            return RedirectToAction(nameof(Index));
        }
    }
}
