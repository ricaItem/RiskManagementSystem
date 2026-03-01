using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WEB_Sentro.Data;
using WEB_Sentro.Data.Entities;
using WEB_Sentro.Models.Identity;

namespace WEB_Sentro.Services
{
    public class RiskVersionService : IRiskVersionService
    {
        private readonly ITenantDbFactory _tenantDbFactory;
        private readonly PlatformDbContext _platformDb;

        public RiskVersionService(ITenantDbFactory tenantDbFactory, PlatformDbContext platformDb)
        {
            _tenantDbFactory = tenantDbFactory;
            _platformDb = platformDb;
        }

        public async Task SaveVersionAsync(int riskId, int orgId, string? changedByUserId, string changeSummary, CancellationToken ct = default)
        {
            await using var db = await _tenantDbFactory.CreateAsync(orgId);
            var risk = await db.Risks.AsNoTracking()
                .Include(r => r.Evaluations.OrderByDescending(e => e.EvaluatedAt).Take(1))
                .FirstOrDefaultAsync(r => r.RiskId == riskId && r.OrgId == orgId, ct);
            if (risk == null) return;

            var snapshot = new
            {
                Risk = new
                {
                    risk.RiskId,
                    risk.OrgId,
                    risk.Title,
                    risk.Description,
                    risk.Category,
                    risk.SourceType,
                    risk.Status,
                    risk.Priority,
                    risk.ReportByUserId,
                    risk.RiskOwnerId,
                    risk.AccountableId,
                    risk.TreatmentDecision,
                    risk.TreatmentJustification,
                    risk.NextReviewDate,
                    risk.LastReviewedAt,
                    risk.OverdueFlag,
                    risk.CreatedAt,
                    risk.UpdatedAt
                },
                LatestEvaluation = risk.Evaluations.FirstOrDefault() == null ? null : new
                {
                    risk.Evaluations.First().LikelihoodScore,
                    risk.Evaluations.First().ImpactScore,
                    risk.Evaluations.First().RiskScore,
                    risk.Evaluations.First().RiskLevel,
                    risk.Evaluations.First().EvaluatedAt
                }
            };
            var snapshotJson = JsonSerializer.Serialize(snapshot);

            var nextVersion = await db.RiskVersions
                .Where(v => v.RiskId == riskId)
                .MaxAsync(v => (int?)v.VersionNo, ct) ?? 0;
            nextVersion++;

            db.RiskVersions.Add(new RiskVersion
            {
                RiskId = riskId,
                VersionNo = nextVersion,
                ChangedAt = DateTime.UtcNow,
                ChangedByUserId = changedByUserId,
                SnapshotJson = snapshotJson,
                ChangeSummary = changeSummary.Length > 500 ? changeSummary.Substring(0, 500) : changeSummary
            });
            await db.SaveChangesAsync(ct);
        }

        public async Task<IReadOnlyList<RiskVersionDto>> GetVersionsAsync(int riskId, int orgId, CancellationToken ct = default)
        {
            await using var db = await _tenantDbFactory.CreateAsync(orgId);
            var exists = await db.Risks.AsNoTracking().AnyAsync(r => r.RiskId == riskId && r.OrgId == orgId, ct);
            if (!exists) return Array.Empty<RiskVersionDto>();

            var list = await db.RiskVersions.AsNoTracking()
                .Where(v => v.RiskId == riskId)
                .OrderByDescending(v => v.VersionNo)
                .Select(v => new RiskVersionDto
                {
                    RiskVersionId = v.RiskVersionId,
                    RiskId = v.RiskId,
                    VersionNo = v.VersionNo,
                    ChangedAt = v.ChangedAt,
                    ChangedByUserId = v.ChangedByUserId,
                    ChangeSummary = v.ChangeSummary,
                    SnapshotJson = v.SnapshotJson
                })
                .ToListAsync(ct);

            var userIds = list.Where(x => !string.IsNullOrEmpty(x.ChangedByUserId)).Select(x => x.ChangedByUserId!).Distinct().ToList();
            if (userIds.Count > 0)
            {
                var users = await _platformDb.Users.AsNoTracking()
                    .Where(u => userIds.Contains(u.Id))
                    .Select(u => new { u.Id, u.FirstName, u.LastName })
                    .ToListAsync(ct);
                var names = users.ToDictionary(u => u.Id, u => $"{u.FirstName} {u.LastName}".Trim());
                foreach (var dto in list)
                    dto.ChangedByDisplayName = dto.ChangedByUserId != null && names.TryGetValue(dto.ChangedByUserId, out var n) ? n : dto.ChangedByUserId;
            }

            return list;
        }
    }
}
