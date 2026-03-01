using Microsoft.EntityFrameworkCore;
using WEB_Sentro.Data;
using WEB_Sentro.Models.Identity;

namespace WEB_Sentro.Services
{
    /// <summary>Runs daily: updates OverdueFlag and creates notifications for risks due for review (or overdue).</summary>
    public class RiskReviewReminderHostedService : BackgroundService
    {
        private readonly IServiceProvider _services;
        private readonly IConfiguration _config;
        private readonly ILogger<RiskReviewReminderHostedService> _logger;

        public RiskReviewReminderHostedService(IServiceProvider services, IConfiguration config, ILogger<RiskReviewReminderHostedService> logger)
        {
            _services = services;
            _config = config;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var intervalHours = _config.GetValue("RiskReview:ReminderIntervalHours", 24);
            if (intervalHours <= 0) { _logger.LogInformation("Risk review reminder disabled."); return; }

            var orgIds = _config.GetSection("RiskReview:OrgIds").Get<int[]>() ?? _config.GetSection("Monitoring:SyncOrgIds").Get<int[]>() ?? new[] { 1 };
            var daysAhead = _config.GetValue("RiskReview:NotifyDaysAhead", 7);

            _logger.LogInformation("Risk review reminder started. Interval: {Hours}h, Orgs: {OrgIds}.", intervalHours, string.Join(",", orgIds));

            while (!stoppingToken.IsCancellationRequested)
            {
                using var scope = _services.CreateScope();
                var tenantFactory = scope.ServiceProvider.GetRequiredService<ITenantDbFactory>();
                var platformDb = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
                var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>>();
                var riskService = scope.ServiceProvider.GetRequiredService<RiskService>();

                foreach (var orgId in orgIds)
                {
                    try
                    {
                        await riskService.UpdateOverdueFlagsAsync(orgId, stoppingToken);
                        await using var db = await tenantFactory.CreateAsync(orgId);
                        var today = DateTime.UtcNow.Date;
                        var windowEnd = today.AddDays(daysAhead);
                        var risks = await db.Risks.AsNoTracking()
                            .Where(r => r.OrgId == orgId && r.DeletedAt == null && r.NextReviewDate.HasValue && r.NextReviewDate.Value <= windowEnd)
                            .Select(r => new { r.RiskId, r.Title, r.NextReviewDate, r.OverdueFlag, r.RiskOwnerId, r.AccountableId, r.ReportByUserId })
                            .ToListAsync(stoppingToken);
                        foreach (var risk in risks)
                        {
                            var recipientId = risk.RiskOwnerId ?? risk.AccountableId ?? risk.ReportByUserId;
                            if (string.IsNullOrEmpty(recipientId)) continue;
                            var title = risk.OverdueFlag ? "Risk review overdue" : "Risk review due";
                            var message = $"Risk '{risk.Title}' (Id: {risk.RiskId}) is " + (risk.OverdueFlag ? "overdue" : "due") + " for review" + (risk.NextReviewDate.HasValue ? $" by {risk.NextReviewDate.Value:yyyy-MM-dd}" : "") + ".";
                            db.Notifications.Add(new Data.Entities.Notification
                            {
                                OrgId = orgId,
                                UserId = recipientId,
                                Title = title,
                                Message = message.Length > 500 ? message.Substring(0, 500) : message,
                                EntityType = "Risk",
                                EntityId = risk.RiskId,
                                CreatedAt = DateTime.UtcNow
                            });
                        }
                        if (risks.Count > 0)
                            await db.SaveChangesAsync(stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Risk review reminder failed for org {OrgId}.", orgId);
                    }
                }

                await Task.Delay(TimeSpan.FromHours(intervalHours), stoppingToken);
            }
        }
    }
}
