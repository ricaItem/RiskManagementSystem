using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WEB_Sentro.Data;
using WEB_Sentro.Data.Entities;
using WEB_Sentro.Models.Identity;
using WEB_Sentro.Services;
using Web_Sentro.Areas.Client.Models;

namespace Web_Sentro.Areas.Client.Controllers
{
    [Area("Client")]
    [Authorize(Policy = "RiskContributors")]
    public class MitigationController : Controller
    {
        private readonly ITenantDbFactory _tenantDbFactory;
        private readonly PlatformDbContext _platformDb;
        private readonly RiskService _riskService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly INotificationService _notificationService;

        public MitigationController(ITenantDbFactory tenantDbFactory, PlatformDbContext platformDb, RiskService riskService, UserManager<ApplicationUser> userManager, INotificationService notificationService)
        {
            _tenantDbFactory = tenantDbFactory;
            _platformDb = platformDb;
            _riskService = riskService;
            _userManager = userManager;
            _notificationService = notificationService;
        }

        private async Task<ApplicationUser?> GetCurrentUserAsync() => await _userManager.GetUserAsync(User);
        private bool IsAdmin() => User.IsInRole("Admin") || User.IsInRole("SuperAdmin");
        private bool IsRiskManager() => User.IsInRole("RiskManager");
        private bool CanManageTasks() => IsAdmin() || IsRiskManager();

        private async Task<bool> IsValidAssigneeAsync(int orgId, string? assignedToUserId)
        {
            if (string.IsNullOrWhiteSpace(assignedToUserId)) return true;

            var candidate = assignedToUserId.Trim();
            return await _platformDb.Users.AsNoTracking()
                .AnyAsync(u => u.Id == candidate && u.OrganizationId == orgId && u.IsActive);
        }

        public async Task<IActionResult> Index(string filter = "active")
        {
            ViewData["Title"] = "Mitigation Planning";
            var user = await GetCurrentUserAsync();
            if (user == null) return Challenge();

            ViewBag.Filter = string.Equals(filter, "archived", StringComparison.OrdinalIgnoreCase) ? "archived" : "active";
            return View();
        }

        public async Task<IActionResult> IndexContent(string filter = "active")
        {
            ViewData["Title"] = "Mitigation Planning";
            var user = await GetCurrentUserAsync();
            if (user == null) return Challenge();

            var model = await BuildIndexModelAsync(user, filter);
            return PartialView("_IndexContent", model);
        }

