using Microsoft.EntityFrameworkCore;
using WEB_Sentro.Data;
using WEB_Sentro.Data.Entities;
using Web_Sentro.Areas.Client.Models;

namespace WEB_Sentro.Services
{
    public class RiskService
    {
        private readonly ApplicationDbContext _db;

        public RiskService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<List<RiskIdentificationViewModel>> GetRisksForListAsync(
            int? orgId,
            string? userId,
            bool employeeOnly,
            string? search,
            string? status,
            string? category,
            CancellationToken ct = default)
        {
            var q = _db.Risks.AsNoTracking().AsQueryable();

            if (orgId.HasValue)
                q = q.Where(r => r.OrgId == orgId.Value);

            if (employeeOnly && !string.IsNullOrEmpty(userId))
                q = q.Where(r => r.ReportByUserId == userId);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                q = q.Where(r => r.Title.Contains(term) || (r.ProjectSite != null && r.ProjectSite.Contains(term)) || (r.Category != null && r.Category.Contains(term)));
            }

            if (!string.IsNullOrWhiteSpace(status))
                q = q.Where(r => r.Status == status);
            if (!string.IsNullOrWhiteSpace(category))
                q = q.Where(r => r.Category == category);

            var list = await q
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new { r.RiskId, r.Title, r.Category, r.Priority, r.ProjectSite, r.ReportByUserId, r.CreatedAt, r.Status, r.SourceType })
                .ToListAsync(ct);

            var riskIds = list.Select(x => x.RiskId).ToList();
            var attachments = await _db.Attachments.AsNoTracking()
                .Where(a => riskIds.Contains(a.RiskId))
                .Select(a => new { a.RiskId, a.FileRef })
                .ToListAsync(ct);
            var attByRisk = attachments.Where(a => a.FileRef != null).GroupBy(a => a.RiskId).ToDictionary(g => g.Key, g => g.Select(x => x.FileRef!).ToList());

            var userIds = list.Select(x => x.ReportByUserId).Distinct().ToList();
            var users = await _db.Users
                .AsNoTracking()
                .Where(u => userIds.Contains(u.Id))
                .Select(u => new { u.Id, u.FirstName, u.LastName })
                .ToListAsync(ct);
            var userNames = users.ToDictionary(u => u.Id, u => $"{u.FirstName} {u.LastName}".Trim());

