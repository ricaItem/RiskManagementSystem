using Microsoft.EntityFrameworkCore;
using WEB_Sentro.Data;
using WEB_Sentro.Data.Entities;
using Web_Sentro.Areas.Client.Models;
using System.Collections.Generic;

namespace WEB_Sentro.Services
{
    public class IncidentService : IIncidentService
    {
        private readonly ITenantDbFactory _tenantDbFactory;
        private readonly IAuditService _auditService;

        public IncidentService(ITenantDbFactory tenantDbFactory, IAuditService auditService)
        {
            _tenantDbFactory = tenantDbFactory;
            _auditService = auditService;
        }

        public async Task<List<Incident>> GetIncidentsAsync(int orgId, int? siteId, string? status, DateTime? startDate, DateTime? endDate)
        {
            await using var db = await _tenantDbFactory.CreateAsync(orgId);
            var query = db.Incidents.AsNoTracking()
                .Include(i => i.Site)
                .Where(i => i.OrgId == orgId);

            if (siteId.HasValue)
                query = query.Where(i => i.SiteId == siteId.Value);

            if (!string.IsNullOrEmpty(status))
                query = query.Where(i => i.Status == status);

            if (startDate.HasValue)
                query = query.Where(i => i.IncidentDate >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(i => i.IncidentDate <= endDate.Value);

            return await query.OrderByDescending(i => i.IncidentDate).ToListAsync();
        }

        public async Task<Incident?> GetIncidentByIdAsync(int incidentId, int orgId)
        {
            await using var db = await _tenantDbFactory.CreateAsync(orgId);
            return await db.Incidents.AsNoTracking()
                .Include(i => i.Site)
                .FirstOrDefaultAsync(i => i.IncidentId == incidentId && i.OrgId == orgId);
        }

        public async Task<Incident> CreateIncidentAsync(Incident incident)
        {
            await using var db = await _tenantDbFactory.CreateAsync(incident.OrgId);
            incident.ReportedAt = DateTime.UtcNow;
            incident.UpdatedAt = DateTime.UtcNow;
            
            db.Incidents.Add(incident);
            await db.SaveChangesAsync();

            await _auditService.LogAsync(incident.OrgId, incident.ReportedByUserId, "Incident", incident.IncidentId, "IncidentCreated", $"Incident reported: {incident.Title}", "Info", null);
            
            return incident;
        }

        public async Task<Incident> UpdateIncidentAsync(Incident incident, string userId)
        {
            await using var db = await _tenantDbFactory.CreateAsync(incident.OrgId);
            var existing = await db.Incidents.FirstOrDefaultAsync(i => i.IncidentId == incident.IncidentId && i.OrgId == incident.OrgId);
            
            if (existing == null) throw new KeyNotFoundException("Incident not found");

            existing.Title = incident.Title;
            existing.Description = incident.Description;
            existing.IncidentDate = incident.IncidentDate;
            existing.Type = incident.Type;
            existing.Severity = incident.Severity;
            existing.Status = incident.Status;
            existing.RootCause = incident.RootCause;
            existing.CorrectiveActions = incident.CorrectiveActions;
            existing.WeatherConditions = incident.WeatherConditions;
            existing.SiteId = incident.SiteId;
            existing.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync();

            await _auditService.LogAsync(incident.OrgId, userId, "Incident", incident.IncidentId, "IncidentUpdated", $"Incident updated: {incident.Title}", "Info", null);

            return existing;
        }

        public async Task DeleteIncidentAsync(int incidentId, int orgId, string userId)
        {
            await using var db = await _tenantDbFactory.CreateAsync(orgId);
            var existing = await db.Incidents.FirstOrDefaultAsync(i => i.IncidentId == incidentId && i.OrgId == orgId);
            
            if (existing != null)
            {
                existing.DeletedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();

                await _auditService.LogAsync(orgId, userId, "Incident", incidentId, "IncidentDeleted", $"Incident deleted: {existing.Title}", "Warning", null);
            }
        }

        public async Task<(int Open, int Total)> GetIncidentStatsAsync(int orgId, DateTime? startDate = null, DateTime? endDate = null, int? siteId = null)
        {
            await using var db = await _tenantDbFactory.CreateAsync(orgId);
            var query = db.Incidents.AsNoTracking().Where(i => i.OrgId == orgId && i.DeletedAt == null);

            if (siteId.HasValue)
                query = query.Where(i => i.SiteId == siteId.Value);

            if (startDate.HasValue)
                query = query.Where(i => i.IncidentDate >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(i => i.IncidentDate <= endDate.Value);

            var open = await query.CountAsync(i => i.Status != "Closed");
            var total = await query.CountAsync();
            return (open, total);
        }

        public async Task<Dictionary<int, (int Open, int Total)>> GetIncidentStatsBySiteAsync(int orgId)
        {
            await using var db = await _tenantDbFactory.CreateAsync(orgId);
            
            var query = await db.Incidents.AsNoTracking()
                .Where(i => i.OrgId == orgId && i.DeletedAt == null)
                .GroupBy(i => i.SiteId)
                .Select(g => new 
                { 
                    SiteId = g.Key, 
                    Open = g.Count(i => i.Status != "Closed"), 
                    Total = g.Count() 
                })
                .ToListAsync();

            return query.ToDictionary(k => k.SiteId, v => (v.Open, v.Total));
        }
    }
}