        private async Task<List<MitigationRiskCardViewModel>> BuildIndexModelAsync(ApplicationUser user, string filter)
        {
            var orgId = user.OrganizationId;
            await using var db = await _tenantDbFactory.CreateAsync(orgId);

            var isArchived = string.Equals(filter, "archived", StringComparison.OrdinalIgnoreCase);
            var query = db.Risks
                .AsNoTracking()
                .Include(r => r.MitigationPlan)
                .Where(r => r.OrgId == orgId && r.Status == "MitigationRequired" && r.DeletedAt == null);

            query = isArchived
                ? query.Where(r => r.MitigationPlan != null && r.MitigationPlan.DeletedAt != null)
                : query.Where(r => r.MitigationPlan == null || r.MitigationPlan.DeletedAt == null);

            var risks = await query
                .OrderByDescending(r => r.UpdatedAt ?? r.CreatedAt)
                .Select(r => new
                {
                    r.RiskId,
                    PlanId = r.MitigationPlan != null ? r.MitigationPlan.PlanId : 0,
                    r.Title,
                    r.Category,
                    r.Priority,
                    r.CreatedAt,
                    IsArchived = r.MitigationPlan != null && r.MitigationPlan.DeletedAt != null
                })
                .ToListAsync();

            var planIds = risks.Where(r => r.PlanId > 0).Select(r => r.PlanId).Distinct().ToList();
            var planProgress = new Dictionary<int, (int Percent, List<string> AssigneeIds)>();
            if (planIds.Count > 0)
            {
                var tasks = await db.MitigationTasks
                    .AsNoTracking()
                    .Where(t => planIds.Contains(t.PlanId))
                    .Select(t => new { t.PlanId, t.Status, t.AssignedToUserId })
                    .ToListAsync();

                foreach (var planId in planIds)
                {
                    var planTasks = tasks.Where(t => t.PlanId == planId).ToList();
                    var total = planTasks.Count;
                    var done = planTasks.Count(t => t.Status == "Done");
                    var percent = total > 0 ? (int)Math.Round((double)done / total * 100) : 0;
                    var assigneeIds = planTasks
                        .Where(t => !string.IsNullOrEmpty(t.AssignedToUserId))
                        .Select(t => t.AssignedToUserId!)
                        .Distinct()
                        .ToList();
                    planProgress[planId] = (percent, assigneeIds);
                }
            }

            var allAssigneeIds = planProgress.Values.SelectMany(p => p.AssigneeIds).Distinct().ToList();
            var userDisplayNames = new Dictionary<string, string>();
            var userProfilePaths = new Dictionary<string, string?>();
            if (allAssigneeIds.Count > 0)
            {
                var users = await _platformDb.Users.AsNoTracking()
                    .Where(u => allAssigneeIds.Contains(u.Id))
                    .Select(u => new { u.Id, DisplayName = u.FirstName + " " + u.LastName, u.ProfileImagePath })
                    .ToListAsync();
                foreach (var u in users)
                {
                    userDisplayNames[u.Id] = (u.DisplayName ?? "").Trim();
                    userProfilePaths[u.Id] = u.ProfileImagePath;
                }
            }

            var model = risks.Select(r =>
            {
                var (percent, assigneeIds) = r.PlanId > 0 && planProgress.TryGetValue(r.PlanId, out var pp) ? pp : (0, new List<string>());
                var displayNames = assigneeIds
                    .Where(id => userDisplayNames.TryGetValue(id, out var name) && !string.IsNullOrEmpty(name))
                    .Select(id => userDisplayNames[id])
                    .Distinct()
                    .ToList();
                var assignedUsers = assigneeIds
                    .Where(id => userDisplayNames.TryGetValue(id, out var name) && !string.IsNullOrEmpty(name))
                    .Select(id => new MitigationAssigneeAvatarViewModel
                    {
                        DisplayName = userDisplayNames[id],
                        ProfileImagePath = userProfilePaths.TryGetValue(id, out var path) ? path : null
                    })
                    .DistinctBy(x => x.DisplayName)
                    .ToList();
                return new MitigationRiskCardViewModel
                {
                    RiskId = r.RiskId,
                    PlanId = r.PlanId,
                    Title = r.Title ?? "",
                    Category = r.Category,
                    Priority = r.Priority,
                    CreatedAt = r.CreatedAt,
                    IsArchived = r.IsArchived,
                    ProgressPercent = percent,
                    AssignedToDisplayNames = displayNames,
                    AssignedUsers = assignedUsers
                };
            }).ToList();

            ViewBag.Filter = isArchived ? "archived" : "active";
            return model;
        }

