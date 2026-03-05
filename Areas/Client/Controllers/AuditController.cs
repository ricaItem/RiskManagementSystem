using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WEB_Sentro.Data;
using WEB_Sentro.Services;
using Web_Sentro.Areas.Client.Models;

namespace Web_Sentro.Areas.Client.Controllers
{
    [Area("Client")]
    [Authorize]
    public class AuditController : Controller
    {
        private readonly ITenantDbFactory _tenantDbFactory;
        private readonly PlatformDbContext _platformDb;

        public AuditController(ITenantDbFactory tenantDbFactory, PlatformDbContext platformDb)
        {
            _tenantDbFactory = tenantDbFactory;
            _platformDb = platformDb;
        }

        public IActionResult Index()
        {
            return View();
        }

        public async Task<IActionResult> AuditContent(DateTime? from, DateTime? to, string? search, string? severity, int page = 1, int pageSize = 10, CancellationToken ct = default)
        {
            var user = await _platformDb.Users.AsNoTracking()
                .Where(u => u.UserName == User.Identity!.Name)
                .Select(u => new { u.OrganizationId })
                .FirstOrDefaultAsync(ct);
            if (user == null)
                return Challenge();

            var orgId = user.OrganizationId;
            var fromDate = from ?? DateTime.UtcNow.AddDays(-30);
            var toDate = to ?? DateTime.UtcNow.AddDays(1);
            
            if (orgId <= 0)
            {
                ViewBag.From = fromDate;
                ViewBag.To = null;
                return PartialView("_AuditContent", new List<AuditLogEntryViewModel>());
            }

            await using var db = await _tenantDbFactory.CreateAsync(orgId);
            
            var query = db.AuditLogs.AsNoTracking()
                .Where(a => a.OrgId == orgId && a.CreatedAt >= fromDate && a.CreatedAt < toDate && a.ActionType != "BackgroundSync" && a.ActionType != "BackgroundSyncFailed");

            if (!string.IsNullOrEmpty(search))
            {
                search = search.ToLower();
                // Note: For EF Core, translation to SQL for complex searches might be tricky.
                // Assuming simple contains. For UserId, we might need a join or subquery if we want to search by name, 
                // but since UserId is a string GUID in AuditLog, we can search by that or other text fields.
                // Searching by User Name requires joining with PlatformDb which is cross-context.
                // For now, let's search in the available fields in AuditLog.
                query = query.Where(a => a.UserId.Contains(search) 
                                      || a.ActionType.Contains(search) 
                                      || a.EntityType.Contains(search) 
                                      || (a.Message != null && a.Message.Contains(search))
                                      || (a.IpAddress != null && a.IpAddress.Contains(search)));
            }

            if (!string.IsNullOrEmpty(severity))
            {
                if (severity.Equals("success", StringComparison.OrdinalIgnoreCase))
                    query = query.Where(a => a.Level == null || a.Level == "Success");
                else if (severity.Equals("warning", StringComparison.OrdinalIgnoreCase))
                    query = query.Where(a => a.Level == "Warning");
                else if (severity.Equals("critical", StringComparison.OrdinalIgnoreCase))
                    query = query.Where(a => a.Level == "Error" || a.Level == "Critical");
            }

            var totalCount = await query.CountAsync(ct);
            var logs = await query
                .OrderByDescending(a => a.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new { a.AuditId, a.UserId, a.EntityType, a.EntityId, a.ActionType, a.Level, a.Message, a.IpAddress, a.CreatedAt })
                .ToListAsync(ct);

            var userIds = logs.Select(l => l.UserId).Distinct().ToList();
            var users = await _platformDb.Users.AsNoTracking()
                .Where(u => userIds.Contains(u.Id))
                .Select(u => new { u.Id, u.FirstName, u.LastName })
                .ToListAsync(ct);
            var userDisplay = users.ToDictionary(u => u.Id, u => $"{u.FirstName} {u.LastName}".Trim());

            var model = logs.Select(a => new AuditLogEntryViewModel
            {
                Id = a.AuditId,
                User = userDisplay.TryGetValue(a.UserId, out var name) ? name : a.UserId,
                Action = a.ActionType,
                Module = a.EntityType,
                Details = a.Message ?? $"{a.EntityType} #{a.EntityId}",
                Timestamp = DateTime.SpecifyKind(a.CreatedAt, DateTimeKind.Utc),
                IpAddress = a.IpAddress,
                Status = string.IsNullOrEmpty(a.Level) ? "Success" : (a.Level == "Warning" ? "Warning" : a.Level == "Error" ? "Critical" : a.Level)
            }).ToList();

            ViewBag.From = fromDate;
            ViewBag.To = toDate == DateTime.UtcNow.AddDays(1) ? (DateTime?)null : toDate;
            
            // Pagination Data
            ViewBag.CurrentPage = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalCount = totalCount;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);
            ViewBag.Search = search;
            ViewBag.Severity = severity;

            return PartialView("_AuditContent", model);
        }
    }
}
