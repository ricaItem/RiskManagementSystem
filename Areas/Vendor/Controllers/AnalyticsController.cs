using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;
using WEB_Sentro.Areas.Vendor.Models;
using WEB_Sentro.Data;

namespace WEB_Sentro.Areas.Vendor.Controllers
{
    [Area("Vendor")]
    [Authorize(Policy = "SuperAdminOnly")]
    public class AnalyticsController : Controller
    {
        private readonly PlatformDbContext _platformDb;

        public AnalyticsController(PlatformDbContext platformDb)
        {
            _platformDb = platformDb;
        }

        public async Task<IActionResult> Index(
            string range = "30d",
            string? q = null,
            string risk = "all",
            string sort = "events_desc",
            int page = 1,
            int pageSize = 25,
            bool exportCsv = false,
            CancellationToken ct = default)
        {
            var normalizedRange = NormalizeRange(range);
            var normalizedRisk = NormalizeRisk(risk);
            var normalizedSort = NormalizeSort(sort);
            var search = q?.Trim();

            if (page < 1) page = 1;
            if (pageSize <= 0) pageSize = 25;
            if (pageSize > 100) pageSize = 100;

            var rangeQuery = _platformDb.OrganizationAnalyticsSnapshots.AsNoTracking()
                .Where(x => x.RangeKey == normalizedRange);

            var filteredQuery = ApplyFilters(rangeQuery, search, normalizedRisk);
            var orderedQuery = ApplySort(filteredQuery, normalizedSort);

            var totalCount = await filteredQuery.CountAsync(ct);
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
            if (page > totalPages) page = totalPages;

            var activeOrganizations = await _platformDb.Organizations.AsNoTracking()
                .CountAsync(o => o.Status == "Active", ct);

            var activityChangePercent = await rangeQuery
                .Select(x => (decimal?)x.ActivityChangePercent)
                .AverageAsync(ct) ?? 0m;

            var userEngagementChangePercent = await rangeQuery
                .Select(x => (decimal?)x.UserChangePercent)
                .AverageAsync(ct) ?? 0m;

            var adoptionChangePercent = await rangeQuery
                .Select(x => (decimal?)x.AdoptionChangePercent)
                .AverageAsync(ct) ?? 0m;

            var atRiskOrganizationsCount = await rangeQuery
                .CountAsync(x => x.SegmentLabel == "At risk", ct);

            var growingOrganizationsCount = await rangeQuery
                .CountAsync(x => x.SegmentLabel == "Growing", ct);

            var inactiveOrganizationsCount = await rangeQuery
                .CountAsync(x => x.SegmentLabel == "Inactive", ct);

            var totalAuditEventsInRange = await rangeQuery
                .SumAsync(x => (int?)x.EventCountInRange, ct) ?? 0;

            var rowsQuery = exportCsv
                ? orderedQuery
                : orderedQuery.Skip((page - 1) * pageSize).Take(pageSize);

            var rows = await rowsQuery
                .Select(x => new
                {
                    x.OrganizationId,
                    x.OrganizationName,
                    x.PlanName,
                    x.SubscriptionStatus,
                    x.UserCount,
                    x.SeatLimit,
                    x.SeatUtilizationPercent,
                    x.LoginsInRange,
                    x.EventCountInRange,
                    x.ActivityChangePercent,
                    x.UserChangePercent,
                    x.FeatureAdoptionPercent,
                    x.AdoptionChangePercent,
                    x.LastActivityAtUtc,
                    x.ErrorRatePercent,
                    x.RenewalDateDisplay,
                    x.HealthScore,
                    x.ChurnRiskLabel,
                    x.SegmentLabel,
                    x.TrendLabel,
                    x.ActivityTrendJson
                })
                .ToListAsync(ct);

            var mappedRows = rows.Select(x => new OrganizationUsageRowViewModel
            {
                OrganizationId = x.OrganizationId,
                OrganizationName = x.OrganizationName,
                PlanName = x.PlanName,
                SubscriptionStatus = x.SubscriptionStatus,
                UserCount = x.UserCount,
                SeatLimit = x.SeatLimit,
                SeatUtilizationLabel = x.SeatLimit.HasValue && x.SeatLimit.Value > 0 ? $"{x.SeatUtilizationPercent}%" : "Unlimited",
                LoginsInRange = x.LoginsInRange,
                EventCountInRange = x.EventCountInRange,
                ActivityChangePercent = x.ActivityChangePercent,
                UserChangePercent = x.UserChangePercent,
                FeatureAdoptionPercent = x.FeatureAdoptionPercent,
                AdoptionChangePercent = x.AdoptionChangePercent,
                LastActivityDisplay = FormatRelativeTime(x.LastActivityAtUtc, DateTime.UtcNow),
                ErrorRatePercent = x.ErrorRatePercent,
                RenewalDateDisplay = x.RenewalDateDisplay,
                HealthScore = x.HealthScore,
                ChurnRiskLabel = x.ChurnRiskLabel,
                SegmentLabel = x.SegmentLabel,
                ActivityTrendPoints = ParseTrendPoints(x.ActivityTrendJson),
                TrendLabel = x.TrendLabel
            }).ToList();

            var model = new AnalyticsIndexViewModel
            {
                RangeLabel = GetRangeLabel(normalizedRange),
                RangeKey = normalizedRange,
                SearchQuery = search ?? string.Empty,
                RiskFilter = normalizedRisk,
                SortBy = normalizedSort,
                CurrentPage = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = totalPages,
                ActiveOrganizations = activeOrganizations,
                TotalAuditEventsInRange = totalAuditEventsInRange,
                ActivityChangePercent = activityChangePercent,
                UserEngagementChangePercent = userEngagementChangePercent,
                AdoptionChangePercent = adoptionChangePercent,
                AtRiskOrganizationsCount = atRiskOrganizationsCount,
                GrowingOrganizationsCount = growingOrganizationsCount,
                InactiveOrganizationsCount = inactiveOrganizationsCount,
                TopOrganizations = mappedRows
            };

            if (exportCsv)
            {
                var csvBytes = Encoding.UTF8.GetBytes(BuildCsv(model));
                var fileName = $"vendor-usage-analytics-{normalizedRange}-{DateTime.UtcNow:yyyyMMddHHmmss}.csv";
                return File(csvBytes, "text/csv", fileName);
            }

            return View(model);
        }

