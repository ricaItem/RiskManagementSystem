using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using WEB_Sentro.Areas.Vendor.Models;
using WEB_Sentro.Data;
using WEB_Sentro.Services;

namespace WEB_Sentro.Areas.Vendor.Controllers
{
    [Area("Vendor")]
    [Authorize(Policy = "SuperAdminOnly")]
    public class LogsController : Controller
    {
        private readonly PlatformDbContext _platformDb;
        private readonly ITenantDbFactory _tenantDbFactory;
        private readonly IMemoryCache _cache;

        public LogsController(PlatformDbContext platformDb, ITenantDbFactory tenantDbFactory, IMemoryCache cache)
        {
            _platformDb = platformDb;
            _tenantDbFactory = tenantDbFactory;
            _cache = cache;
        }

        public async Task<IActionResult> Index(string? search, string? severity, int? orgId, int page = 1, int pageSize = 25, CancellationToken ct = default)
        {
            if (page < 1) page = 1;
            if (pageSize <= 0) pageSize = 25;
            if (pageSize > 100) pageSize = 100;

            var model = await BuildModelAsync(search, severity, orgId, page, pageSize, ct);
            return View(model);
        }

        private async Task<LogsIndexViewModel> BuildModelAsync(string? search, string? severity, int? orgId, int page, int pageSize, CancellationToken ct)
        {
            var organizations = await _platformDb.Organizations.AsNoTracking()
                .OrderBy(o => o.OrgName)
                .Select(o => new OrganizationOptionViewModel { OrganizationId = o.OrganizationId, OrgName = o.OrgName })
                .ToListAsync(ct);

            var selectedOrgIds = orgId.HasValue && orgId.Value > 0
                ? new List<int> { orgId.Value }
                : organizations.Select(o => o.OrganizationId).ToList();

            var cacheKey = $"vendor:logs:{orgId}:{severity}:{search}";
            var rows = await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1);
                return await LoadRowsAsync(selectedOrgIds, search, severity, ct);
            }) ?? new List<VendorLogRowViewModel>();

            var totalCount = rows.Count;
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
            if (page > totalPages) page = totalPages;

            var pageRows = rows
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return new LogsIndexViewModel
            {
                Search = search,
                Severity = severity,
                OrganizationId = orgId,
                CurrentPage = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = totalPages,
                OrganizationOptions = organizations,
                Logs = pageRows
            };
        }

        private async Task<List<VendorLogRowViewModel>> LoadRowsAsync(List<int> orgIds, string? search, string? severity, CancellationToken ct)
        {
            var organizationLookup = await _platformDb.Organizations.AsNoTracking()
                .Where(o => orgIds.Contains(o.OrganizationId))
                .ToDictionaryAsync(o => o.OrganizationId, o => o.OrgName, ct);

            var allRows = new List<VendorLogRowViewModel>();
            var fromUtc = DateTime.UtcNow.AddDays(-14);

            foreach (var currentOrgId in orgIds)
            {
                try
                {
                    await using var tenantDb = await _tenantDbFactory.CreateAsync(currentOrgId);
                    var baseQuery = tenantDb.AuditLogs.AsNoTracking()
                        .Where(a => a.OrgId == currentOrgId && a.CreatedAt >= fromUtc);

                    if (!string.IsNullOrWhiteSpace(search))
                    {
                        var term = search.Trim();
                        baseQuery = baseQuery.Where(a =>
                            a.ActionType.Contains(term) ||
                            a.EntityType.Contains(term) ||
                            (a.Message != null && a.Message.Contains(term)) ||
                            a.UserId.Contains(term));
                    }

                    if (!string.IsNullOrWhiteSpace(severity))
                    {
                        if (severity.Equals("critical", StringComparison.OrdinalIgnoreCase))
                        {
                            baseQuery = baseQuery.Where(a => a.Level == "Critical" || a.Level == "Error");
                        }
                        else if (severity.Equals("warning", StringComparison.OrdinalIgnoreCase))
                        {
                            baseQuery = baseQuery.Where(a => a.Level == "Warning");
                        }
                        else if (severity.Equals("success", StringComparison.OrdinalIgnoreCase))
                        {
                            baseQuery = baseQuery.Where(a => a.Level == "Success" || a.Level == null);
                        }
                    }

                    var rawRows = await baseQuery
                        .OrderByDescending(a => a.CreatedAt)
                        .Take(40)
                        .Select(a => new { a.CreatedAt, a.UserId, a.ActionType, a.Level })
                        .ToListAsync(ct);

                    if (rawRows.Count == 0)
                    {
                        continue;
                    }

                    var userIds = rawRows.Select(x => x.UserId).Distinct().ToList();
                    var usersLookup = await _platformDb.Users.AsNoTracking()
                        .Where(u => userIds.Contains(u.Id))
                        .ToDictionaryAsync(u => u.Id, u => string.IsNullOrWhiteSpace($"{u.FirstName} {u.LastName}".Trim()) ? (u.Email ?? u.UserName ?? u.Id) : $"{u.FirstName} {u.LastName}".Trim(), ct);

                    foreach (var row in rawRows)
                    {
                        var mappedStatus = string.IsNullOrWhiteSpace(row.Level) ? "Info" : row.Level;
                        allRows.Add(new VendorLogRowViewModel
                        {
                            TimestampUtc = row.CreatedAt,
                            TimestampDisplay = row.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                            OrganizationId = currentOrgId,
                            OrganizationName = organizationLookup.GetValueOrDefault(currentOrgId, "-"),
                            ActorName = usersLookup.GetValueOrDefault(row.UserId, row.UserId),
                            Event = row.ActionType,
                            Status = mappedStatus,
                            StatusColorClass = GetStatusColorClass(mappedStatus)
                        });
                    }
                }
                catch
                {
                    allRows.Add(new VendorLogRowViewModel
                    {
                        TimestampUtc = DateTime.UtcNow,
                        TimestampDisplay = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
                        OrganizationId = currentOrgId,
                        OrganizationName = organizationLookup.GetValueOrDefault(currentOrgId, "-"),
                        ActorName = "System",
                        Event = "TenantLogUnavailable",
                        Status = "Warning",
                        StatusColorClass = "text-amber-500"
                    });
                }
            }

            return allRows
                .OrderByDescending(x => x.TimestampUtc)
                .Take(200)
                .ToList();
        }

        private static string GetStatusColorClass(string status)
        {
            if (status.Equals("Critical", StringComparison.OrdinalIgnoreCase) || status.Equals("Error", StringComparison.OrdinalIgnoreCase))
            {
                return "text-rose-500";
            }

            if (status.Equals("Warning", StringComparison.OrdinalIgnoreCase))
            {
                return "text-amber-500";
            }

            if (status.Equals("Success", StringComparison.OrdinalIgnoreCase))
            {
                return "text-emerald-500";
            }

            return "text-slate-500";
        }
    }
}
