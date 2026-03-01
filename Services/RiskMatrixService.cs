using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using WEB_Sentro.Data;
using WEB_Sentro.Data.Entities;

namespace WEB_Sentro.Services
{
    public class RiskMatrixService : IRiskMatrixService
    {
        private readonly ITenantDbFactory _tenantDbFactory;
        private static readonly ConcurrentDictionary<int, (RiskMatrixConfigDto Config, DateTime CachedAt)> _cache = new();
        private static readonly TimeSpan CacheExpiry = TimeSpan.FromMinutes(10);

        public RiskMatrixService(ITenantDbFactory tenantDbFactory)
        {
            _tenantDbFactory = tenantDbFactory;
        }

        public async Task<RiskMatrixConfigDto?> GetActiveConfigAsync(int orgId, CancellationToken ct = default)
        {
            if (_cache.TryGetValue(orgId, out var entry) && DateTime.UtcNow - entry.CachedAt < CacheExpiry)
                return entry.Config;

            await using var db = await _tenantDbFactory.CreateAsync(orgId);
            var config = await db.RiskMatrixConfigs.AsNoTracking()
                .Where(c => c.OrgId == orgId && c.IsActive)
                .OrderByDescending(c => c.CreatedAt)
                .Include(c => c.Cells)
                .Include(c => c.AppetiteBands)
                .Include(c => c.TreatmentTriggers)
                .FirstOrDefaultAsync(ct);
            if (config == null)
            {
                await EnsureDefaultMatrixAsync(orgId, ct);
                return await GetActiveConfigAsync(orgId, ct);
            }

            var dto = MapToDto(config);
            _cache.TryAdd(orgId, (dto, DateTime.UtcNow));
            return dto;
        }

        public async Task<int> ComputeScoreAsync(int orgId, int likelihood, int impact, CancellationToken ct = default)
        {
            var config = await GetActiveConfigAsync(orgId, ct);
            if (config?.Cells.Count > 0)
            {
                var cell = config.Cells.FirstOrDefault(c => c.Likelihood == likelihood && c.Impact == impact);
                if (cell != null) return cell.Score;
            }
            return likelihood * impact;
        }

        public async Task<string?> GetBandForScoreAsync(int orgId, int score, CancellationToken ct = default)
        {
            var config = await GetActiveConfigAsync(orgId, ct);
            if (config?.AppetiteBands.Count > 0)
            {
                var band = config.AppetiteBands.FirstOrDefault(b => score >= b.MinScore && score <= b.MaxScore);
                return band?.BandName;
            }
            return score >= 20 ? "Critical" : score >= 15 ? "High" : score >= 7 ? "Medium" : "Low";
        }

        public async Task<int?> GetReviewFrequencyDaysAsync(int orgId, int score, CancellationToken ct = default)
        {
            var config = await GetActiveConfigAsync(orgId, ct);
            if (config?.AppetiteBands.Count > 0)
            {
                var band = config.AppetiteBands.FirstOrDefault(b => score >= b.MinScore && score <= b.MaxScore);
                return band?.ReviewFrequencyDays;
            }
            return score >= 20 ? 30 : score >= 15 ? 90 : score >= 7 ? 180 : 365;
        }

        public async Task<IReadOnlyList<string>> GetAllowedDecisionsAsync(int orgId, int score, CancellationToken ct = default)
        {
            var config = await GetActiveConfigAsync(orgId, ct);
            if (config?.TreatmentTriggers.Count > 0)
            {
                var bandName = await GetBandForScoreAsync(orgId, score, ct);
                var trigger = config.TreatmentTriggers.FirstOrDefault(t =>
                    (t.BandName != null && t.BandName == bandName) ||
                    (t.MinScore.HasValue && t.MaxScore.HasValue && score >= t.MinScore.Value && score <= t.MaxScore.Value));
                if (trigger?.AllowedDecisions.Count > 0) return trigger.AllowedDecisions;
            }
            return new[] { "Mitigate", "Transfer", "Accept", "Avoid" };
        }

