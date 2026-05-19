using WEB_Sentro.Data;
using WEB_Sentro.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using System.Text.RegularExpressions;
using System.Linq;

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

            // Resolve IP: prefer explicit ipAddress param, then standard proxy headers, then connection remote address
            string? resolvedIp = null;
            if (!string.IsNullOrWhiteSpace(ipAddress))
            {
                resolvedIp = ipAddress;
            }
            else
            {
                var http = _httpContextAccessor.HttpContext;
                if (http != null)
                {
                    // X-Forwarded-For may contain a comma separated list; first value is the client
                    if (http.Request.Headers.TryGetValue("X-Forwarded-For", out StringValues xff) && !StringValues.IsNullOrEmpty(xff))
                    {
                        resolvedIp = xff.ToString().Split(',').Select(s => s.Trim()).FirstOrDefault();
                    }
                    // RFC 7239 Forwarded header: look for for=...
                    else if (http.Request.Headers.TryGetValue("Forwarded", out StringValues fwd) && !StringValues.IsNullOrEmpty(fwd))
                    {
                        var m = Regex.Match(fwd.ToString(), @"for=(?:\""(?<ip>[^\""]+)\""|(?<ip>[^;,\s]+))", RegexOptions.IgnoreCase);
                        if (m.Success) resolvedIp = m.Groups["ip"].Value;
                    }
                    else
                    {
                        resolvedIp = http.Connection.RemoteIpAddress?.ToString();
                    }
                }
            }

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
