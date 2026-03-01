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

        public async Task<IActionResult> Index(DateTime? from, DateTime? to, CancellationToken ct = default)
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
                return View(new List<AuditLogEntryViewModel>());
            }

            await using var db = await _tenantDbFactory.CreateAsync(orgId);
            var logs = await db.AuditLogs.AsNoTracking()
                .Where(a => a.OrgId == orgId && a.CreatedAt >= fromDate && a.CreatedAt < toDate)
                .OrderByDescending(a => a.CreatedAt)
                .Take(500)
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
                Timestamp = a.CreatedAt,
                IpAddress = a.IpAddress,
                Status = string.IsNullOrEmpty(a.Level) ? "Success" : (a.Level == "Warning" ? "Warning" : a.Level == "Error" ? "Critical" : a.Level)
            }).ToList();

            ViewBag.From = fromDate;
            ViewBag.To = toDate == DateTime.UtcNow.AddDays(1) ? (DateTime?)null : toDate;
            return View(model);
        }
    }
}