            return list.Select(r => new RiskIdentificationViewModel
            {
                Id = r.RiskId,
                Title = r.Title ?? "",
                Category = r.Category ?? "No Category",
                Priority = r.Priority ?? "Medium",
                DetectedBy = userNames.TryGetValue(r.ReportByUserId, out var name) ? name : "Unknown",
                ReportedBy = userNames.TryGetValue(r.ReportByUserId, out var rb) ? rb : "",
                ReportByUserId = r.ReportByUserId,
                ProjectSite = r.ProjectSite ?? "",
                DateLogged = r.CreatedAt,
                DateReported = r.CreatedAt,
                Status = r.Status ?? "Draft",
                SourceType = r.SourceType,
                AttachmentsCount = attByRisk.GetValueOrDefault(r.RiskId)?.Count ?? 0,
                Attachments = attByRisk.GetValueOrDefault(r.RiskId) ?? new List<string>()
            }).ToList();
        }

        public async Task<Risk?> GetByIdForOrgAsync(int riskId, int? orgId, bool superAdmin, CancellationToken ct = default)
        {
            var q = _db.Risks.AsQueryable();
            if (orgId.HasValue && !superAdmin)
                q = q.Where(r => r.OrgId == orgId.Value);
            return await q.Include(r => r.Evaluations.OrderByDescending(e => e.EvaluatedAt).Take(1))
                .FirstOrDefaultAsync(r => r.RiskId == riskId, ct);
        }

        public async Task<Risk> CreateRiskAsync(int orgId, string reportByUserId, string title, string? category, string? sourceType, string? projectSite, string? description, string status = "Draft", CancellationToken ct = default)
        {
            var risk = new Risk
            {
                OrgId = orgId,
                ReportByUserId = reportByUserId,
                Title = title,
                Category = category ?? "No Category",
                SourceType = sourceType,
                ProjectSite = projectSite,
                Description = description,
                Status = status == "Submitted" ? "Submitted" : "Draft",
                Priority = "Medium",
                CreatedAt = DateTime.UtcNow
            };
            _db.Risks.Add(risk);
            await _db.SaveChangesAsync(ct);
            return risk;
        }

        public async Task<bool> SubmitRiskAsync(int riskId, int? orgId, string userId, bool employeeOnly, CancellationToken ct = default)
        {
            var q = _db.Risks.Where(r => r.RiskId == riskId);
            if (orgId.HasValue)
                q = q.Where(r => r.OrgId == orgId.Value);
            var risk = await q.FirstOrDefaultAsync(ct);
            if (risk == null || risk.Status != "Draft") return false;
            if (employeeOnly && risk.ReportByUserId != userId) return false;
            risk.Status = "Submitted";
            risk.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            return true;
        }

        public async Task<int> GetActiveRisksCountAsync(int? orgId, bool superAdmin, CancellationToken ct = default)
        {
            var q = _db.Risks.AsNoTracking().Where(r => r.DeletedAt == null);
            if (orgId.HasValue && !superAdmin)
                q = q.Where(r => r.OrgId == orgId.Value);
            return await q.CountAsync(ct);
        }

        public async Task<List<RiskIdentificationViewModel>> GetHighPriorityRisksAsync(int? orgId, bool superAdmin, int top = 10, CancellationToken ct = default)
        {
            var q = _db.Risks.AsNoTracking().Where(r => r.DeletedAt == null);
            if (orgId.HasValue && !superAdmin)
                q = q.Where(r => r.OrgId == orgId.Value);

            var list = await q
                .OrderByDescending(r => r.Priority == "Critical" ? 3 : r.Priority == "High" ? 2 : r.Priority == "Medium" ? 1 : 0)
                .ThenByDescending(r => r.CreatedAt)
                .Take(top)
                .Select(r => new { r.RiskId, r.Title, r.Category, r.Priority, r.ProjectSite, r.ReportByUserId })
                .ToListAsync(ct);

            var userIds = list.Select(x => x.ReportByUserId).Distinct().ToList();
            var users = await _db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id))
                .Select(u => new { u.Id, u.FirstName, u.LastName }).ToListAsync(ct);
            var userNames = users.ToDictionary(u => u.Id, u => $"{u.FirstName} {u.LastName}".Trim());

            return list.Select(r => new RiskIdentificationViewModel
            {
                Id = r.RiskId,
                Title = r.Title ?? "",
                Category = r.Category ?? "No Category",
                Priority = r.Priority ?? "Medium",
                DetectedBy = userNames.TryGetValue(r.ReportByUserId, out var name) ? name : "Unknown",
                ProjectSite = r.ProjectSite ?? ""
            }).ToList();
        }

        public async Task UpdateRiskAsync(int riskId, int? orgId, string? title, string? category, string? sourceType, string? priority, string? projectSite, bool superAdmin, CancellationToken ct = default)
        {
            var q = _db.Risks.Where(r => r.RiskId == riskId);
            if (orgId.HasValue && !superAdmin)
                q = q.Where(r => r.OrgId == orgId.Value);
            var risk = await q.FirstOrDefaultAsync(ct);
            if (risk == null) return;
            if (title != null) risk.Title = title;
            if (category != null) risk.Category = category;
            if (sourceType != null) risk.SourceType = sourceType;
            if (priority != null) risk.Priority = priority;
            if (projectSite != null) risk.ProjectSite = projectSite;
            risk.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        public async Task SoftDeleteAsync(int riskId, int? orgId, bool superAdmin, CancellationToken ct = default)
        {
            var q = _db.Risks.Where(r => r.RiskId == riskId);
            if (orgId.HasValue && !superAdmin)
                q = q.Where(r => r.OrgId == orgId.Value);
            var risk = await q.FirstOrDefaultAsync(ct);
            if (risk == null) return;
            risk.DeletedAt = DateTime.UtcNow;
            risk.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        public async Task RestoreAsync(int riskId, int? orgId, bool superAdmin, CancellationToken ct = default)
        {
            var q = _db.Risks.IgnoreQueryFilters().Where(r => r.RiskId == riskId);
            if (orgId.HasValue && !superAdmin)
                q = q.Where(r => r.OrgId == orgId.Value);
            var risk = await q.FirstOrDefaultAsync(ct);
            if (risk == null) return;
            risk.DeletedAt = null;
            risk.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        public void AddAuditLog(int orgId, string userId, string entityType, int entityId, string actionType, string? message, string? ipAddress)
        {
            _db.AuditLogs.Add(new AuditLog
            {
                OrgId = orgId,
                UserId = userId,
                EntityType = entityType,
                EntityId = entityId,
                ActionType = actionType,
                Level = "Info",
                Message = message,
                IpAddress = ipAddress,
                CreatedAt = DateTime.UtcNow
            });
        }

        public async Task SaveChangesAsync(CancellationToken ct = default) => await _db.SaveChangesAsync(ct);
    }
}
