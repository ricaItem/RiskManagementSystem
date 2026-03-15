using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WEB_Sentro.Data;
using WEB_Sentro.Data.Entities;
using WEB_Sentro.Models.Identity;
using WEB_Sentro.Services;

namespace Web_Sentro.Areas.Client.Controllers
{
    [Area("Client")]
    [Authorize]
    public class NotificationsController : Controller
    {
        private readonly ITenantDbFactory _tenantDbFactory;
        private readonly PlatformDbContext _platformDb;

        public NotificationsController(ITenantDbFactory tenantDbFactory, PlatformDbContext platformDb)
        {
            _tenantDbFactory = tenantDbFactory;
            _platformDb = platformDb;
        }

        public async Task<IActionResult> Index(CancellationToken ct = default)
        {
            var user = await GetCurrentUserInfoAsync(ct);
            if (user == null || user.Value.OrganizationId <= 0)
                return View(new List<NotificationRowVm>());

            var (userId, orgId) = user.Value;
            await using var db = await _tenantDbFactory.CreateAsync(orgId);
            var list = await db.Notifications.AsNoTracking()
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .Take(100)
                .ToListAsync(ct);
            var vms = list.Select(n => ToRowVm(n)).ToList();
            return View(vms);
        }

        /// <summary>Returns unread count and recent items for the notification dropdown (JSON).</summary>
        [HttpGet]
        public async Task<IActionResult> ApiDropdown(CancellationToken ct = default)
        {
            var user = await GetCurrentUserInfoAsync(ct);
            if (user == null || user.Value.OrganizationId <= 0)
                return Json(new { unreadCount = 0, items = Array.Empty<object>() });

            var (userId, orgId) = user.Value;
            await using var db = await _tenantDbFactory.CreateAsync(orgId);
            var unreadCount = await db.Notifications.CountAsync(n => n.UserId == userId && n.ReadAt == null, ct);
            var isEmployee = User.IsInRole("Employee");
            var myMitigationTasksCount = 0;
            if (isEmployee)
            {
                myMitigationTasksCount = await db.MitigationTasks.AsNoTracking()
                    .Where(t => t.AssignedToUserId == userId
                        && t.Status != "Done"
                        && t.Plan != null
                        && t.Plan.DeletedAt == null
                        && t.Plan.Risk.OrgId == orgId
                        && t.Plan.Risk.DeletedAt == null)
                    .CountAsync(ct);
            }

            var list = await db.Notifications.AsNoTracking()
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .Take(15)
                .ToListAsync(ct);
            var items = list.Select(n => new
            {
                n.NotificationId,
                n.Title,
                n.Message,
                n.CreatedAt,
                n.ReadAt,
                ActionUrl = BuildActionUrl(n)
            }).ToList();
            return Json(new { unreadCount, items, isEmployee, myMitigationTasksCount });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApiMarkRead(int id, CancellationToken ct = default)
        {
            var user = await GetCurrentUserInfoAsync(ct);
            if (user == null || user.Value.OrganizationId <= 0) return NotFound();
            var (userId, orgId) = user.Value;
            await using var db = await _tenantDbFactory.CreateAsync(orgId);
            var n = await db.Notifications.FirstOrDefaultAsync(x => x.NotificationId == id && x.UserId == userId, ct);
            if (n == null) return NotFound();
            n.ReadAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return Json(new { ok = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApiMarkAllRead(CancellationToken ct = default)
        {
            var user = await GetCurrentUserInfoAsync(ct);
            if (user == null || user.Value.OrganizationId <= 0) return NotFound();
            var (userId, orgId) = user.Value;
            await using var db = await _tenantDbFactory.CreateAsync(orgId);
            await db.Notifications
                .Where(n => n.UserId == userId && n.ReadAt == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(n => n.ReadAt, DateTime.UtcNow), ct);
            return Json(new { ok = true });
        }

        private async Task<(string Id, int OrganizationId)?> GetCurrentUserInfoAsync(CancellationToken ct)
        {
            var u = await _platformDb.Users.AsNoTracking()
                .Where(x => x.UserName == User.Identity!.Name)
                .Select(x => new { x.Id, x.OrganizationId })
                .FirstOrDefaultAsync(ct);
            return u == null ? null : (u.Id, u.OrganizationId);
        }

        private static string? BuildActionUrl(Notification n)
        {
            if (n.EntityType != "Risk" || !n.EntityId.HasValue) return null;
            var id = n.EntityId.Value;
            var t = (n.Title ?? "").ToLowerInvariant();
            if (t.Contains("mitigation") || t.Contains("closed") || t.Contains("task"))
                return $"/Client/Mitigation/Board?riskId={id}";
            return $"/Client/Risks/Assess/{id}";
        }

        private static NotificationRowVm ToRowVm(Notification n)
        {
            return new NotificationRowVm
            {
                NotificationId = n.NotificationId,
                Title = n.Title,
                Message = n.Message,
                EntityType = n.EntityType,
                EntityId = n.EntityId,
                CreatedAt = n.CreatedAt,
                ReadAt = n.ReadAt,
                ActionUrl = BuildActionUrl(n)
            };
        }
    }

    public class NotificationRowVm
    {
        public int NotificationId { get; set; }
        public string Title { get; set; } = "";
        public string Message { get; set; } = "";
        public string? EntityType { get; set; }
        public int? EntityId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ReadAt { get; set; }
        public string? ActionUrl { get; set; }
    }
}
