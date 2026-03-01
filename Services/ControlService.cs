using Microsoft.EntityFrameworkCore;
using WEB_Sentro.Data;
using WEB_Sentro.Data.Entities;

namespace WEB_Sentro.Services
{
    public class ControlService
    {
        private readonly ITenantDbFactory _tenantDbFactory;

        public ControlService(ITenantDbFactory tenantDbFactory)
        {
            _tenantDbFactory = tenantDbFactory;
        }

        public async Task<List<ControlDto>> GetControlsAsync(int orgId, string? search, CancellationToken ct = default)
        {
            await using var db = await _tenantDbFactory.CreateAsync(orgId);
            var q = db.Controls.AsNoTracking().Where(c => c.OrgId == orgId);
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                q = q.Where(c => c.Name.Contains(term) || (c.Description != null && c.Description.Contains(term)));
            }
            return await q.OrderBy(c => c.Name)
                .Select(c => new ControlDto { ControlId = c.ControlId, OrgId = c.OrgId, Name = c.Name, Description = c.Description, OwnerId = c.OwnerId, Frequency = c.Frequency, Type = c.Type, Status = c.Status })
                .ToListAsync(ct);
        }

        public async Task<ControlDto?> GetByIdAsync(int controlId, int orgId, CancellationToken ct = default)
        {
            await using var db = await _tenantDbFactory.CreateAsync(orgId);
            var c = await db.Controls.AsNoTracking().FirstOrDefaultAsync(x => x.ControlId == controlId && x.OrgId == orgId, ct);
            return c == null ? null : new ControlDto { ControlId = c.ControlId, OrgId = c.OrgId, Name = c.Name ?? "", Description = c.Description, OwnerId = c.OwnerId, Frequency = c.Frequency, Type = c.Type, Status = c.Status ?? "Active" };
        }

        public async Task<ControlDto?> CreateAsync(int orgId, string name, string? description, string? ownerId, string? frequency, string? type, CancellationToken ct = default)
        {
            await using var db = await _tenantDbFactory.CreateAsync(orgId);
            var c = new Control { OrgId = orgId, Name = name.Trim(), Description = description?.Trim(), OwnerId = ownerId, Frequency = frequency?.Trim(), Type = type?.Trim(), Status = "Active", CreatedAt = DateTime.UtcNow };
            db.Controls.Add(c);
            await db.SaveChangesAsync(ct);
            return new ControlDto { ControlId = c.ControlId, OrgId = c.OrgId, Name = c.Name, Description = c.Description, OwnerId = c.OwnerId, Frequency = c.Frequency, Type = c.Type, Status = c.Status };
        }

        public async Task<bool> UpdateAsync(int controlId, int orgId, string name, string? description, string? ownerId, string? frequency, string? type, string? status, CancellationToken ct = default)
        {
            await using var db = await _tenantDbFactory.CreateAsync(orgId);
            var c = await db.Controls.FirstOrDefaultAsync(x => x.ControlId == controlId && x.OrgId == orgId, ct);
            if (c == null) return false;
            c.Name = name.Trim();
            c.Description = description?.Trim();
            c.OwnerId = ownerId;
            c.Frequency = frequency?.Trim();
            c.Type = type?.Trim();
            if (!string.IsNullOrWhiteSpace(status)) c.Status = status.Trim();
            c.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return true;
        }

        public async Task<bool> DeleteAsync(int controlId, int orgId, CancellationToken ct = default)
        {
            await using var db = await _tenantDbFactory.CreateAsync(orgId);
            var c = await db.Controls.FirstOrDefaultAsync(x => x.ControlId == controlId && x.OrgId == orgId, ct);
            if (c == null) return false;
            db.Controls.Remove(c);
            await db.SaveChangesAsync(ct);
            return true;
        }

        public async Task<IReadOnlyList<RiskControlDto>> GetLinkedControlsForRiskAsync(int riskId, int orgId, CancellationToken ct = default)
        {
            await using var db = await _tenantDbFactory.CreateAsync(orgId);
            var exists = await db.Risks.AsNoTracking().AnyAsync(r => r.RiskId == riskId && r.OrgId == orgId, ct);
            if (!exists) return Array.Empty<RiskControlDto>();
            return await db.RiskControls.AsNoTracking()
                .Where(rc => rc.RiskId == riskId)
                .Include(rc => rc.Control)
                .OrderBy(rc => rc.Control!.Name)
                .Select(rc => new RiskControlDto { RiskControlId = rc.RiskControlId, RiskId = rc.RiskId, ControlId = rc.ControlId, ControlName = rc.Control!.Name, Notes = rc.Notes, LinkedAt = rc.LinkedAt })
                .ToListAsync(ct);
        }

        public async Task<bool> LinkControlToRiskAsync(int riskId, int controlId, int orgId, string? notes, CancellationToken ct = default)
        {
            await using var db = await _tenantDbFactory.CreateAsync(orgId);
            if (!await db.Risks.AnyAsync(r => r.RiskId == riskId && r.OrgId == orgId, ct)) return false;
            if (!await db.Controls.AnyAsync(c => c.ControlId == controlId && c.OrgId == orgId, ct)) return false;
            if (await db.RiskControls.AnyAsync(rc => rc.RiskId == riskId && rc.ControlId == controlId, ct)) return false;
            db.RiskControls.Add(new RiskControl { RiskId = riskId, ControlId = controlId, Notes = notes?.Trim(), LinkedAt = DateTime.UtcNow });
            await db.SaveChangesAsync(ct);
            return true;
        }

        public async Task<bool> UnlinkControlFromRiskAsync(int riskControlId, int orgId, CancellationToken ct = default)
        {
            await using var db = await _tenantDbFactory.CreateAsync(orgId);
            var rc = await db.RiskControls.FirstOrDefaultAsync(x => x.RiskControlId == riskControlId, ct);
            if (rc == null) return false;
            var risk = await db.Risks.AsNoTracking().FirstOrDefaultAsync(r => r.RiskId == rc.RiskId && r.OrgId == orgId, ct);
            if (risk == null) return false;
            db.RiskControls.Remove(rc);
            await db.SaveChangesAsync(ct);
            return true;
        }
    }

    public class ControlDto
    {
        public int ControlId { get; set; }
        public int OrgId { get; set; }
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public string? OwnerId { get; set; }
        public string? Frequency { get; set; }
        public string? Type { get; set; }
        public string Status { get; set; } = "Active";
    }

    public class RiskControlDto
    {
        public int RiskControlId { get; set; }
        public int RiskId { get; set; }
        public int ControlId { get; set; }
        public string ControlName { get; set; } = "";
        public string? Notes { get; set; }
        public DateTime LinkedAt { get; set; }
    }
}