        public async Task<IActionResult> Board(int riskId)
        {
            ViewData["Title"] = "Mitigation Board";
            var user = await GetCurrentUserAsync();
            if (user == null) return Challenge();

            var orgId = user.OrganizationId;
            await using var db = await _tenantDbFactory.CreateAsync(orgId);

            var risk = await db.Risks.AsNoTracking().FirstOrDefaultAsync(r => r.RiskId == riskId && r.OrgId == orgId);
            if (risk == null) return NotFound();

            await _riskService.EnsureMitigationPlanExistsAsync(riskId, orgId, user.Id);

            var plan = await db.MitigationPlans
                .AsNoTracking()
                .Include(p => p.Risk)
                .FirstOrDefaultAsync(p => p.RiskId == riskId);
            if (plan == null)
                return RedirectToAction("Identification", "Risks", new { area = "Client" });
            if (plan.DeletedAt != null)
                return RedirectToAction(nameof(Index), new { archived = 1 });

            var tasks = await db.MitigationTasks
                .AsNoTracking()
                .Where(t => t.PlanId == plan.PlanId)
                .Select(t => new { t.TaskId, t.Title, t.Status, t.DueDate, t.AssignedToUserId, t.ProgressPercent, Priority = plan.Risk.Priority })
                .ToListAsync();

            var userIds = tasks.Where(t => t.AssignedToUserId != null).Select(t => t.AssignedToUserId!).Distinct().ToList();
            var userDisplayNames = new Dictionary<string, string>();
            var userProfilePaths = new Dictionary<string, string?>();
            if (userIds.Count > 0)
            {
                var users = await _platformDb.Users.AsNoTracking()
                    .Where(u => userIds.Contains(u.Id))
                    .Select(u => new { u.Id, u.FirstName, u.LastName, u.ProfileImagePath })
                    .ToListAsync();
                foreach (var u in users)
                {
                    userDisplayNames[u.Id] = $"{u.FirstName} {u.LastName}".Trim();
                    userProfilePaths[u.Id] = u.ProfileImagePath;
                }
            }

            var model = tasks.Select(t => new MitigationTaskViewModel
            {
                Id = t.TaskId,
                RiskId = riskId,
                Title = t.Title,
                Status = t.Status,
                DueDate = t.DueDate,
                AssignedToUserId = t.AssignedToUserId,
                AssignedTo = t.AssignedToUserId != null && userDisplayNames.TryGetValue(t.AssignedToUserId, out var name) ? name : "—",
                AssignedToProfileImagePath = t.AssignedToUserId != null && userProfilePaths.TryGetValue(t.AssignedToUserId, out var profilePath) ? profilePath : null,
                Priority = t.Priority ?? "Unassessed",
                ProgressPercent = t.ProgressPercent
            }).ToList();

            var orgUsers = await _platformDb.Users.AsNoTracking()
                .Where(u => u.OrganizationId == orgId)
                .Select(u => new { u.Id, DisplayName = u.FirstName + " " + u.LastName })
                .ToListAsync();
            ViewBag.RiskId = riskId;
            ViewBag.PlanId = plan.PlanId;
            ViewBag.RiskStatus = risk.Status;
            ViewBag.RiskTitle = risk.Title;
            ViewBag.OrgUsers = orgUsers;
            ViewBag.IsAdmin = IsAdmin();
            ViewBag.CanManageTasks = CanManageTasks();
            ViewBag.CurrentUserId = user.Id;
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "RiskGovernance")]
        public async Task<IActionResult> SoftDeletePlan(int planId)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Challenge();
            if (!CanManageTasks()) return Json(new { ok = false });

            await using var db = await _tenantDbFactory.CreateAsync(user.OrganizationId);
            var plan = await db.MitigationPlans.Include(p => p.Risk).FirstOrDefaultAsync(p => p.PlanId == planId);
            if (plan == null || plan.Risk.OrgId != user.OrganizationId) return Json(new { ok = false });
            if (plan.DeletedAt != null) return Json(new { ok = true });
            plan.DeletedAt = DateTime.UtcNow;
            plan.Status = "Archived";
            await db.SaveChangesAsync();
            _riskService.AddAuditLog(db, plan.Risk.OrgId, user.Id, "MitigationPlan", plan.PlanId, "PlanArchived", "Mitigation plan archived", HttpContext.Connection.RemoteIpAddress?.ToString());
            await db.SaveChangesAsync();
            return Json(new { ok = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "RiskGovernance")]
        public async Task<IActionResult> DeleteTask(int taskId)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Challenge();
            if (!CanManageTasks()) return Json(new { ok = false });

