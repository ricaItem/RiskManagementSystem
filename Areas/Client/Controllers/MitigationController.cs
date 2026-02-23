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
    [Authorize]
    public class MitigationController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly RiskService _riskService;
        private readonly UserManager<ApplicationUser> _userManager;

        public MitigationController(ApplicationDbContext db, RiskService riskService, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _riskService = riskService;
            _userManager = userManager;
        }

        private async Task<ApplicationUser?> GetCurrentUserAsync() => await _userManager.GetUserAsync(User);

        public async Task<IActionResult> Index()
        {
            ViewData["Title"] = "Mitigation Planning";
            var user = await GetCurrentUserAsync();
            if (user == null) return Challenge();

            var orgId = user.OrganizationId;
            var risks = await _db.Risks
                .AsNoTracking()
                .Where(r => r.OrgId == orgId && r.Status == "MitigationRequired" && r.DeletedAt == null)
                .OrderByDescending(r => r.UpdatedAt ?? r.CreatedAt)
                .Select(r => new MitigationRiskCardViewModel
                {
                    RiskId = r.RiskId,
                    Title = r.Title,
                    Category = r.Category,
                    Priority = r.Priority,
                    CreatedAt = r.CreatedAt
                })
                .ToListAsync();

            return View(risks);
        }

        public async Task<IActionResult> Board(int riskId)
        {
            ViewData["Title"] = "Mitigation Board";
            var user = await GetCurrentUserAsync();
            if (user == null) return Challenge();

            var orgId = user.OrganizationId;
            var risk = await _db.Risks.AsNoTracking().FirstOrDefaultAsync(r => r.RiskId == riskId && r.OrgId == orgId);
            if (risk == null) return NotFound();

            await _riskService.EnsureMitigationPlanExistsAsync(riskId, orgId, user.Id);
            await _riskService.SaveChangesAsync();

            var plan = await _db.MitigationPlans
                .AsNoTracking()
                .Include(p => p.Risk)
                .FirstOrDefaultAsync(p => p.RiskId == riskId);
            if (plan == null)
                return RedirectToAction("Identification", "Risks", new { area = "Client" });

            var tasks = await _db.MitigationTasks
                .AsNoTracking()
                .Where(t => t.PlanId == plan.PlanId)
                .Select(t => new { t.TaskId, t.Title, t.Status, t.DueDate, t.AssignedToUserId, t.ProgressPercent, Priority = plan.Risk.Priority })
                .ToListAsync();

            var userIds = tasks.Where(t => t.AssignedToUserId != null).Select(t => t.AssignedToUserId!).Distinct().ToList();
            var userDisplayNames = new Dictionary<string, string>();
            if (userIds.Count > 0)
            {
                var users = await _db.Users.AsNoTracking()
                    .Where(u => userIds.Contains(u.Id))
                    .Select(u => new { u.Id, u.FirstName, u.LastName })
                    .ToListAsync();
                foreach (var u in users)
                    userDisplayNames[u.Id] = $"{u.FirstName} {u.LastName}".Trim();
            }

            var model = tasks.Select(t => new MitigationTaskViewModel
            {
                Id = t.TaskId,
                RiskId = riskId,
                Title = t.Title,
                Status = t.Status,
                DueDate = t.DueDate,
                AssignedTo = t.AssignedToUserId != null && userDisplayNames.TryGetValue(t.AssignedToUserId, out var name) ? name : "—",
                Priority = t.Priority ?? "Unassessed",
                ProgressPercent = t.ProgressPercent
            }).ToList();

            var orgUsers = await _db.Users.AsNoTracking()
                .Where(u => u.OrganizationId == orgId)
                .Select(u => new { u.Id, DisplayName = u.FirstName + " " + u.LastName })
                .ToListAsync();
            ViewBag.RiskId = riskId;
            ViewBag.PlanId = plan.PlanId;
            ViewBag.RiskStatus = risk.Status;
            ViewBag.OrgUsers = orgUsers;
            return View(model);
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

            var task = await _db.MitigationTasks
                .Include(t => t.Plan)
                .ThenInclude(p => p!.Risk)
                .FirstOrDefaultAsync(t => t.TaskId == taskId);
            if (task == null) return NotFound();
            if (task.Plan.Risk.OrgId != user.OrganizationId) return Forbid();

            var previousStatus = task.Status;
            task.Status = newStatus;
            task.UpdatedAt = DateTime.UtcNow;
            _riskService.AddAuditLog(task.Plan.Risk.OrgId, user.Id, "MitigationTask", task.TaskId, "TaskMoved", $"Status {previousStatus} → {newStatus}", HttpContext.Connection.RemoteIpAddress?.ToString());
            await _db.SaveChangesAsync();

            var planId = task.PlanId;
            var allDone = await _db.MitigationTasks.AllAsync(t => t.PlanId == planId && t.Status == "Done");
            if (allDone)
            {
                var risk = task.Plan.Risk;
                risk.Status = "Closed_Controlled";
                risk.UpdatedAt = DateTime.UtcNow;
                _riskService.AddAuditLog(risk.OrgId, user.Id, "Risk", risk.RiskId, "RiskClosedControlled", "All mitigation tasks completed", HttpContext.Connection.RemoteIpAddress?.ToString());
                await _db.SaveChangesAsync();
            }

            return Json(new { ok = true });
        }

        [HttpGet]
        public async Task<IActionResult> TaskDetails(int taskId)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Challenge();

            var task = await _db.MitigationTasks
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
        public async Task<IActionResult> UpdateTask(int taskId, string? assignedToUserId, string? dueDate, string? description, int? progressPercent, string? status)
        {
            if (taskId <= 0) return BadRequest();

            var user = await GetCurrentUserAsync();
            if (user == null) return Challenge();

            var task = await _db.MitigationTasks
                .Include(t => t.Plan)
                .ThenInclude(p => p!.Risk)
                .FirstOrDefaultAsync(t => t.TaskId == taskId);
            if (task == null) return NotFound();
            if (task.Plan.Risk.OrgId != user.OrganizationId) return Forbid();

            var allowedStatuses = new[] { "ToDo", "InProgress", "Review", "Done" };
            if (!string.IsNullOrWhiteSpace(status) && allowedStatuses.Contains(status, StringComparer.OrdinalIgnoreCase))
                task.Status = status;

            task.AssignedToUserId = string.IsNullOrWhiteSpace(assignedToUserId) ? null : assignedToUserId.Trim();
            task.DueDate = !string.IsNullOrWhiteSpace(dueDate) && DateTime.TryParse(dueDate, out var d) ? d : (DateTime?)null;
            task.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
            task.ProgressPercent = progressPercent.HasValue ? Math.Clamp(progressPercent.Value, 0, 100) : 0;
            task.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return Json(new { ok = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateTask(int planId, string title, string? description, string? assignedToUserId, string? dueDate)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Challenge();

            var plan = await _db.MitigationPlans
                .Include(p => p.Risk)
                .FirstOrDefaultAsync(p => p.PlanId == planId);
            if (plan == null || plan.Risk.OrgId != user.OrganizationId) return NotFound();

            if (string.IsNullOrWhiteSpace(title)) return BadRequest();

            var task = new MitigationTask
            {
                PlanId = planId,
                Title = title.Trim(),
                Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
                AssignedToUserId = string.IsNullOrWhiteSpace(assignedToUserId) ? null : assignedToUserId.Trim(),
                DueDate = !string.IsNullOrWhiteSpace(dueDate) && DateTime.TryParse(dueDate, out var d) ? d : (DateTime?)null,
                Status = "ToDo",
                ProgressPercent = 0,
                UpdatedAt = DateTime.UtcNow
            };
            _db.MitigationTasks.Add(task);
            await _db.SaveChangesAsync();
            _riskService.AddAuditLog(plan.Risk.OrgId, user.Id, "MitigationTask", task.TaskId, "TaskCreated", task.Title, HttpContext.Connection.RemoteIpAddress?.ToString());
            await _db.SaveChangesAsync();

            return Json(new { ok = true, taskId = task.TaskId, title = task.Title, status = task.Status, dueDateFormatted = task.DueDate?.ToString("MMM d") ?? "N/A", progressPercent = 0 });
        }
    }
}
