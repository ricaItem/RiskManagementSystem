using Microsoft.EntityFrameworkCore;
using WEB_Sentro.Data;
using WEB_Sentro.Data.Entities;
using System.Linq.Expressions;

namespace WEB_Sentro.Services
{
    public class ProjectService
    {
        private readonly ITenantDbFactory _tenantDbFactory;
        private readonly PlatformDbContext _platformDb;

        public ProjectService(ITenantDbFactory tenantDbFactory, PlatformDbContext platformDb)
        {
            _tenantDbFactory = tenantDbFactory;
            _platformDb = platformDb;
        }

        // --- Projects ---

        public async Task<List<Project>> GetProjectsAsync(int orgId, string? status, string? search, int? siteId, CancellationToken ct = default)
        {
            await using var db = await _tenantDbFactory.CreateAsync(orgId);
            var q = db.Projects.AsNoTracking().Where(p => p.OrgId == orgId);

            if (!string.IsNullOrEmpty(status))
                q = q.Where(p => p.Status == status);

            if (!string.IsNullOrEmpty(search))
                q = q.Where(p => p.Name.Contains(search) || p.ProjectCode.Contains(search));

            if (siteId.HasValue)
                q = q.Where(p => p.SiteId == siteId.Value);

            return await q.OrderByDescending(p => p.CreatedAt).ToListAsync(ct);
        }

        public async Task<Project?> GetProjectByIdAsync(int orgId, int projectId, CancellationToken ct = default)
        {
            await using var db = await _tenantDbFactory.CreateAsync(orgId);
            return await db.Projects
                .Include(p => p.Site)
                .Include(p => p.Tasks)
                .Include(p => p.Risks)
                .Include(p => p.PurchaseOrders)
                .Include(p => p.Incidents)
                .Include(p => p.Expenses)
                .FirstOrDefaultAsync(p => p.OrgId == orgId && p.ProjectId == projectId, ct);
        }

        public async Task<Project> CreateProjectAsync(int orgId, string userId, Project project, CancellationToken ct = default)
        {
            await using var db = await _tenantDbFactory.CreateAsync(orgId);
            project.OrgId = orgId;
            project.CreatedAt = DateTime.UtcNow;
            project.CreatedByUserId = userId;
            project.UpdatedAt = DateTime.UtcNow;
            project.UpdatedByUserId = userId;
            
            db.Projects.Add(project);
            await db.SaveChangesAsync(ct);
            return project;
        }

        public async Task<bool> UpdateProjectAsync(int orgId, int projectId, string userId, string name, string? description, string status, DateTime? startDate, DateTime? endDate, decimal? budget, string? managerId, int? siteId, CancellationToken ct = default)
        {
            await using var db = await _tenantDbFactory.CreateAsync(orgId);
            var project = await db.Projects.FirstOrDefaultAsync(p => p.OrgId == orgId && p.ProjectId == projectId, ct);
            if (project == null) return false;

            project.Name = name;
            project.Description = description;
            project.Status = status;
            project.StartDate = startDate;
            project.EndDate = endDate;
            project.Budget = budget;
            project.ManagerUserId = managerId;
            project.SiteId = siteId;
            project.UpdatedAt = DateTime.UtcNow;
            project.UpdatedByUserId = userId;

            await db.SaveChangesAsync(ct);
            return true;
        }

        public async Task<bool> DeleteProjectAsync(int orgId, int projectId, string userId, CancellationToken ct = default)
        {
            await using var db = await _tenantDbFactory.CreateAsync(orgId);
            var project = await db.Projects.FirstOrDefaultAsync(p => p.OrgId == orgId && p.ProjectId == projectId, ct);
            if (project == null) return false;

            project.DeletedAt = DateTime.UtcNow;
            project.UpdatedAt = DateTime.UtcNow;
            project.UpdatedByUserId = userId;

            await db.SaveChangesAsync(ct);
            return true;
        }

        // --- Tasks (WBS) ---

        public async Task<List<ProjectTask>> GetProjectTasksAsync(int orgId, int projectId, CancellationToken ct = default)
        {
            await using var db = await _tenantDbFactory.CreateAsync(orgId);
            return await db.ProjectTasks.AsNoTracking()
                .Where(t => t.ProjectId == projectId)
                .OrderBy(t => t.SortOrder)
                .ToListAsync(ct);
        }

