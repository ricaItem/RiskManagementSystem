using WEB_Sentro.Data;
using WEB_Sentro.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace WEB_Sentro.Services
{
    public interface IAuditService
    {
        Task LogAsync(int orgId, string userId, string entityType, int entityId, string actionType, string? message = null, string? severity = "Info", string? ipAddress = null);
    }

    public class AuditService : IAuditService
    {
        private readonly ITenantDbFactory _tenantDbFactory;

        public AuditService(ITenantDbFactory tenantDbFactory)
        {
            _tenantDbFactory = tenantDbFactory;
        }

        public async Task LogAsync(int orgId, string userId, string entityType, int entityId, string actionType, string? message = null, string? severity = "Info", string? ipAddress = null)
        {
            if (orgId <= 0) return;

            await using var db = await _tenantDbFactory.CreateAsync(orgId);
            var log = new AuditLog
            {
                OrgId = orgId,
                UserId = userId,
                EntityType = entityType,
                EntityId = entityId,
                ActionType = actionType,
                Level = severity,
                Message = message,
                IpAddress = ipAddress,
                CreatedAt = DateTime.UtcNow
            };
            db.AuditLogs.Add(log);
            await db.SaveChangesAsync();
        }
    }
}