            await using var db = await _tenantDbFactory.CreateAsync(user.OrganizationId);
            var task = await db.MitigationTasks
                .Include(t => t.Plan)
                .ThenInclude(p => p!.Risk)
                .FirstOrDefaultAsync(t => t.TaskId == taskId);
            if (task == null || task.Plan.Risk.OrgId != user.OrganizationId) return Json(new { ok = false });
            db.MitigationTasks.Remove(task);
            await db.SaveChangesAsync();
            _riskService.AddAuditLog(db, task.Plan.Risk.OrgId, user.Id, "MitigationTask", task.TaskId, "TaskDeleted", $"Task deleted: {task.Title}", HttpContext.Connection.RemoteIpAddress?.ToString());
            await db.SaveChangesAsync();
            return Json(new { ok = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MoveTask(int taskId, string newStatus)
        {
            var allowed = new[] { "ToDo", "InProgress", "Review", "Done" };
            if (string.IsNullOrWhiteSpace(newStatus) || !allowed.Contains(newStatus, StringComparer.OrdinalIgnoreCase))
                return BadRequest();

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            await using var db = await _tenantDbFactory.CreateAsync(user.OrganizationId);

            var task = await db.MitigationTasks
                .Include(t => t.Plan)
                .ThenInclude(p => p!.Risk)
                .FirstOrDefaultAsync(t => t.TaskId == taskId);
            if (task == null) return NotFound();
            if (task.Plan.Risk.OrgId != user.OrganizationId) return Forbid();
            if (!CanManageTasks() && !string.Equals(task.AssignedToUserId, user.Id, StringComparison.OrdinalIgnoreCase)) return Forbid();

            var previousStatus = task.Status;
            task.Status = newStatus;
            task.UpdatedAt = DateTime.UtcNow;
            _riskService.AddAuditLog(db, task.Plan.Risk.OrgId, user.Id, "MitigationTask", task.TaskId, "TaskMoved", $"Status {previousStatus} → {newStatus}", HttpContext.Connection.RemoteIpAddress?.ToString());
            await db.SaveChangesAsync();

            var planId = task.PlanId;
            var allDone = await db.MitigationTasks
                .Where(t => t.PlanId == planId)
                .AllAsync(t => t.Status == "Done");
            if (allDone)
            {
                var risk = task.Plan.Risk;
                // All tasks done = ready for residual review, not auto-close
                if (risk.Status == "MitigationRequired")
                {
                    risk.Status = "Monitoring";
                    risk.UpdatedAt = DateTime.UtcNow;
                    _riskService.AddAuditLog(db, risk.OrgId, user.Id, "Risk", risk.RiskId, "RiskReadyForResidualAssessment", "All mitigation tasks completed. Ready for residual assessment.", HttpContext.Connection.RemoteIpAddress?.ToString());
                    await db.SaveChangesAsync();
                    await _notificationService.NotifyRiskEventAsync(risk.OrgId, "MitigationCompleted", risk.RiskId, "Mitigation completed", $"Risk '{risk.Title}' mitigation tasks are complete. Please perform residual assessment.", risk.ReportByUserId);
                }
            }

            return Json(new { ok = true });
        }

        [HttpGet]
        public async Task<IActionResult> TaskDetails(int taskId)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Challenge();

            await using var db = await _tenantDbFactory.CreateAsync(user.OrganizationId);

            var task = await db.MitigationTasks
                .AsNoTracking()
                .Include(t => t.Plan)
                .ThenInclude(p => p!.Risk)
                .FirstOrDefaultAsync(t => t.TaskId == taskId);
            if (task == null) return NotFound();
            if (task.Plan.Risk.OrgId != user.OrganizationId) return Forbid();

            return Json(new
            {
                task.TaskId,
                task.Title,
                task.AssignedToUserId,
                task.DueDate,
                task.Description,
                task.ProgressPercent,
                task.Status
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateTask(int taskId, string? title, string? assignedToUserId, string? dueDate, string? description, int? progressPercent, string? status)
        {
            if (taskId <= 0) return BadRequest();

            var user = await GetCurrentUserAsync();
            if (user == null) return Challenge();

            await using var db = await _tenantDbFactory.CreateAsync(user.OrganizationId);

            var task = await db.MitigationTasks
                .Include(t => t.Plan)
                .ThenInclude(p => p!.Risk)
                .FirstOrDefaultAsync(t => t.TaskId == taskId);
            if (task == null) return NotFound();
            if (task.Plan.Risk.OrgId != user.OrganizationId) return Forbid();
            var allowedStatuses = new[] { "ToDo", "InProgress", "Review", "Done" };
            var canManage = CanManageTasks();
            var previousAssigneeId = task.AssignedToUserId;
            if (!canManage && !string.Equals(task.AssignedToUserId, user.Id, StringComparison.OrdinalIgnoreCase)) return Forbid();

            if (canManage)
            {
                if (!string.IsNullOrWhiteSpace(title))
                    task.Title = title.Trim();

                if (!string.IsNullOrWhiteSpace(status) && allowedStatuses.Contains(status, StringComparer.OrdinalIgnoreCase))
                    task.Status = status;

                var normalizedAssignee = string.IsNullOrWhiteSpace(assignedToUserId) ? null : assignedToUserId.Trim();
                if (!await IsValidAssigneeAsync(user.OrganizationId, normalizedAssignee))
                    return BadRequest(new { ok = false, message = "Invalid assignee." });

                task.AssignedToUserId = normalizedAssignee;
                task.DueDate = !string.IsNullOrWhiteSpace(dueDate) && DateTime.TryParse(dueDate, out var d) ? d : (DateTime?)null;
                task.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(status) && allowedStatuses.Contains(status, StringComparer.OrdinalIgnoreCase))
                    task.Status = status;

                task.Description = string.IsNullOrWhiteSpace(description) ? task.Description : description.Trim();
            }

            task.ProgressPercent = progressPercent.HasValue ? Math.Clamp(progressPercent.Value, 0, 100) : task.ProgressPercent;
            task.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            if (canManage
                && !string.IsNullOrWhiteSpace(task.AssignedToUserId)
                && !string.Equals(previousAssigneeId, task.AssignedToUserId, StringComparison.OrdinalIgnoreCase))
            {
                await _notificationService.NotifyMitigationTaskAssignmentAsync(
                    task.Plan.Risk.OrgId,
                    task.Plan.Risk.RiskId,
                    task.TaskId,
                    task.Title,
                    task.AssignedToUserId,
                    user.Id);
            }

            return Json(new { ok = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "RiskGovernance")]
        public async Task<IActionResult> CreateTask(int planId, string title, string? description, string? assignedToUserId, string? dueDate)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Challenge();
            if (!CanManageTasks()) return Forbid();

            await using var db = await _tenantDbFactory.CreateAsync(user.OrganizationId);

            var plan = await db.MitigationPlans
                .Include(p => p.Risk)
                .FirstOrDefaultAsync(p => p.PlanId == planId);
            if (plan == null || plan.Risk.OrgId != user.OrganizationId) return NotFound();

            if (string.IsNullOrWhiteSpace(title)) return BadRequest();

            var normalizedAssignee = string.IsNullOrWhiteSpace(assignedToUserId) ? null : assignedToUserId.Trim();
            if (!await IsValidAssigneeAsync(user.OrganizationId, normalizedAssignee))
                return BadRequest(new { ok = false, message = "Invalid assignee." });

            var task = new MitigationTask
            {
                PlanId = planId,
                Title = title.Trim(),
                Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
                AssignedToUserId = normalizedAssignee,
                DueDate = !string.IsNullOrWhiteSpace(dueDate) && DateTime.TryParse(dueDate, out var d) ? d : (DateTime?)null,
                Status = "ToDo",
                ProgressPercent = 0,
                UpdatedAt = DateTime.UtcNow
            };
            db.MitigationTasks.Add(task);
            await db.SaveChangesAsync();

            if (!string.IsNullOrWhiteSpace(task.AssignedToUserId))
            {
                await _notificationService.NotifyMitigationTaskAssignmentAsync(
                    plan.Risk.OrgId,
                    plan.Risk.RiskId,
                    task.TaskId,
                    task.Title,
                    task.AssignedToUserId,
                    user.Id);
            }

            _riskService.AddAuditLog(db, plan.Risk.OrgId, user.Id, "MitigationTask", task.TaskId, "TaskCreated", task.Title, HttpContext.Connection.RemoteIpAddress?.ToString());
            await db.SaveChangesAsync();

            return Json(new { ok = true, taskId = task.TaskId, title = task.Title, status = task.Status, dueDateFormatted = task.DueDate?.ToString("MMM d") ?? "N/A", progressPercent = 0 });
        }
    }
}
