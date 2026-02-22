using Microsoft.EntityFrameworkCore;
using WEB_Sentro.Data;
using WEB_Sentro.Data.Entities;
using Web_Sentro.Areas.Client.Models;

namespace WEB_Sentro.Services
{
    public class RiskEvaluationService
    {
        private readonly ApplicationDbContext _db;
        private readonly RiskService _riskService;

        public RiskEvaluationService(ApplicationDbContext db, RiskService riskService)
        {
            _db = db;
            _riskService = riskService;
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
            var q = _db.Risks.AsNoTracking().Where(r => r.RiskId == riskId);
            if (orgId.HasValue && !superAdmin)
                q = q.Where(r => r.OrgId == orgId.Value);
            var risk = await q.Select(r => new { r.RiskId, r.Title }).FirstOrDefaultAsync(ct);
            if (risk == null) return null;

            var latest = await _db.RiskEvaluations.AsNoTracking()
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

        public async Task<bool> SaveAssessmentAsync(int riskId, int orgId, string userId, int likelihood, int impact, string? remarks, string? ipAddress, bool superAdmin, CancellationToken ct = default)
        {
            var risk = await _db.Risks.FirstOrDefaultAsync(r => r.RiskId == riskId && (superAdmin || r.OrgId == orgId), ct);
            if (risk == null) return false;

            likelihood = Math.Clamp(likelihood, 1, 5);
            impact = Math.Clamp(impact, 1, 5);
            var riskScore = ComputeRiskScore(likelihood, impact);
            var riskLevel = RiskLevelFromScore(riskScore);

            var latest = await _db.RiskEvaluations
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
                _db.RiskEvaluations.Add(new RiskEvaluation
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

            _riskService.AddAuditLog(risk.OrgId, userId, "Risk", riskId, "RiskAssessmentSaved", $"Risk evaluated: {riskLevel} (score {riskScore})", ipAddress);
            await _db.SaveChangesAsync(ct);
            return true;
        }
    }
}
