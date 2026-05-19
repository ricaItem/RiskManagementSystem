using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WEB_Sentro.Data;
using WEB_Sentro.Models.Identity;
using WEB_Sentro.Services;

namespace WEB_Sentro.Controllers.Api
{
    [ApiController]
    [Route("api/controls")]
    [Authorize]
    public class ControlsApiController : ControllerBase
    {
        private readonly ControlService _controlService;
        private readonly ITenantDbFactory _tenantDbFactory;

        public ControlsApiController(ControlService controlService, ITenantDbFactory tenantDbFactory)
        {
            _controlService = controlService;
            _tenantDbFactory = tenantDbFactory;
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
        [ProducesResponseType(typeof(IReadOnlyList<ControlDto>), 200)]
        [ProducesResponseType(403)]
        public async Task<IActionResult> List([FromQuery] int? orgId, [FromQuery] string? search, CancellationToken ct)
        {
            var userOrgId = await GetOrgIdAsync();
            var resolvedOrgId = orgId ?? userOrgId;
            if (!resolvedOrgId.HasValue) return Forbid();
            if (userOrgId.HasValue && orgId.HasValue && orgId.Value != userOrgId.Value) return Forbid();
            var list = await _controlService.GetControlsAsync(resolvedOrgId.Value, search, ct);
            return Ok(list);
        }

        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(ControlDto), 200)]
        [ProducesResponseType(403)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> Get(int id, [FromQuery] int? orgId, CancellationToken ct)
        {
            var userOrgId = await GetOrgIdAsync();
            var resolvedOrgId = orgId ?? userOrgId;
            if (!resolvedOrgId.HasValue) return Forbid();
            if (userOrgId.HasValue && orgId.HasValue && orgId.Value != userOrgId.Value) return Forbid();
            var c = await _controlService.GetByIdAsync(id, resolvedOrgId.Value, ct);
            return c == null ? NotFound() : Ok(c);
        }

        [HttpPost]
        [ProducesResponseType(typeof(ControlDto), 201)]
        [ProducesResponseType(400)]
        [ProducesResponseType(403)]
        public async Task<IActionResult> Create([FromQuery] int? orgId, [FromBody] CreateControlRequest body, CancellationToken ct)
        {
            var userOrgId = await GetOrgIdAsync();
            var resolvedOrgId = orgId ?? userOrgId;
            if (!resolvedOrgId.HasValue) return Forbid();
            if (userOrgId.HasValue && orgId.HasValue && orgId.Value != userOrgId.Value) return Forbid();
            if (string.IsNullOrWhiteSpace(body?.Name)) return BadRequest();
            var c = await _controlService.CreateAsync(resolvedOrgId.Value, body.Name!, body.Description, body.OwnerId, body.Frequency, body.Type, ct);
            return c == null ? BadRequest() : CreatedAtAction(nameof(Get), new { id = c.ControlId }, c);
        }

        [HttpPut("{id:int}")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(403)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> Update(int id, [FromQuery] int? orgId, [FromBody] UpdateControlRequest body, CancellationToken ct)
        {
            var userOrgId = await GetOrgIdAsync();
            var resolvedOrgId = orgId ?? userOrgId;
            if (!resolvedOrgId.HasValue) return Forbid();
            if (userOrgId.HasValue && orgId.HasValue && orgId.Value != userOrgId.Value) return Forbid();
            if (string.IsNullOrWhiteSpace(body?.Name)) return BadRequest();
            var ok = await _controlService.UpdateAsync(id, resolvedOrgId.Value, body.Name!, body.Description, body.OwnerId, body.Frequency, body.Type, body.Status, ct);
            return ok ? Ok() : NotFound();
        }

        [HttpDelete("{id:int}")]
        [ProducesResponseType(204)]
        [ProducesResponseType(403)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> Delete(int id, [FromQuery] int? orgId, CancellationToken ct)
        {
            var userOrgId = await GetOrgIdAsync();
            var resolvedOrgId = orgId ?? userOrgId;
            if (!resolvedOrgId.HasValue) return Forbid();
            if (userOrgId.HasValue && orgId.HasValue && orgId.Value != userOrgId.Value) return Forbid();
            var ok = await _controlService.DeleteAsync(id, resolvedOrgId.Value, ct);
            return ok ? NoContent() : NotFound();
        }
    }

    public class CreateControlRequest
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? OwnerId { get; set; }
        public string? Frequency { get; set; }
        public string? Type { get; set; }
    }

    public class UpdateControlRequest
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? OwnerId { get; set; }
        public string? Frequency { get; set; }
        public string? Type { get; set; }
        public string? Status { get; set; }
    }
}
