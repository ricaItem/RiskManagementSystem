using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WEB_Sentro.Data;
using WEB_Sentro.Data.Entities;
using WEB_Sentro.Models.Identity;

namespace WEB_Sentro.Services
{
    public class NotificationService : INotificationService
    {
        private readonly ITenantDbFactory _tenantDbFactory;
        private readonly PlatformDbContext _platformDb;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RiskService _riskService;

        public NotificationService(
            ITenantDbFactory tenantDbFactory,
            PlatformDbContext platformDb,
            UserManager<ApplicationUser> userManager,
            RiskService riskService)
        {
            _tenantDbFactory = tenantDbFactory;
            _platformDb = platformDb;
            _userManager = userManager;
            _riskService = riskService;
        }

        public async Task NotifyRiskEventAsync(int orgId, string eventType, int? riskId, string title, string message, string? reportByUserId, CancellationToken ct = default)
        {
            var recipientIds = await GetRecipientIdsAsync(orgId, reportByUserId, ct);
            if (recipientIds.Count == 0) return;

            await using var db = await _tenantDbFactory.CreateAsync(orgId);
            var now = DateTime.UtcNow;
            foreach (var userId in recipientIds)
            {
                db.Notifications.Add(new Notification
                {
                    OrgId = orgId,
                    UserId = userId,
                    Title = title,
                    Message = message.Length > 500 ? message.Substring(0, 500) : message,
                    EntityType = "Risk",
                    EntityId = riskId,
                    CreatedAt = now
                });
            }
            _riskService.AddAuditLog(db, orgId, "System", "Notification", riskId ?? 0, "NotificationSent", $"Risk event '{eventType}' notified to {recipientIds.Count} recipient(s)", null);
            await db.SaveChangesAsync(ct);
        }

        public async Task NotifyMonitoringAlertAsync(int orgId, int monitoringSiteId, string ruleName, string severity, string measuredValues, int? riskId, CancellationToken ct = default)
        {
            // Reuse the existing risk-event notification pipeline so dropdown/badge behavior is consistent.
            // We treat monitoring alerts as a "MonitoringAlert" event type linked to the associated risk (if any).

            await using var db = await _tenantDbFactory.CreateAsync(orgId);

            var site = await db.MonitoringSites.AsNoTracking()
                .Where(s => s.OrgId == orgId && s.MonitoringSiteId == monitoringSiteId)
                .Select(s => new { s.MonitoringSiteId, s.Name })
                .FirstOrDefaultAsync(ct);

            var siteName = site?.Name ?? "Monitoring site";
            var title = $"{ruleName} – {severity} alert at {siteName}";
            var message = string.IsNullOrWhiteSpace(measuredValues)
                ? $"Monitoring rule '{ruleName}' triggered with severity {severity} at {siteName}."
                : measuredValues;

            // Delegate to NotifyRiskEventAsync so that notifications are created exactly
            // like other risk events (same recipients, badge logic, and ActionUrl behavior).
            await NotifyRiskEventAsync(orgId, "MonitoringAlert", riskId, title, message, reportByUserId: null, ct);
        }

        private async Task<List<string>> GetRecipientIdsAsync(int orgId, string? reportByUserId, CancellationToken ct)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrEmpty(reportByUserId))
                set.Add(reportByUserId);

            var orgUsers = await _platformDb.Users
                .Where(u => u.OrganizationId == orgId && u.IsActive)
                .ToListAsync(ct);
            foreach (var u in orgUsers)
            {
                var roles = await _userManager.GetRolesAsync(u);
                if (roles.Any(r => string.Equals(r, "Admin", StringComparison.OrdinalIgnoreCase) || string.Equals(r, "RiskManager", StringComparison.OrdinalIgnoreCase)))
                    set.Add(u.Id);
            }
            return set.ToList();
        }
    }
}
