using WEB_Sentro.Data;
using WEB_Sentro.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;

namespace WEB_Sentro.Services
{
    public interface IAuditService
    {
        Task LogAsync(int orgId, string userId, string entityType, int entityId, string actionType, string? message = null, string? severity = "Info", string? ipAddress = null, string? category = null);
    }

    public class AuditService : IAuditService
    {
        private readonly ITenantDbFactory _tenantDbFactory;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AuditService(ITenantDbFactory tenantDbFactory, IHttpContextAccessor httpContextAccessor)
        {
            _tenantDbFactory = tenantDbFactory;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task LogAsync(int orgId, string userId, string entityType, int entityId, string actionType, string? message = null, string? severity = "Info", string? ipAddress = null, string? category = null)
        {
            if (orgId <= 0) return;

            await using var db = await _tenantDbFactory.CreateAsync(orgId);
            var resolvedCategory = string.IsNullOrWhiteSpace(category)
                ? AuditLogClassifier.DetermineCategory(entityType, actionType)
                : category;
            var resolvedIp = string.IsNullOrWhiteSpace(ipAddress)
                ? _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString()
                : ipAddress;

            var log = new AuditLog
            {
                OrgId = orgId,
                UserId = userId,
                EntityType = entityType,
                EntityId = entityId,
                ActionType = actionType,
                Level = severity,
                Message = string.IsNullOrWhiteSpace(message) ? $"[{resolvedCategory}]" : $"[{resolvedCategory}] {message}",
                IpAddress = AuditLogClassifier.NormalizeIp(resolvedIp),
                CreatedAt = DateTime.UtcNow
            };
            db.AuditLogs.Add(log);
            await db.SaveChangesAsync();
        }
    }
}
