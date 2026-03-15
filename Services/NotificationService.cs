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
            var recipients = await GetRecipientsAsync(orgId, reportByUserId, ct);
            if (recipients.Count == 0) return;

            await using var db = await _tenantDbFactory.CreateAsync(orgId);
            var now = DateTime.UtcNow;
            foreach (var recipient in recipients)
            {
                if (!recipient.IsAdmin && !recipient.IsRiskManager && !IsEmployeeRelevantRiskEvent(eventType))
                {
                    continue;
                }

                var content = BuildRiskNotificationContent(eventType, title, message, recipient);
                db.Notifications.Add(new Notification
                {
                    OrgId = orgId,
                    UserId = recipient.UserId,
                    Title = content.Title,
                    Message = content.Message,
                    EntityType = "Risk",
                    EntityId = riskId,
                    CreatedAt = now
                });
            }
            var notifiedCount = db.ChangeTracker.Entries<Notification>().Count(e => e.State == EntityState.Added);
            if (notifiedCount == 0) return;

            _riskService.AddAuditLog(db, orgId, "System", "Notification", riskId ?? 0, "NotificationSent", $"Risk event '{eventType}' notified to {notifiedCount} recipient(s)", null);
            await db.SaveChangesAsync(ct);
        }

        public async Task NotifyMitigationTaskAssignmentAsync(int orgId, int riskId, int taskId, string taskTitle, string assignedToUserId, string assignedByUserId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(assignedToUserId)) return;

            var recipients = await GetRecipientsAsync(orgId, null, ct);
            if (recipients.Count == 0) return;

            var assigneeName = await _platformDb.Users.AsNoTracking()
                .Where(u => u.Id == assignedToUserId)
                .Select(u => (u.FirstName + " " + u.LastName).Trim())
                .FirstOrDefaultAsync(ct) ?? "Assigned user";

            var notifierTargets = recipients
                .Where(r => r.UserId.Equals(assignedToUserId, StringComparison.OrdinalIgnoreCase)
                    || ((r.IsAdmin || r.IsRiskManager) && !r.UserId.Equals(assignedByUserId, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (notifierTargets.Count == 0) return;

            await using var db = await _tenantDbFactory.CreateAsync(orgId);
            var now = DateTime.UtcNow;

            foreach (var recipient in notifierTargets)
            {
                var isAssignee = recipient.UserId.Equals(assignedToUserId, StringComparison.OrdinalIgnoreCase);
                var title = isAssignee
                    ? "New mitigation task assigned"
                    : "Mitigation task assignment update";
                var message = isAssignee
                    ? $"You were assigned '{taskTitle}'. Open My Tasks to start mitigation."
                    : $"{assigneeName} was assigned '{taskTitle}' for risk #{riskId}.";

                db.Notifications.Add(new Notification
                {
                    OrgId = orgId,
                    UserId = recipient.UserId,
                    Title = title,
                    Message = message.Length > 500 ? message.Substring(0, 500) : message,
                    EntityType = "Risk",
                    EntityId = riskId,
                    CreatedAt = now
                });
            }

            _riskService.AddAuditLog(db, orgId, "System", "Notification", taskId, "TaskAssignmentNotified", $"Mitigation task assignment notification sent to {notifierTargets.Count} recipient(s)", null);
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

        private async Task<List<RecipientContext>> GetRecipientsAsync(int orgId, string? reportByUserId, CancellationToken ct)
        {
            var recipients = new Dictionary<string, RecipientContext>(StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrEmpty(reportByUserId))
            {
                recipients[reportByUserId] = new RecipientContext
                {
                    UserId = reportByUserId,
                    IsReporter = true
                };
            }

            var orgUsers = await _platformDb.Users
                .Where(u => u.OrganizationId == orgId && u.IsActive)
                .ToListAsync(ct);

            foreach (var u in orgUsers)
            {
                if (!recipients.TryGetValue(u.Id, out var ctx))
                {
                    ctx = new RecipientContext { UserId = u.Id };
                    recipients[u.Id] = ctx;
                }

                var roles = await _userManager.GetRolesAsync(u);
                ctx.IsAdmin = roles.Any(r => string.Equals(r, "Admin", StringComparison.OrdinalIgnoreCase) || string.Equals(r, "SuperAdmin", StringComparison.OrdinalIgnoreCase));
                ctx.IsRiskManager = roles.Any(r => string.Equals(r, "RiskManager", StringComparison.OrdinalIgnoreCase));
                ctx.IsEmployee = roles.Any(r => string.Equals(r, "Employee", StringComparison.OrdinalIgnoreCase));

                if (!ctx.IsAdmin && !ctx.IsRiskManager && !ctx.IsReporter)
                {
                    recipients.Remove(u.Id);
                }
            }

            return recipients.Values.ToList();
        }

        private static (string Title, string Message) BuildRiskNotificationContent(string eventType, string title, string message, RecipientContext recipient)
        {
            var safeTitle = string.IsNullOrWhiteSpace(title) ? "Risk update" : title.Trim();
            var safeMessage = string.IsNullOrWhiteSpace(message) ? "A risk update is available." : message.Trim();

            if (recipient.IsAdmin || recipient.IsRiskManager)
            {
                return eventType switch
                {
                    "Submitted" => ($"Action needed: {safeTitle}", safeMessage),
                    "MitigationRequired" => ($"Planning queue: {safeTitle}", safeMessage),
                    "MitigationCompleted" => ($"Assessment queue: {safeTitle}", safeMessage),
                    _ => (safeTitle, safeMessage)
                };
            }

            return eventType switch
            {
                "HighCriticalAssessed" => ("Your risk was assessed", "Your reported risk has been assessed and prioritized. Check the latest risk status."),
                "ResidualAssessed" => ("Residual risk assessed", "Residual assessment is complete for your reported risk."),
                _ => (safeTitle, safeMessage)
            };
        }

        private static bool IsEmployeeRelevantRiskEvent(string eventType)
        {
            if (string.IsNullOrWhiteSpace(eventType)) return false;

            return eventType.Contains("Assess", StringComparison.OrdinalIgnoreCase)
                || string.Equals(eventType, "HighCriticalAssessed", StringComparison.OrdinalIgnoreCase)
                || string.Equals(eventType, "ResidualAssessed", StringComparison.OrdinalIgnoreCase);
        }

        private sealed class RecipientContext
        {
            public string UserId { get; set; } = string.Empty;
            public bool IsAdmin { get; set; }
            public bool IsRiskManager { get; set; }
            public bool IsEmployee { get; set; }
            public bool IsReporter { get; set; }
        }
    }
}