        public async Task<ProjectTask> CreateTaskAsync(int orgId, string userId, ProjectTask task, CancellationToken ct = default)
        {
            await using var db = await _tenantDbFactory.CreateAsync(orgId);
            
            // Auto-calculate sort order if not provided
            if (task.SortOrder == 0)
            {
                var maxOrder = await db.ProjectTasks
                    .Where(t => t.ProjectId == task.ProjectId && t.ParentTaskId == task.ParentTaskId)
                    .MaxAsync(t => (int?)t.SortOrder, ct) ?? 0;
                task.SortOrder = maxOrder + 10;
            }

            task.CreatedAt = DateTime.UtcNow;
            task.CreatedByUserId = userId;
            task.UpdatedAt = DateTime.UtcNow;
            task.UpdatedByUserId = userId;

            db.ProjectTasks.Add(task);
            await db.SaveChangesAsync(ct);
            return task;
        }

        public async Task<bool> UpdateTaskAsync(int orgId, int taskId, string userId, string title, string? description, string status, int percentComplete, DateTime? start, DateTime? end, string? assignedTo, decimal? budget, CancellationToken ct = default)
        {
            await using var db = await _tenantDbFactory.CreateAsync(orgId);
            var task = await db.ProjectTasks.FirstOrDefaultAsync(t => t.ProjectTaskId == taskId, ct);
            if (task == null) return false;
            
            // Validate project belongs to org
            var project = await db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.ProjectId == task.ProjectId && p.OrgId == orgId, ct);
            if (project == null) return false;

            task.Title = title;
            task.Description = description;
            task.Status = status;
            task.PercentComplete = percentComplete;
            task.StartDate = start;
            task.EndDate = end;
            task.AssignedToUserId = assignedTo;
            task.Budget = budget;
            task.UpdatedAt = DateTime.UtcNow;
            task.UpdatedByUserId = userId;

            // Rollup logic could go here (updating parent tasks)

            await db.SaveChangesAsync(ct);
            return true;
        }

        public async Task<bool> DeleteTaskAsync(int orgId, int taskId, CancellationToken ct = default)
        {
            await using var db = await _tenantDbFactory.CreateAsync(orgId);
            var task = await db.ProjectTasks.FirstOrDefaultAsync(t => t.ProjectTaskId == taskId, ct);
            if (task == null) return false;

             // Validate project belongs to org
            var project = await db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.ProjectId == task.ProjectId && p.OrgId == orgId, ct);
            if (project == null) return false;

            db.ProjectTasks.Remove(task); // Hard delete for tasks usually, or soft delete if needed
            await db.SaveChangesAsync(ct);
            return true;
        }

        // --- Intelligence ---

        public async Task<object> GetProjectStatsAsync(int orgId, int projectId, CancellationToken ct = default)
        {
            await using var db = await _tenantDbFactory.CreateAsync(orgId);
            
            var risks = await db.Risks.AsNoTracking().Where(r => r.ProjectId == projectId && r.DeletedAt == null).ToListAsync(ct);
            var expenses = await db.Expenses.AsNoTracking().Where(e => e.ProjectId == projectId).SumAsync(e => e.Amount, ct);
            var pos = await db.PurchaseOrders.AsNoTracking().Where(p => p.ProjectId == projectId).ToListAsync(ct);
            var tasks = await db.ProjectTasks.AsNoTracking().Where(t => t.ProjectId == projectId).ToListAsync(ct);

            var totalTasks = tasks.Count;
            var completedTasks = tasks.Count(t => t.Status == "Completed");
            var progress = totalTasks > 0 ? (double)tasks.Sum(t => t.PercentComplete) / totalTasks : 0;

            return new
            {
                RiskCount = risks.Count,
                HighRiskCount = risks.Count(r => r.Priority == "High" || r.Priority == "Critical"),
                TotalExpenses = expenses,
                OpenPOCount = pos.Count(p => p.Status != "Received" && p.Status != "Cancelled"),
                Progress = Math.Round(progress, 1),
                TaskCount = totalTasks
            };
        }
    }
}
