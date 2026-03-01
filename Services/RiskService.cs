using Microsoft.EntityFrameworkCore;
using WEB_Sentro.Data;
using WEB_Sentro.Data.Entities;
using Web_Sentro.Areas.Client.Models;

namespace WEB_Sentro.Services
{
    public class RiskService
    {
        private readonly ITenantDbFactory _tenantDbFactory;
        private readonly PlatformDbContext _platformDb;

        public RiskService(ITenantDbFactory tenantDbFactory, PlatformDbContext platformDb)
        {
            _tenantDbFactory = tenantDbFactory;
            _platformDb = platformDb;
        }

        public async Task<List<RiskIdentificationViewModel>> GetRisksForListAsync(
            int? orgId,
            string? userId,
            bool employeeOnly,
            string? search,
            string? status,
            string? category,
            int? siteId = null,
            bool showDeleted = false,
            CancellationToken ct = default)
        {
            if (!orgId.HasValue)
                return new List<RiskIdentificationViewModel>();

            await using var db = await _tenantDbFactory.CreateAsync(orgId.Value);

            var q = db.Risks.AsNoTracking().AsQueryable();
            if (showDeleted)
                q = q.IgnoreQueryFilters();

            if (orgId.HasValue)
                q = q.Where(r => r.OrgId == orgId.Value);

            q = q.Where(r => r.Status != "Draft" || r.ReportByUserId == userId);

            if (employeeOnly && !string.IsNullOrEmpty(userId))
                q = q.Where(r => r.ReportByUserId == userId);

            if (!showDeleted)
                q = q.Where(r => r.DeletedAt == null);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                q = q.Where(r => r.Title.Contains(term) || (r.ProjectSite != null && r.ProjectSite.Contains(term)) || (r.Category != null && r.Category.Contains(term)));
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                var s = status.Trim();
                q = s switch
                {
                    "For_Review" => q.Where(r => r.Status == "For_Review" || r.Status == "Submitted" || r.Status == "Reviewed"),
                    "Rejected" => q.Where(r => r.Status == "Rejected"),
                    "Closed_Invalid" => q.Where(r => r.Status == "Closed_Invalid"),
                    "Monitoring" => q.Where(r => r.Status == "Monitoring" || r.Status == "Approved"),
                    "Submitted" => q.Where(r => r.Status == "For_Review" || r.Status == "Submitted" || r.Status == "Reviewed"),
                    _ => q.Where(r => r.Status == s)
                };
            }
            if (!string.IsNullOrWhiteSpace(category))
                q = q.Where(r => r.Category == category);
            if (siteId.HasValue)
                q = q.Where(r => r.SiteId == siteId.Value);

            var list = await q
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new { r.RiskId, r.Title, r.Category, r.Priority, r.ProjectSite, r.ReportByUserId, r.CreatedAt, r.Status, r.SourceType, r.DeletedAt, r.OrgId, r.SiteId })
                .ToListAsync(ct);

            var siteIds = list.Where(x => x.SiteId.HasValue).Select(x => x.SiteId!.Value).Distinct().ToList();
            var siteNames = siteIds.Count > 0
                ? await db.Sites.AsNoTracking().Where(s => siteIds.Contains(s.SiteId)).Select(s => new { s.SiteId, s.SiteName }).ToDictionaryAsync(x => x.SiteId, x => x.SiteName, ct)
                : new Dictionary<int, string>();

            var riskIds = list.Select(x => x.RiskId).ToList();
            var attachments = await db.Attachments.AsNoTracking()
                .Where(a => riskIds.Contains(a.RiskId))
                .Select(a => new { a.RiskId, a.FileRef })
                .ToListAsync(ct);
            var attByRisk = attachments.Where(a => a.FileRef != null).GroupBy(a => a.RiskId).ToDictionary(g => g.Key, g => g.Select(x => x.FileRef!).ToList());

