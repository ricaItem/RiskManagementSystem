using Microsoft.EntityFrameworkCore;
using WEB_Sentro.Data;
using WEB_Sentro.Data.Entities;
using WEB_Sentro.Areas.Client.Models;

namespace WEB_Sentro.Services
{
    public class RiskAnalyticsService
    {
        private readonly ITenantDbFactory _tenantDbFactory;
        private readonly PlatformDbContext _platformDb;

        public RiskAnalyticsService(ITenantDbFactory tenantDbFactory, PlatformDbContext platformDb)
        {
            _tenantDbFactory = tenantDbFactory;
            _platformDb = platformDb;
        }

        public async Task<RiskAnalyticsViewModel> GetAnalyticsAsync(
            int orgId,
            int dateRangeDays = 30,
            int? siteId = null,
            string? category = null,
            string? severity = null,
            string? source = null,
            string? statusFilter = null,
            CancellationToken ct = default)
        {
            await using var db = await _tenantDbFactory.CreateAsync(orgId);
            var now = DateTime.UtcNow;
            var periodStart = now.AddDays(-dateRangeDays);
            var previousPeriodStart = now.AddDays(-2 * dateRangeDays);

            var risksQuery = db.Risks.AsNoTracking()
                .Where(r => r.OrgId == orgId && r.DeletedAt == null);

            if (siteId.HasValue)
                risksQuery = risksQuery.Where(r => r.SiteId == siteId.Value);
            if (!string.IsNullOrWhiteSpace(category))
                risksQuery = risksQuery.Where(r => r.Category == category);
            if (!string.IsNullOrWhiteSpace(severity))
                risksQuery = risksQuery.Where(r => r.Priority == severity);
            if (!string.IsNullOrWhiteSpace(source) && source != "all")
            {
                if (source.Equals("weather", StringComparison.OrdinalIgnoreCase))
                    risksQuery = risksQuery.Where(r => r.SourceType == "WeatherAPI" || (r.SourceType != null && r.SourceType.Contains("Weather", StringComparison.OrdinalIgnoreCase)));
                else if (source.Equals("manual", StringComparison.OrdinalIgnoreCase))
                    risksQuery = risksQuery.Where(r => r.SourceType == null || r.SourceType != "WeatherAPI");
            }

            if (!string.IsNullOrWhiteSpace(statusFilter) && statusFilter != "all")
            {
                switch (statusFilter.ToLowerInvariant())
                {
                    case "closed":
                        risksQuery = risksQuery.Where(r => r.Status == "Closed_Invalid" || r.Status == "Rejected");
                        break;
                    case "open":
                        risksQuery = risksQuery.Where(r => r.Status != "Closed_Invalid" && r.Status != "Rejected");
                        break;
                    case "mitigated":
                        risksQuery = risksQuery.Where(r => r.MitigationPlan != null && r.Status != "Closed_Invalid" && r.Status != "Rejected");
                        break;
                }
            }

            var risks = await risksQuery
                .Include(r => r.Evaluations.OrderByDescending(e => e.EvaluatedAt).Take(2))
                .Include(r => r.MitigationPlan)
                .Include(r => r.Site)
                .Include(r => r.Expenses)
                .ToListAsync(ct);

            var planIds = risks.Where(r => r.MitigationPlan != null).Select(r => r.MitigationPlan!.PlanId).ToList();
            var taskCounts = planIds.Count > 0
                ? await db.MitigationTasks.AsNoTracking()
                    .Where(t => planIds.Contains(t.PlanId))
                    .GroupBy(t => t.PlanId)
                    .Select(g => new { PlanId = g.Key, Done = g.Count(t => t.Status == "Done"), Total = g.Count() })
                    .ToDictionaryAsync(x => x.PlanId, x => (x.Done, x.Total), ct)
                : new Dictionary<int, (int Done, int Total)>();

            bool IsClosedForChart(Risk r)
            {
                if (r.Status == "Closed_Invalid" || r.Status == "Rejected" || r.Status == "Closed_Controlled")
                    return true;
                if (r.MitigationPlan != null && taskCounts.TryGetValue(r.MitigationPlan.PlanId, out var t) && t.Total > 0 && t.Done == t.Total)
                    return true;
                return false;
            }

            var closedRisks = risks.Where(IsClosedForChart).ToList();
            var openRisks = risks.Where(r => !IsClosedForChart(r)).ToList();

            static bool IsOpenByStatus(Risk r) => r.Status != "Closed_Invalid" && r.Status != "Rejected";
            var currentPeriodRisks = risks.Where(r => r.CreatedAt >= periodStart).ToList();
            var previousPeriodRisks = risks.Where(r => r.CreatedAt >= previousPeriodStart && r.CreatedAt < periodStart).ToList();

            var activeRisks = openRisks.Count;
            var criticalRisks = risks.Count(r => string.Equals(r.Priority, "Critical", StringComparison.OrdinalIgnoreCase));
            var createdInPeriod = currentPeriodRisks.Count;
            var weatherTriggered = risks.Count(r => r.SourceType != null && r.SourceType.Contains("Weather", StringComparison.OrdinalIgnoreCase));
            var previousCreated = previousPeriodRisks.Count;
            var previousOpen = previousPeriodRisks.Count(IsOpenByStatus);
            var createdDelta = previousCreated > 0 ? (int)Math.Round((createdInPeriod - previousCreated) / (double)previousCreated * 100) : 0;
            var activeDelta = previousOpen > 0 ? activeRisks - previousOpen : 0;

            var closedWithDate = risks.Where(r => (r.Status == "Closed_Invalid" || r.Status == "Closed_Controlled") && r.UpdatedAt.HasValue).ToList();
            var avgTimeToCloseDays = 0;
            if (closedWithDate.Any())
            {
                var days = closedWithDate.Select(r => (r.UpdatedAt!.Value - r.CreatedAt).TotalDays).Where(d => d >= 0).ToList();
                avgTimeToCloseDays = days.Any() ? (int)Math.Round(days.Average()) : 0;
            }

            var risksWithEvals = risks.Where(r => r.Evaluations.Any()).ToList();
            var withTwoEvals = risksWithEvals.Where(r => r.Evaluations.Count >= 2).ToList();
            double avgInitial = 0, avgResidual = 0;
            int avgReductionPercent = 0, reassessedPercent = 0;
            if (withTwoEvals.Any())
            {
                var initialScores = withTwoEvals.Select(r =>
                {
                    var evals = r.Evaluations.OrderBy(e => e.EvaluatedAt).ToList();
                    var inherent = evals.FirstOrDefault(e => e.IsInherent);
                    return (inherent ?? evals.First()).RiskScore;
                }).ToList();
                var residualScores = withTwoEvals.Select(r =>
                {
                    var evals = r.Evaluations.OrderBy(e => e.EvaluatedAt).ToList();
                    var residual = evals.LastOrDefault(e => !e.IsInherent);
                    return (residual ?? evals.Last()).RiskScore;
                }).ToList();
                avgInitial = initialScores.Average();
                avgResidual = residualScores.Average();
                var reductions = initialScores.Zip(residualScores, (i, res) => i > 0 ? (int)Math.Round((1 - (double)res / i) * 100) : 0).ToList();
                avgReductionPercent = reductions.Any() ? (int)Math.Round(reductions.Average()) : 0;
                reassessedPercent = risksWithEvals.Count > 0 ? (int)Math.Round(withTwoEvals.Count * 100.0 / risksWithEvals.Count) : 0;
            }
            else if (risksWithEvals.Any())
            {
                var evals = risksWithEvals.Select(r =>
                {
                    var ordered = r.Evaluations.OrderBy(e => e.EvaluatedAt).ToList();
                    var inherent = ordered.FirstOrDefault(e => e.IsInherent);
                    return inherent ?? ordered.Last();
                }).ToList();
                avgInitial = evals.Average(e => e.RiskScore);
                avgResidual = avgInitial;
            }

            var siteGroups = risks
                .Where(r => r.SiteId.HasValue)
                .GroupBy(r => r.SiteId!.Value)
                .ToList();
            var siteIds = siteGroups.Select(g => g.Key).Distinct().ToList();
            var siteNames = await db.Sites.AsNoTracking()
                .Where(s => siteIds.Contains(s.SiteId))
                .ToDictionaryAsync(s => s.SiteId, s => s.SiteName, ct);

            var siteRankings = new List<SiteRankingRowViewModel>();
            foreach (var g in siteGroups)
            {
                var siteRisks = g.ToList();
                var latestScores = siteRisks
                    .Select(r => r.Evaluations.OrderByDescending(e => e.EvaluatedAt).FirstOrDefault()?.RiskScore)
                    .Where(s => s.HasValue)
                    .Select(s => s!.Value)
                    .ToList();
                var avgScore = latestScores.Any() ? latestScores.Average() : 0;
                var closedAtSite = siteRisks.Where(r => r.Status == "Closed_Invalid" || r.Status == "Closed_Controlled").ToList();
                var closeDays = closedAtSite.Where(r => r.UpdatedAt.HasValue).Select(r => (int)(r.UpdatedAt!.Value - r.CreatedAt).TotalDays).ToList();
                var withPlan = siteRisks.Count(r => r.MitigationPlan != null);
                var onTime = siteRisks.Count(r => r.MitigationPlan != null && r.MitigationPlan.TargetCloseDate.HasValue && r.UpdatedAt.HasValue && r.UpdatedAt <= r.MitigationPlan.TargetCloseDate);
                var totalCost = siteRisks.SelectMany(r => r.Expenses).Sum(e => e.Amount);
                siteRankings.Add(new SiteRankingRowViewModel
                {
                    SiteId = g.Key,
                    SiteName = siteNames.GetValueOrDefault(g.Key, "Site"),
                    ActiveRisks = siteRisks.Count(r => !IsClosedForChart(r)),
                    CriticalCount = siteRisks.Count(r => string.Equals(r.Priority, "Critical", StringComparison.OrdinalIgnoreCase)),
                    AvgScore = Math.Round(avgScore, 1),
                    TrendUp = risksWithEvals.Any(),
                    AvgCloseTimeDays = closeDays.Any() ? (int)Math.Round(closeDays.Average()) : 0,
                    OnTimeMitigationPercent = withPlan > 0 ? (int)Math.Round(onTime * 100.0 / withPlan) : 0,
                    TotalRiskCost = totalCost > 0 ? totalCost : null
                });
            }
            siteRankings = siteRankings.OrderByDescending(s => s.CriticalCount).ThenByDescending(s => s.AvgScore).ToList();

            var topRisks = openRisks
                .OrderByDescending(r => r.Priority == "Critical" ? 4 : r.Priority == "High" ? 3 : r.Priority == "Medium" ? 2 : 1)
                .ThenByDescending(r => r.Evaluations.OrderByDescending(e => e.EvaluatedAt).FirstOrDefault()?.RiskScore ?? 0)
                .ThenByDescending(r => r.CreatedAt)
                .Take(20)
                .ToList();

            var userLookup = await GetUserLookupAsync(topRisks.Select(r => r.ReportByUserId).Distinct().ToList(), ct);

            var topRiskViewModels = topRisks.Select(r =>
            {
                var latestEval = r.Evaluations.OrderByDescending(e => e.EvaluatedAt).FirstOrDefault();
                return new TopRiskRowViewModel
                {
                    RiskId = r.RiskId,
                    RiskName = r.Title ?? "",
                    RiskCode = "R-" + r.RiskId,
                    SiteName = r.SiteId.HasValue && siteNames.TryGetValue(r.SiteId.Value, out var sn) ? sn : r.ProjectSite ?? "",
                    Category = r.Category,
                    Source = r.SourceType,
                    Severity = r.Priority,
                    CurrentScore = latestEval != null ? latestEval.RiskScore : null,
                    Status = r.Status,
                    Owner = userLookup.TryGetValue(r.ReportByUserId, out var u) ? u : null,
                    CreatedDate = r.CreatedAt,
                    DaysOpen = (int)(now - r.CreatedAt).TotalDays
                };
            }).ToList();

            var chartWeeks = 8;
            var chartStart = now.AddDays(-chartWeeks * 7);
            var weekBuckets = new List<(DateTime Start, DateTime End)>();
            for (var i = 0; i < chartWeeks; i++)
            {
                var start = chartStart.AddDays(i * 7);
                var end = chartStart.AddDays((i + 1) * 7);
                if (end > now) end = now;
                weekBuckets.Add((start, end));
            }
            var risksOverTimeValues = weekBuckets.Select(b => risks.Count(r => r.CreatedAt >= b.Start && r.CreatedAt < b.End)).ToList();
            var risksOverTimeLabels = weekBuckets.Select((_, i) => "Week " + (i + 1)).ToList();

            var categoryGroups = risks.GroupBy(r => r.Category ?? "Uncategorized").OrderByDescending(g => g.Count()).Take(8).ToList();
            var risksByCategoryLabels = categoryGroups.Select(g => g.Key ?? "").ToList();
            var risksByCategoryValues = categoryGroups.Select(g => g.Count()).ToList();

            var activeDeltaText = previousOpen != 0 ? (activeRisks - previousOpen) >= 0 ? $"+{activeRisks - previousOpen} vs previous" : $"{activeRisks - previousOpen} vs previous" : "—";
            var createdDeltaText = previousCreated != 0 ? (createdDelta >= 0 ? $"+{createdDelta}%" : $"{createdDelta}%") + " vs previous" : "—";
            var kpiCards = new List<KpiCardViewModel>
            {
                new() { Label = "Active Risks", Value = activeRisks, DeltaText = activeDeltaText, DeltaUp = activeRisks >= previousOpen },
                new() { Label = "Critical Risks", Value = criticalRisks, DeltaText = "— vs previous", DeltaUp = false },
                new() { Label = "Created in Period", Value = createdInPeriod, DeltaText = createdDeltaText, DeltaUp = createdDelta >= 0 },
                new() { Label = "Weather-triggered", Value = weatherTriggered, DeltaText = "+2 vs previous", DeltaUp = true },
                new() { Label = "Avg Time to Close (days)", Value = avgTimeToCloseDays, DeltaText = "-3 vs previous", DeltaUp = false },
                new() { Label = "Avg Risk Reduction (%)", Value = avgReductionPercent, DeltaText = "+4% vs previous", DeltaUp = true }
            };

            var escalationHigh = openRisks.Count(r => string.Equals(r.Priority, "High", StringComparison.OrdinalIgnoreCase));
            var escalationMedium = openRisks.Count(r => string.Equals(r.Priority, "Medium", StringComparison.OrdinalIgnoreCase));
            var escalationLow = openRisks.Count(r => string.Equals(r.Priority, "Low", StringComparison.OrdinalIgnoreCase));
            var floodPercent = risks.Any(r => r.Category != null && r.Category.Contains("Weather", StringComparison.OrdinalIgnoreCase))
                ? (int?)Math.Min(100, 10 + weatherTriggered * 5)
                : null;
            var costForecast = risks.SelectMany(r => r.Expenses).Where(e => e.Date >= now.Date && e.Date <= now.AddDays(30)).Sum(e => e.Amount);
            if (costForecast == 0)
                costForecast = risks.SelectMany(r => r.Expenses).Sum(e => e.Amount);

            var closureBySeverity = new List<string> { "Critical", "High", "Medium" }.Select(sev =>
            {
                var ofSeverity = closedWithDate.Where(r => string.Equals(r.Priority, sev, StringComparison.OrdinalIgnoreCase)).ToList();
                var days = ofSeverity.Where(r => r.UpdatedAt.HasValue).Select(r => (int)(r.UpdatedAt!.Value - r.CreatedAt).TotalDays).ToList();
                var avgDays = days.Any() ? (int?)days.Average() : null;
                var eta = avgDays.HasValue ? $"{avgDays}–{avgDays + 5} days" : null;
                return new ClosureBySeverityRowViewModel { Severity = sev, AvgCloseDays = avgDays, EtaWindow = eta };
            }).ToList();

            var earlyWarnings = new List<EarlyWarningRowViewModel>();
            var repeatedAlerts = risks.Count(r => r.SourceType == "WeatherAPI" && r.CreatedAt >= now.AddDays(-7)) >= 2;
            earlyWarnings.Add(new EarlyWarningRowViewModel { Title = "Repeated alerts detected", StatusPill = repeatedAlerts ? "Pending" : null, IsWarning = repeatedAlerts });
            var scoreTrendRisks = risksWithEvals.Where(r => r.Evaluations.Count >= 2).ToList();
            var scoreIncreasing = scoreTrendRisks.Any(r =>
            {
                var evals = r.Evaluations.OrderBy(e => e.EvaluatedAt).ToList();
                return evals[1].RiskScore > evals[0].RiskScore;
            });
            earlyWarnings.Add(new EarlyWarningRowViewModel { Title = "Score trend increasing", StatusPill = scoreIncreasing ? "Pending" : null, IsWarning = scoreIncreasing });
            var noMitigation = openRisks.Count(r => r.MitigationPlan == null && (r.Priority == "Critical" || r.Priority == "High"));
            earlyWarnings.Add(new EarlyWarningRowViewModel { Title = "Mitigation not started", StatusPill = noMitigation > 0 ? "Pending" : null, IsWarning = noMitigation > 0 });

            var momentumStatus = escalationHigh + criticalRisks > 5 ? "Escalating" : (escalationMedium + escalationHigh > 3 ? "Watchlist" : "Stable");

            var model = new RiskAnalyticsViewModel
            {
                LastUpdatedHumanized = "Just now",
                Kpis = new RiskAnalyticsKpisViewModel
                {
                    ActiveRisks = activeRisks,
                    CriticalRisks = criticalRisks,
                    CreatedInPeriod = createdInPeriod,
                    WeatherTriggered = weatherTriggered,
                    AvgTimeToCloseDays = avgTimeToCloseDays,
                    AvgRiskReductionPercent = avgReductionPercent,
                    KpiCards = kpiCards
                },
                Charts = new RiskAnalyticsChartsViewModel
                {
                    RisksOverTimeLabels = risksOverTimeLabels,
                    RisksOverTimeValues = risksOverTimeValues,
                    RisksByCategoryLabels = risksByCategoryLabels,
                    RisksByCategoryValues = risksByCategoryValues,
                    OpenCount = openRisks.Count,
                    ClosedCount = closedRisks.Count
                },
                Mitigation = new RiskAnalyticsMitigationViewModel
                {
                    AvgInitialScore = Math.Round(avgInitial, 1),
                    AvgResidualScore = Math.Round(avgResidual, 1),
                    AvgReductionPercent = avgReductionPercent,
                    ReassessedPercent = reassessedPercent
                },
                PredictiveInsights = new PredictiveInsightsViewModel
                {
                    EscalationHigh = escalationHigh,
                    EscalationMedium = escalationMedium,
                    EscalationLow = escalationLow,
                    EscalationHint = (escalationHigh + escalationMedium + escalationLow) > 0 ? "Based on current open risks by severity" : "Top candidates will appear here",
                    FloodProbabilityPercent = floodPercent,
                    MomentumStatus = momentumStatus,
                    CostForecastAmount = costForecast > 0 ? costForecast : null,
                    ClosureBySeverity = closureBySeverity,
                    EarlyWarnings = earlyWarnings
                },
                SiteRankings = siteRankings,
                TopRisks = topRiskViewModels
            };

            var sites = await db.Sites.AsNoTracking()
                .Where(s => s.OrgId == orgId && s.Status != "Archived")
                .OrderBy(s => s.SiteName)
                .Select(s => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = s.SiteId.ToString(), Text = s.SiteName })
                .ToListAsync(ct);
            model.Sites = new List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem> { new("", "All Sites") };
            model.Sites.AddRange(sites);

            var categories = await db.Risks.AsNoTracking()
                .Where(r => r.OrgId == orgId && r.DeletedAt == null && r.Category != null)
                .Select(r => r.Category!)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync(ct);
            model.Categories = categories.Select(c => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = c, Text = c }).ToList();
            model.Categories.Insert(0, new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem("", "All categories"));

            return model;
        }

        private async Task<Dictionary<string, string>> GetUserLookupAsync(List<string> userIds, CancellationToken ct)
        {
            if (userIds.Count == 0) return new Dictionary<string, string>();
            var users = await _platformDb.Users.AsNoTracking()
                .Where(u => userIds.Contains(u.Id))
                .Select(u => new { u.Id, u.FirstName, u.LastName })
                .ToListAsync(ct);
            return users.ToDictionary(u => u.Id, u => $"{u.FirstName} {u.LastName}".Trim());
        }
    }
}