        public async Task<bool> RequiresJustificationAsync(int orgId, int score, string decision, CancellationToken ct = default)
        {
            var config = await GetActiveConfigAsync(orgId, ct);
            if (config?.TreatmentTriggers.Count > 0)
            {
                var bandName = await GetBandForScoreAsync(orgId, score, ct);
                var trigger = config.TreatmentTriggers.FirstOrDefault(t =>
                    (t.BandName != null && t.BandName == bandName) ||
                    (t.MinScore.HasValue && t.MaxScore.HasValue && score >= t.MinScore.Value && score <= t.MaxScore.Value));
                if (trigger != null && trigger.RequiresJustification)
                {
                    var d = decision?.Trim();
                    if (string.Equals(d, "Accept", StringComparison.OrdinalIgnoreCase) || string.Equals(d, "Transfer", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            return false;
        }

        public void InvalidateCache(int orgId) => _cache.TryRemove(orgId, out _);

        public async Task EnsureDefaultMatrixAsync(int orgId, CancellationToken ct = default)
        {
            await using var db = await _tenantDbFactory.CreateAsync(orgId);
            if (await db.RiskMatrixConfigs.AnyAsync(c => c.OrgId == orgId, ct)) return;

            var now = DateTime.UtcNow;
            var config = new RiskMatrixConfig
            {
                OrgId = orgId,
                Name = "Default 5×5",
                IsActive = true,
                CreatedAt = now
            };
            db.RiskMatrixConfigs.Add(config);
            await db.SaveChangesAsync(ct);

            for (var l = 1; l <= 5; l++)
                for (var i = 1; i <= 5; i++)
                    db.RiskMatrixCells.Add(new RiskMatrixCell { RiskMatrixConfigId = config.RiskMatrixConfigId, Likelihood = l, Impact = i, Score = l * i });

            var bands = new[] { (1, 6, "Low", 365), (7, 14, "Medium", 180), (15, 19, "High", 90), (20, 25, "Critical", 30) };
            foreach (var (min, max, name, freq) in bands)
                db.RiskAppetiteBands.Add(new RiskAppetiteBand { RiskMatrixConfigId = config.RiskMatrixConfigId, MinScore = min, MaxScore = max, BandName = name, ReviewFrequencyDays = freq });

            foreach (var bandName in new[] { "High", "Critical" })
                db.RiskTreatmentTriggers.Add(new RiskTreatmentTrigger
                {
                    RiskMatrixConfigId = config.RiskMatrixConfigId,
                    BandName = bandName,
                    AllowedDecisions = "Mitigate,Transfer,Accept,Avoid",
                    RequiresJustification = true,
                    RequiresApproval = false
                });
            await db.SaveChangesAsync(ct);
            InvalidateCache(orgId);
        }

        private static RiskMatrixConfigDto MapToDto(RiskMatrixConfig c)
        {
            return new RiskMatrixConfigDto
            {
                RiskMatrixConfigId = c.RiskMatrixConfigId,
                OrgId = c.OrgId,
                Name = c.Name ?? "",
                Cells = c.Cells.Select(x => new RiskMatrixCellDto { Likelihood = x.Likelihood, Impact = x.Impact, Score = x.Score }).ToList(),
                AppetiteBands = c.AppetiteBands.Select(x => new RiskAppetiteBandDto { MinScore = x.MinScore, MaxScore = x.MaxScore, BandName = x.BandName ?? "", ReviewFrequencyDays = x.ReviewFrequencyDays }).ToList(),
                TreatmentTriggers = c.TreatmentTriggers.Select(x => new RiskTreatmentTriggerDto
                {
                    BandName = x.BandName,
                    MinScore = x.MinScore,
                    MaxScore = x.MaxScore,
                    AllowedDecisions = (x.AllowedDecisions ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
                    RequiresJustification = x.RequiresJustification,
                    RequiresApproval = x.RequiresApproval
                }).ToList()
            };
        }
    }
}
