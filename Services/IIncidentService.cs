using WEB_Sentro.Data.Entities;
using Web_Sentro.Areas.Client.Models; // I might need this for ViewModels later, but for now entities are fine.

namespace WEB_Sentro.Services
{
    public interface IIncidentService
    {
        Task<List<Incident>> GetIncidentsAsync(int orgId, int? siteId, string? status, DateTime? startDate, DateTime? endDate);
        Task<Incident?> GetIncidentByIdAsync(int incidentId, int orgId);
        Task<Incident> CreateIncidentAsync(Incident incident);
        Task<Incident> UpdateIncidentAsync(Incident incident, string userId);
        Task DeleteIncidentAsync(int incidentId, int orgId, string userId);
        Task<(int Open, int Total)> GetIncidentStatsAsync(int orgId, DateTime? startDate = null, DateTime? endDate = null, int? siteId = null);
        Task<Dictionary<int, (int Open, int Total)>> GetIncidentStatsBySiteAsync(int orgId);
    }
}