            var userIds = list.Select(x => x.ReportByUserId).Distinct().ToList();
            var users = await _platformDb.Users
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
                Priority = r.Priority ?? "Unassessed",
                DetectedBy = userNames.TryGetValue(r.ReportByUserId, out var name) ? name : "Unknown",
                ReportedBy = userNames.TryGetValue(r.ReportByUserId, out var rb) ? rb : "",
                ReportByUserId = r.ReportByUserId,
                ProjectSite = r.ProjectSite ?? "",
                SiteId = r.SiteId,
                SiteName = r.SiteId.HasValue && siteNames.TryGetValue(r.SiteId.Value, out var sn) ? sn : null,
                DateLogged = r.CreatedAt,
                DateReported = r.CreatedAt,
                Status = r.Status ?? "Draft",
                SourceType = r.SourceType,
                OrgId = r.OrgId,
                DeletedAt = r.DeletedAt,
                AttachmentsCount = attByRisk.GetValueOrDefault(r.RiskId)?.Count ?? 0,
                Attachments = attByRisk.GetValueOrDefault(r.RiskId) ?? new List<string>()
            }).ToList();
        }

        public async Task<Risk?> GetByIdForOrgAsync(int riskId, int? orgId, bool superAdmin, CancellationToken ct = default)
        {
            if (!orgId.HasValue)
                return null;

            await using var db = await _tenantDbFactory.CreateAsync(orgId.Value);

            var q = db.Risks.AsQueryable();
            if (orgId.HasValue && !superAdmin)
                q = q.Where(r => r.OrgId == orgId.Value);
            return await q.Include(r => r.Evaluations.OrderByDescending(e => e.EvaluatedAt).Take(1))
                .FirstOrDefaultAsync(r => r.RiskId == riskId, ct);
        }

        public async Task<Risk> CreateRiskAsync(int orgId, string reportByUserId, string title, string? category, string? sourceType, string? projectSite, string? description, string status = "Draft", int? siteId = null, CancellationToken ct = default)
        {
            await using var db = await _tenantDbFactory.CreateAsync(orgId);

            var risk = new Risk
            {
                OrgId = orgId,
                ReportByUserId = reportByUserId,
                Title = title,
                Category = category ?? "No Category",
                SourceType = sourceType,
                ProjectSite = projectSite,
                Description = description,
                Status = status == "For_Review" ? "For_Review" : "Draft",
                Priority = "Unassessed",
                SiteId = siteId,
                CreatedAt = DateTime.UtcNow
            };
            db.Risks.Add(risk);
            await db.SaveChangesAsync(ct);
            return risk;
        }

        public async Task<bool> SubmitRiskAsync(int riskId, int? orgId, string userId, bool employeeOnly, CancellationToken ct = default)
        {
            if (!orgId.HasValue)
                return false;

            await using var db = await _tenantDbFactory.CreateAsync(orgId.Value);

            var q = db.Risks.Where(r => r.RiskId == riskId);
            q = q.Where(r => r.OrgId == orgId.Value);
            var risk = await q.FirstOrDefaultAsync(ct);
            if (risk == null || risk.Status != "Draft") return false;
            if (employeeOnly && risk.ReportByUserId != userId) return false;
            risk.Status = "For_Review";
            risk.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return true;
        }

        public async Task<int> GetActiveRisksCountAsync(int? orgId, bool superAdmin, CancellationToken ct = default)
        {
            if (!orgId.HasValue)
                return 0;

            await using var db = await _tenantDbFactory.CreateAsync(orgId.Value);

            var q = db.Risks.AsNoTracking().Where(r => r.DeletedAt == null);
            if (!superAdmin)
                q = q.Where(r => r.OrgId == orgId.Value);
            return await q.CountAsync(ct);
        }

        public async Task<int> GetOpenCriticalRisksCountForSiteAsync(int orgId, int monitoringSiteId, CancellationToken ct = default)
        {
            await using var db = await _tenantDbFactory.CreateAsync(orgId);
            return await db.Risks.AsNoTracking()
                .Where(r => r.OrgId == orgId && r.LocationId == monitoringSiteId && r.DeletedAt == null
                    && (r.Priority == "Critical" || r.Priority == "High")
                    && r.Status != "Closed_Invalid" && r.Status != "Rejected")
                .CountAsync(ct);
        }

        public async Task<int> GetOverdueMitigationTasksCountForSiteAsync(int orgId, int? monitoringSiteId, CancellationToken ct = default)
        {
            if (!monitoringSiteId.HasValue) return 0;
            await using var db = await _tenantDbFactory.CreateAsync(orgId);
            var today = DateTime.UtcNow.Date;
            return await db.MitigationTasks.AsNoTracking()
                .Where(t => t.Plan != null && t.Plan.Risk != null
                    && t.Plan.Risk.OrgId == orgId && t.Plan.Risk.LocationId == monitoringSiteId
                    && t.Plan.Risk.DeletedAt == null
                    && t.DueDate.HasValue && t.DueDate.Value < today && t.Status != "Done")
                .CountAsync(ct);
        }

        public async Task<List<RiskIdentificationViewModel>> GetHighPriorityRisksAsync(int? orgId, bool superAdmin, int top = 10, CancellationToken ct = default)
        {
            if (!orgId.HasValue)
                return new List<RiskIdentificationViewModel>();

            await using var db = await _tenantDbFactory.CreateAsync(orgId.Value);

            var q = db.Risks.AsNoTracking().Where(r => r.DeletedAt == null);
            if (!superAdmin)
                q = q.Where(r => r.OrgId == orgId.Value);

            var list = await q
                .OrderByDescending(r => r.Priority == "Critical" ? 3 : r.Priority == "High" ? 2 : r.Priority == "Medium" ? 1 : 0)
                .ThenByDescending(r => r.CreatedAt)
                .Take(top)
                .Select(r => new { r.RiskId, r.Title, r.Category, r.Priority, r.ProjectSite, r.ReportByUserId, r.SourceType, r.CreatedAt, r.SiteId })
                .ToListAsync(ct);

            var siteIds = list.Where(x => x.SiteId.HasValue).Select(x => x.SiteId!.Value).Distinct().ToList();
            var siteNames = siteIds.Count > 0
                ? await db.Sites.AsNoTracking().Where(s => siteIds.Contains(s.SiteId)).Select(s => new { s.SiteId, s.SiteName }).ToDictionaryAsync(x => x.SiteId, x => x.SiteName, ct)
                : new Dictionary<int, string>();

            var userIds = list.Select(x => x.ReportByUserId).Distinct().ToList();
            var users = await _platformDb.Users.AsNoTracking().Where(u => userIds.Contains(u.Id))
                .Select(u => new { u.Id, u.FirstName, u.LastName }).ToListAsync(ct);
            var userNames = users.ToDictionary(u => u.Id, u => $"{u.FirstName} {u.LastName}".Trim());

            return list.Select(r => new RiskIdentificationViewModel
            {
                Id = r.RiskId,
                Title = r.Title ?? "",
                Category = r.Category ?? "No Category",
                Priority = r.Priority ?? "Medium",
                DetectedBy = userNames.TryGetValue(r.ReportByUserId, out var name) ? name : "Unknown",
                ProjectSite = r.ProjectSite ?? "",
                SiteId = r.SiteId,
                SiteName = r.SiteId.HasValue && siteNames.TryGetValue(r.SiteId.Value, out var sn) ? sn : null,
                SourceType = r.SourceType,
                DateLogged = r.CreatedAt
            }).ToList();
        }

        public async Task<bool> HasOpenRiskForSiteRuleAsync(int orgId, int monitoringSiteId, string ruleCode, int withinHours = 6, CancellationToken ct = default)
        {
            await using var db = await _tenantDbFactory.CreateAsync(orgId);
            var since = DateTime.UtcNow.AddHours(-withinHours);
            return await db.Risks.AsNoTracking()
                .Where(r => r.OrgId == orgId && r.LocationId == monitoringSiteId && r.Category == ruleCode && r.SourceType == "WeatherAPI"
                    && r.DeletedAt == null && r.Status != "Closed_Invalid" && r.Status != "Rejected" && r.CreatedAt >= since)
                .AnyAsync(ct);
        }

        /// <summary>Returns the RiskId of an existing open risk for the same OrgId+SiteId+MonitoringRuleCode within the last withinHours, or null.</summary>
        public async Task<int?> GetExistingOpenRiskIdForSiteRuleAsync(int orgId, int monitoringSiteId, string ruleCode, int withinHours = 12, CancellationToken ct = default)
        {
            await using var db = await _tenantDbFactory.CreateAsync(orgId);
            var since = DateTime.UtcNow.AddHours(-withinHours);
            return await db.Risks.AsNoTracking()
                .Where(r => r.OrgId == orgId && r.LocationId == monitoringSiteId && r.MonitoringRuleCode == ruleCode && r.SourceType == "WeatherAPI"
                    && r.DeletedAt == null && r.Status != "Closed_Invalid" && r.Status != "Rejected" && r.CreatedAt >= since)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => (int?)r.RiskId)
                .FirstOrDefaultAsync(ct);
        }

        public async Task<Risk?> CreateRiskFromMonitoringAsync(int orgId, int monitoringSiteId, string ruleCode, string title, string priority, string reportByUserId, string? siteName, string? descriptionWithMeasuredValues, int? projectSiteId = null, CancellationToken ct = default)
        {
            await using var db = await _tenantDbFactory.CreateAsync(orgId);
            var isHighOrCritical = string.Equals(priority, "Critical", StringComparison.OrdinalIgnoreCase) || string.Equals(priority, "High", StringComparison.OrdinalIgnoreCase);
            var risk = new Risk
            {
                OrgId = orgId,
                LocationId = monitoringSiteId,
                SiteId = projectSiteId,
                ReportByUserId = reportByUserId,
                Title = title,
                Category = "Weather",
                MonitoringRuleCode = ruleCode,
                SourceType = "WeatherAPI",
                ProjectSite = siteName,
                Description = descriptionWithMeasuredValues ?? $"Auto-created from monitoring rule: {ruleCode}",
                Status = isHighOrCritical ? "MitigationRequired" : "Monitoring",
                Priority = priority,
                CreatedAt = DateTime.UtcNow
            };
            db.Risks.Add(risk);
            await db.SaveChangesAsync(ct);
            await EnsureAutoRiskEvaluationAsync(db, risk.RiskId, orgId, priority, descriptionWithMeasuredValues, reportByUserId, ct);
            await db.SaveChangesAsync(ct);
            return risk;
        }

        /// <summary>Creates or updates a single RiskEvaluation for an AUTO risk from monitoring (Likelihood/Impact from severity).</summary>
        public async Task EnsureAutoRiskEvaluationAsync(TenantDbContext db, int riskId, int orgId, string severity, string? measuredValuesRemarks, string userId, CancellationToken ct = default)
        {
            var (likelihood, impact) = severity switch
            {
                _ when string.Equals(severity, "Critical", StringComparison.OrdinalIgnoreCase) => (5, 5),
                _ when string.Equals(severity, "High", StringComparison.OrdinalIgnoreCase) => (4, 4),
                _ when string.Equals(severity, "Medium", StringComparison.OrdinalIgnoreCase) => (3, 3),
                _ => (2, 2)
            };
            var riskScore = RiskEvaluationService.ComputeRiskScore(likelihood, impact);
            var riskLevel = RiskEvaluationService.RiskLevelFromScore(riskScore);
            var r = measuredValuesRemarks ?? "";
            var remarks = r.Length > 255 ? r.Substring(0, 255) : r;

            var existing = await db.RiskEvaluations.Where(e => e.RiskId == riskId).OrderByDescending(e => e.EvaluatedAt).FirstOrDefaultAsync(ct);
            if (existing != null)
            {
                existing.LikelihoodScore = likelihood;
                existing.ImpactScore = impact;
                existing.RiskScore = riskScore;
                existing.RiskLevel = riskLevel;
                existing.EvaluatedByUserId = userId;
                existing.Remarks = remarks;
                existing.EvaluatedAt = DateTime.UtcNow;
            }
            else
            {
                db.RiskEvaluations.Add(new RiskEvaluation
                {
                    RiskId = riskId,
                    EvaluatedByUserId = userId,
                    LikelihoodScore = likelihood,
                    ImpactScore = impact,
                    RiskScore = riskScore,
                    RiskLevel = riskLevel,
                    Decision = "Auto",
                    Remarks = remarks,
                    EvaluatedAt = DateTime.UtcNow
                });
            }
            var risk = await db.Risks.FirstOrDefaultAsync(r => r.RiskId == riskId && r.OrgId == orgId, ct);
            if (risk != null)
            {
                risk.Priority = riskLevel;
                risk.UpdatedAt = DateTime.UtcNow;
            }
        }

        /// <summary>Ensures an AUTO risk has a RiskEvaluation (creates or updates). Call when risk already exists (e.g. from Create Plan or sync update).</summary>
        public async Task EnsureAutoRiskEvaluationForRiskAsync(int riskId, int orgId, string severity, string? measuredValuesRemarks, string userId, CancellationToken ct = default)
        {
            await using var db = await _tenantDbFactory.CreateAsync(orgId);
            await EnsureAutoRiskEvaluationAsync(db, riskId, orgId, severity, measuredValuesRemarks, userId, ct);
            await db.SaveChangesAsync(ct);
        }

        public async Task UpdateRiskAsync(int riskId, int? orgId, string? title, string? category, string? sourceType, string? priority, string? projectSite, int? siteId, bool superAdmin, CancellationToken ct = default)
        {
            if (!orgId.HasValue)
                return;

            await using var db = await _tenantDbFactory.CreateAsync(orgId.Value);

            var q = db.Risks.Where(r => r.RiskId == riskId);
            if (!superAdmin)
                q = q.Where(r => r.OrgId == orgId.Value);
            var risk = await q.FirstOrDefaultAsync(ct);
            if (risk == null) return;
            if (title != null) risk.Title = title;
            if (category != null) risk.Category = category;
            if (sourceType != null) risk.SourceType = sourceType;
            if (priority != null) risk.Priority = priority;
            if (projectSite != null) risk.ProjectSite = projectSite;
            risk.SiteId = siteId;
            risk.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        public async Task SoftDeleteAsync(int riskId, int? orgId, bool superAdmin, CancellationToken ct = default)
        {
            if (!orgId.HasValue)
                return;

            await using var db = await _tenantDbFactory.CreateAsync(orgId.Value);

            var q = db.Risks.Where(r => r.RiskId == riskId);
            if (!superAdmin)
                q = q.Where(r => r.OrgId == orgId.Value);
            var risk = await q.FirstOrDefaultAsync(ct);
            if (risk == null) return;
            risk.DeletedAt = DateTime.UtcNow;
            risk.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        public async Task RestoreAsync(int riskId, int? orgId, bool superAdmin, CancellationToken ct = default)
        {
            if (!orgId.HasValue)
                return;

            await using var db = await _tenantDbFactory.CreateAsync(orgId.Value);

            var q = db.Risks.IgnoreQueryFilters().Where(r => r.RiskId == riskId);
            if (!superAdmin)
                q = q.Where(r => r.OrgId == orgId.Value);
            var risk = await q.FirstOrDefaultAsync(ct);
            if (risk == null) return;
            risk.DeletedAt = null;
            risk.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        public async Task<bool> HardDeleteAsync(int riskId, int? orgId, string userId, bool superAdmin, bool allowOnlyDraft, CancellationToken ct = default)
        {
            if (!orgId.HasValue)
                return false;

            await using var db = await _tenantDbFactory.CreateAsync(orgId.Value);

            var q = db.Risks.IgnoreQueryFilters().Where(r => r.RiskId == riskId);
            if (!superAdmin)
                q = q.Where(r => r.OrgId == orgId.Value);
            var risk = await q.Include(r => r.Evaluations).FirstOrDefaultAsync(ct);
            if (risk == null) return false;
            if (allowOnlyDraft && risk.Status != "Draft") return false;
            db.RiskEvaluations.RemoveRange(risk.Evaluations);
            db.Risks.Remove(risk);
            await db.SaveChangesAsync(ct);
            return true;
        }

        public async Task<bool> ReviewRiskAsync(int riskId, int? orgId, string userId, CancellationToken ct = default)
        {
            if (!orgId.HasValue)
                return false;

            await using var db = await _tenantDbFactory.CreateAsync(orgId.Value);

            var q = db.Risks.Where(r => r.RiskId == riskId);
            q = q.Where(r => r.OrgId == orgId.Value);
            var risk = await q.FirstOrDefaultAsync(ct);
            if (risk == null || risk.Status != "Submitted") return false;
            risk.Status = "Reviewed";
            risk.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return true;
        }

        public async Task<bool> ApproveRiskAsync(int riskId, int? orgId, string userId, CancellationToken ct = default)
        {
            if (!orgId.HasValue)
                return false;

            await using var db = await _tenantDbFactory.CreateAsync(orgId.Value);

            var q = db.Risks.Where(r => r.RiskId == riskId);
            q = q.Where(r => r.OrgId == orgId.Value);
            var risk = await q.FirstOrDefaultAsync(ct);
            if (risk == null || risk.Status != "Reviewed") return false;
            risk.Status = "Approved";
            risk.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return true;
        }

        public async Task<bool> RejectRiskAsync(int riskId, int? orgId, string userId, string? remarks, CancellationToken ct = default)
        {
            if (!orgId.HasValue)
                return false;

            await using var db = await _tenantDbFactory.CreateAsync(orgId.Value);

            var q = db.Risks.Where(r => r.RiskId == riskId);
            q = q.Where(r => r.OrgId == orgId.Value);
            var risk = await q.FirstOrDefaultAsync(ct);
            if (risk == null) return false;
            if (risk.Status != "For_Review" && risk.Status != "Submitted" && risk.Status != "Reviewed") return false;
            risk.Status = "Rejected";
            risk.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return true;
        }

        public async Task EnsureMitigationPlanExistsAsync(int riskId, int orgId, string userId, string? severity = null, CancellationToken ct = default)
        {
            await using var db = await _tenantDbFactory.CreateAsync(orgId);

            var risk = await db.Risks
                .Include(r => r.MitigationPlan)
                .FirstOrDefaultAsync(r => r.RiskId == riskId && r.OrgId == orgId, ct);
            if (risk == null) return;

            var plan = risk.MitigationPlan;
            if (plan == null)
            {
                plan = new MitigationPlan
                {
                    RiskId = riskId,
                    CreatedByUserId = userId,
                    Status = "Active",
                    CreatedAt = DateTime.UtcNow
                };
                db.MitigationPlans.Add(plan);
                await db.SaveChangesAsync(ct);
            }

            var hasTasks = await db.MitigationTasks.AnyAsync(t => t.PlanId == plan.PlanId, ct);
            if (hasTasks) return;

            var titles = GetDefaultTaskTitlesForRisk(risk.Category, risk.SourceType, severity);
            var now = DateTime.UtcNow;
            foreach (var title in titles)
            {
                db.MitigationTasks.Add(new MitigationTask
                {
                    PlanId = plan.PlanId,
                    Title = title,
                    Status = "ToDo",
                    ProgressPercent = 0,
                    UpdatedAt = now
                });
            }
            await db.SaveChangesAsync(ct);
        }

        private static string[] GetDefaultTaskTitlesForRisk(string? category, string? sourceType, string? severity = null)
        {
            var cat = (category ?? "").Trim();
            var src = (sourceType ?? "").Trim();
            var sev = (severity ?? "").Trim();
            if (cat.Contains("Weather", StringComparison.OrdinalIgnoreCase) || src.Equals("WeatherAPI", StringComparison.OrdinalIgnoreCase) || src.Equals("Weather", StringComparison.OrdinalIgnoreCase))
            {
                if (sev.Equals("Critical", StringComparison.OrdinalIgnoreCase))
                    return new[] { "Assign safety officer and due date", "Suspend crane operations if wind exceeds threshold", "Secure loose materials and barricade area", "Communicate toolbox talk: high wind protocol" };
                return new[] { "Suspend crane operations", "Secure loose materials", "Conduct toolbox talk" };
            }
            if (cat.Contains("Supplier", StringComparison.OrdinalIgnoreCase) || src.Equals("SupplierAPI", StringComparison.OrdinalIgnoreCase) || src.Equals("Supplier", StringComparison.OrdinalIgnoreCase))
                return new[] { "Contact supplier", "Arrange backup supplier", "Update procurement schedule" };
            return new[] { "Investigate the risk", "Assign owner and due date", "Implement control measures", "Verify controls are effective" };
        }

        public void AddAuditLog(TenantDbContext db, int orgId, string userId, string entityType, int entityId, string actionType, string? message, string? ipAddress)
        {
            db.AuditLogs.Add(new AuditLog
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

        public async Task SaveChangesAsync(TenantDbContext db, CancellationToken ct = default) => await db.SaveChangesAsync(ct);
    }
}
