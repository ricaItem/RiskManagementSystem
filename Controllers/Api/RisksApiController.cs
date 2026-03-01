using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WEB_Sentro.Data;
using WEB_Sentro.Models.Identity;
using WEB_Sentro.Services;

namespace WEB_Sentro.Controllers.Api
{
    public class LinkControlRequest
    {
        public int ControlId { get; set; }
        public string? Notes { get; set; }
    }

    [ApiController]
    [Route("api/risks")]
    [Authorize]
    public class RisksApiController : ControllerBase
    {
        private readonly IRiskVersionService _versionService;
        private readonly ITenantDbFactory _tenantDbFactory;

        private readonly ControlService _controlService;

        public RisksApiController(IRiskVersionService versionService, ITenantDbFactory tenantDbFactory, ControlService controlService)
        {
            _versionService = versionService;
            _tenantDbFactory = tenantDbFactory;
            _controlService = controlService;
        }

        /// <summary>GET /api/risks/{id}/versions — returns version history for the risk (org-scoped). SuperAdmin may pass ?orgId=.</summary>
        [HttpGet("{id:int}/versions")]
        [ProducesResponseType(typeof(IReadOnlyList<RiskVersionDto>), 200)]
        [ProducesResponseType(403)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetVersions(int id, [FromQuery] int? orgId, CancellationToken ct)
        {
            var userOrgId = await GetOrgIdAsync();
            var resolvedOrgId = orgId ?? userOrgId;
            if (!resolvedOrgId.HasValue) return Forbid();
            if (userOrgId.HasValue && orgId.HasValue && orgId.Value != userOrgId.Value) return Forbid();
            var orgIdValue = resolvedOrgId.Value;
            var list = await _versionService.GetVersionsAsync(id, orgIdValue, ct);
            if (list.Count == 0)
            {
                await using var db = await _tenantDbFactory.CreateAsync(orgIdValue);
                var exists = await db.Risks.AsNoTracking().AnyAsync(r => r.RiskId == id && r.OrgId == orgIdValue, ct);
                if (!exists) return NotFound();
            }
            return Ok(list);
        }

        [HttpGet("{id:int}/controls")]
        [ProducesResponseType(typeof(IReadOnlyList<RiskControlDto>), 200)]
        [ProducesResponseType(403)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetRiskControls(int id, [FromQuery] int? orgId, CancellationToken ct)
        {
            var userOrgId = await GetOrgIdAsync();
            var resolvedOrgId = orgId ?? userOrgId;
            if (!resolvedOrgId.HasValue) return Forbid();
            if (userOrgId.HasValue && orgId.HasValue && orgId.Value != userOrgId.Value) return Forbid();
            var list = await _controlService.GetLinkedControlsForRiskAsync(id, resolvedOrgId.Value, ct);
            return Ok(list);
        }

        [HttpPost("{id:int}/controls")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(403)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> LinkControl(int id, [FromQuery] int? orgId, [FromBody] LinkControlRequest body, CancellationToken ct)
        {
            var userOrgId = await GetOrgIdAsync();
            var resolvedOrgId = orgId ?? userOrgId;
            if (!resolvedOrgId.HasValue) return Forbid();
            if (userOrgId.HasValue && orgId.HasValue && orgId.Value != userOrgId.Value) return Forbid();
            var ok = await _controlService.LinkControlToRiskAsync(id, body.ControlId, resolvedOrgId.Value, body.Notes, ct);
            return ok ? Ok() : NotFound();
        }

        [HttpDelete("{id:int}/controls/{riskControlId:int}")]
        [ProducesResponseType(204)]
        [ProducesResponseType(403)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> UnlinkControl(int id, int riskControlId, [FromQuery] int? orgId, CancellationToken ct)
        {
            var userOrgId = await GetOrgIdAsync();
            var resolvedOrgId = orgId ?? userOrgId;
            if (!resolvedOrgId.HasValue) return Forbid();
            if (userOrgId.HasValue && orgId.HasValue && orgId.Value != userOrgId.Value) return Forbid();
            var ok = await _controlService.UnlinkControlFromRiskAsync(riskControlId, resolvedOrgId.Value, ct);
            return ok ? NoContent() : NotFound();
        }

        private async Task<int?> GetOrgIdAsync()
        {
            var userId = User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return null;
            if (User?.IsInRole("SuperAdmin") == true) return null; // can query any org; for simplicity we require org for versions
            var userManager = HttpContext.RequestServices.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>>();
            var user = await userManager.FindByIdAsync(userId);
            return user?.OrganizationId;
        }
    }
}