        private static IQueryable<Data.Entities.OrganizationAnalyticsSnapshot> ApplyFilters(
            IQueryable<Data.Entities.OrganizationAnalyticsSnapshot> query,
            string? search,
            string risk)
        {
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(x =>
                    x.OrganizationName.Contains(search) ||
                    x.PlanName.Contains(search) ||
                    x.SubscriptionStatus.Contains(search));
            }

            if (!string.Equals(risk, "all", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(x => x.ChurnRiskLabel == risk);
            }

            return query;
        }

        private static IQueryable<Data.Entities.OrganizationAnalyticsSnapshot> ApplySort(
            IQueryable<Data.Entities.OrganizationAnalyticsSnapshot> query,
            string sort)
        {
            return sort switch
            {
                "org_asc" => query.OrderBy(x => x.OrganizationName),
                "org_desc" => query.OrderByDescending(x => x.OrganizationName),
                "users_desc" => query.OrderByDescending(x => x.UserCount).ThenBy(x => x.OrganizationName),
                "users_asc" => query.OrderBy(x => x.UserCount).ThenBy(x => x.OrganizationName),
                "events_asc" => query.OrderBy(x => x.EventCountInRange).ThenBy(x => x.OrganizationName),
                "activity_change_desc" => query.OrderByDescending(x => x.ActivityChangePercent).ThenBy(x => x.OrganizationName),
                "users_change_desc" => query.OrderByDescending(x => x.UserChangePercent).ThenBy(x => x.OrganizationName),
                "adoption_change_desc" => query.OrderByDescending(x => x.AdoptionChangePercent).ThenBy(x => x.OrganizationName),
                "adoption_desc" => query.OrderByDescending(x => x.FeatureAdoptionPercent).ThenBy(x => x.OrganizationName),
                "error_desc" => query.OrderByDescending(x => x.ErrorRatePercent).ThenBy(x => x.OrganizationName),
                "renewal_asc" => query.OrderBy(x => x.RenewalDateDisplay == "-").ThenBy(x => x.RenewalDateDisplay),
                "health_desc" => query.OrderByDescending(x => x.HealthScore).ThenBy(x => x.OrganizationName),
                "segment_desc" => query
                    .OrderByDescending(x => x.SegmentLabel == "At risk" ? 4 : x.SegmentLabel == "Inactive" ? 3 : x.SegmentLabel == "Growing" ? 2 : 1)
                    .ThenBy(x => x.OrganizationName),
                "risk_desc" => query
                    .OrderByDescending(x => x.ChurnRiskLabel == "High" ? 3 : x.ChurnRiskLabel == "Medium" ? 2 : 1)
                    .ThenBy(x => x.OrganizationName),
                _ => query.OrderByDescending(x => x.EventCountInRange).ThenByDescending(x => x.HealthScore).ThenBy(x => x.OrganizationName)
            };
        }

