using Microsoft.EntityFrameworkCore;
using WEB_Sentro.Data;
using WEB_Sentro.Data.Entities;
using Web_Sentro.Areas.Client.Models;

namespace WEB_Sentro.Services
{
    public class RiskEvaluationService
    {
        private readonly ITenantDbFactory _tenantDbFactory;
        private readonly RiskService _riskService;
        private readonly INotificationService _notificationService;
        private readonly IRiskVersionService _versionService;
        private readonly IRiskMatrixService _matrixService;

        public RiskEvaluationService(ITenantDbFactory tenantDbFactory, RiskService riskService, INotificationService notificationService, IRiskVersionService versionService, IRiskMatrixService matrixService)
        {
            _tenantDbFactory = tenantDbFactory;
            _riskService = riskService;
            _notificationService = notificationService;
            _versionService = versionService;
            _matrixService = matrixService;
        }

        public static int ComputeRiskScore(int likelihood, int impact) => likelihood * impact;

        /// <summary>Centralized priority thresholds: 1-6 Low, 7-14 Medium, 15-19 High, 20-25 Critical.</summary>
        public static string RiskLevelFromScore(int score) => score switch
        {
            >= 20 => "Critical",
            >= 15 => "High",
            >= 7 => "Medium",
            _ => "Low"  // 1-6
        };

        public async Task<RiskAssessmentViewModel?> GetAssessmentViewModelAsync(int riskId, int? orgId, bool superAdmin, CancellationToken ct = default)
        {
            if (!orgId.HasValue)
                return null;

            await using var db = await _tenantDbFactory.CreateAsync(orgId.Value);

            var q = db.Risks.AsNoTracking().Where(r => r.RiskId == riskId);
            if (!superAdmin)
                q = q.Where(r => r.OrgId == orgId.Value);
            var risk = await q.Select(r => new { r.RiskId, r.Title }).FirstOrDefaultAsync(ct);
            if (risk == null) return null;

            var latest = await db.RiskEvaluations.AsNoTracking()
                .Where(e => e.RiskId == riskId)
                .OrderByDescending(e => e.EvaluatedAt)
                .Select(e => new { e.LikelihoodScore, e.ImpactScore })
                .FirstOrDefaultAsync(ct);

            return new RiskAssessmentViewModel
            {
                RiskId = risk.RiskId,
                RiskTitle = risk.Title ?? "",
                Likelihood = latest?.LikelihoodScore ?? 1,
                Impact = latest?.ImpactScore ?? 1
            };
        }

        public async Task<(bool Ok, string? Error)> SaveAssessmentAsync(int riskId, int orgId, string userId, int likelihood, int impact, string? remarks, string? treatmentDecision, string? treatmentJustification, string? ipAddress, bool superAdmin, CancellationToken ct = default)
        {
            await using var db = await _tenantDbFactory.CreateAsync(orgId);

            var risk = await db.Risks.FirstOrDefaultAsync(r => r.RiskId == riskId && (superAdmin || r.OrgId == orgId), ct);
            if (risk == null) return (false, "Risk not found");

            likelihood = Math.Clamp(likelihood, 1, 5);
            impact = Math.Clamp(impact, 1, 5);
            var riskScore = await _matrixService.ComputeScoreAsync(orgId, likelihood, impact, ct);
            var riskLevel = await _matrixService.GetBandForScoreAsync(orgId, riskScore, ct) ?? RiskLevelFromScore(riskScore);

            var decision = treatmentDecision?.Trim();
            if (!string.IsNullOrEmpty(decision))
            {
                var allowed = await _matrixService.GetAllowedDecisionsAsync(orgId, riskScore, ct);
                if (allowed.Count > 0 && !allowed.Any(a => string.Equals(a, decision, StringComparison.OrdinalIgnoreCase)))
                    return (false, $"Treatment decision '{decision}' is not allowed for this risk level. Allowed: {string.Join(", ", allowed)}.");
                if (await _matrixService.RequiresJustificationAsync(orgId, riskScore, decision, ct))
                {
                    if (string.IsNullOrWhiteSpace(treatmentJustification))
                        return (false, "Justification is required for Accept or Transfer for this risk level.");
                }
            }

            var latest = await db.RiskEvaluations
                .Where(e => e.RiskId == riskId)
                .OrderByDescending(e => e.EvaluatedAt)
                .FirstOrDefaultAsync(ct);

            if (latest != null)
            {
                latest.LikelihoodScore = likelihood;
                latest.ImpactScore = impact;
                latest.RiskScore = riskScore;
                latest.RiskLevel = riskLevel;
                latest.EvaluatedByUserId = userId;
                latest.Remarks = remarks;
                latest.EvaluatedAt = DateTime.UtcNow;
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
                    Decision = "None",
                    Remarks = remarks,
                    EvaluatedAt = DateTime.UtcNow
                });
            }

            risk.Priority = riskLevel;
            risk.UpdatedAt = DateTime.UtcNow;
            risk.Status = riskScore >= 15 ? "MitigationRequired" : "Monitoring";
            if (!string.IsNullOrEmpty(decision))
            {
                risk.TreatmentDecision = decision;
                risk.TreatmentJustification = string.IsNullOrWhiteSpace(treatmentJustification) ? null : treatmentJustification.Trim().Length > 500 ? treatmentJustification.Trim().Substring(0, 500) : treatmentJustification.Trim();
                risk.TreatmentSelectedAt = DateTime.UtcNow;
                risk.TreatmentSelectedByUserId = userId;
            }
            var reviewDays = await _matrixService.GetReviewFrequencyDaysAsync(orgId, riskScore, ct);
            if (reviewDays.HasValue && !risk.NextReviewDate.HasValue)
            {
                risk.NextReviewDate = DateTime.UtcNow.Date.AddDays(reviewDays.Value);
                risk.OverdueFlag = false;
            }

            _riskService.AddAuditLog(db, risk.OrgId, userId, "Risk", riskId, "RiskAssessmentSaved", $"Risk evaluated: {riskLevel} (score {riskScore})", ipAddress);
            await db.SaveChangesAsync(ct);
            await _versionService.SaveVersionAsync(riskId, risk.OrgId, userId, $"Assessment saved: {riskLevel} (score {riskScore})", ct);
            if (riskLevel == "High" || riskLevel == "Critical")
                await _notificationService.NotifyRiskEventAsync(risk.OrgId, "HighCriticalAssessed", riskId, "High/Critical risk assessed", $"Risk '{risk.Title}' assessed as {riskLevel} (score {riskScore}).", risk.ReportByUserId, ct);
            return (true, null);
        }
    }
}