        private static List<int> ParseTrendPoints(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new List<int> { 0, 0, 0, 0, 0, 0 };

            try
            {
                return JsonSerializer.Deserialize<List<int>>(json) ?? new List<int> { 0, 0, 0, 0, 0, 0 };
            }
            catch
            {
                return new List<int> { 0, 0, 0, 0, 0, 0 };
            }
        }

        private static string FormatRelativeTime(DateTime? date, DateTime nowUtc)
        {
            if (!date.HasValue) return "No activity";

            var diff = nowUtc - date.Value;
            if (diff.TotalMinutes < 1) return "Just now";
            if (diff.TotalHours < 1) return $"{Math.Max(1, diff.Minutes)}m ago";
            if (diff.TotalDays < 1) return $"{Math.Max(1, (int)diff.TotalHours)}h ago";
            if (diff.TotalDays < 30) return $"{Math.Max(1, (int)diff.TotalDays)}d ago";
            return date.Value.ToString("yyyy-MM-dd");
        }

        private static string GetRangeLabel(string range)
        {
            return range switch
            {
                "90d" => "Last 90 Days",
                "1y" => "Last 12 Months",
                _ => "Last 30 Days"
            };
        }

        private static string NormalizeRange(string range)
        {
            if (string.Equals(range, "90d", StringComparison.OrdinalIgnoreCase)) return "90d";
            if (string.Equals(range, "1y", StringComparison.OrdinalIgnoreCase)) return "1y";
            return "30d";
        }

        private static string NormalizeRisk(string risk)
        {
            if (string.Equals(risk, "low", StringComparison.OrdinalIgnoreCase)) return "Low";
            if (string.Equals(risk, "medium", StringComparison.OrdinalIgnoreCase)) return "Medium";
            if (string.Equals(risk, "high", StringComparison.OrdinalIgnoreCase)) return "High";
            return "all";
        }

        private static string NormalizeSort(string sort)
        {
            return sort?.ToLowerInvariant() switch
            {
                "org_asc" => "org_asc",
                "org_desc" => "org_desc",
                "users_desc" => "users_desc",
                "users_asc" => "users_asc",
                "events_asc" => "events_asc",
                "events_desc" => "events_desc",
                "activity_change_desc" => "activity_change_desc",
                "users_change_desc" => "users_change_desc",
                "adoption_change_desc" => "adoption_change_desc",
                "adoption_desc" => "adoption_desc",
                "error_desc" => "error_desc",
                "renewal_asc" => "renewal_asc",
                "health_desc" => "health_desc",
                "segment_desc" => "segment_desc",
                "risk_desc" => "risk_desc",
                _ => "events_desc"
            };
        }

        private static string BuildCsv(AnalyticsIndexViewModel model)
        {
            static string Esc(string? value)
            {
                if (string.IsNullOrEmpty(value)) return "";
                return $"\"{value.Replace("\"", "\"\"")}\"";
            }

            var sb = new StringBuilder();
            sb.AppendLine("Organization,Plan,Subscription Status,Active Users,Seat Limit,Seat Utilization,Logins,Activity Events,Activity Change %,User Change %,Feature Adoption %,Adoption Change %,Error Rate %,Last Activity,Renewal Date,Health Score,Churn Risk,Segment,Trend");

            foreach (var row in model.TopOrganizations)
            {
                sb.AppendLine(string.Join(",",
                    Esc(row.OrganizationName),
                    Esc(row.PlanName),
                    Esc(row.SubscriptionStatus),
                    row.UserCount,
                    row.SeatLimit?.ToString() ?? "",
                    Esc(row.SeatUtilizationLabel),
                    row.LoginsInRange,
                    row.EventCountInRange,
                    row.ActivityChangePercent.ToString("0.0"),
                    row.UserChangePercent.ToString("0.0"),
                    row.FeatureAdoptionPercent,
                    row.AdoptionChangePercent.ToString("0.0"),
                    row.ErrorRatePercent.ToString("0.0"),
                    Esc(row.LastActivityDisplay),
                    Esc(row.RenewalDateDisplay),
                    row.HealthScore,
                    Esc(row.ChurnRiskLabel),
                    Esc(row.SegmentLabel),
                    Esc(row.TrendLabel)));
            }

            return sb.ToString();
        }
    }
}
