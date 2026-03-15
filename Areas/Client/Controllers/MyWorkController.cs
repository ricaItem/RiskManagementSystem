using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WEB_Sentro.Data;
using WEB_Sentro.Data.Entities;
using WEB_Sentro.Models.Identity;
using WEB_Sentro.Services;
using Web_Sentro.Areas.Client.Models;

namespace WEB_Sentro.Areas.Client.Controllers
{
    [Area("Client")]
    [Authorize(Roles = "Employee")]
    public class MyWorkController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ITenantDbFactory _tenantDbFactory;

        public MyWorkController(UserManager<ApplicationUser> userManager, ITenantDbFactory tenantDbFactory)
        {
            _userManager = userManager;
            _tenantDbFactory = tenantDbFactory;
        }

        /// <summary>
        /// Employee dashboard: My Work — cards + task list + assigned assessments + recent alerts.
        /// All data tenant-scoped and filtered to current user (mock data for now).
        /// </summary>
        public async Task<IActionResult> Index()
        {
            ViewData["Title"] = "My Dashboard";

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();
            if (user.OrganizationId <= 0) return View(new MyWorkDashboardViewModel());

            var orgId = user.OrganizationId;
            var myTasks = await BuildMyTasksAsync(orgId, user.Id, 6);
            var myRecentRisks = await BuildMyRisksAsync(orgId, user.Id, 6);
            var recentAssessedRisks = await BuildMyAssessedRisksAsync(orgId, user.Id, 6);
            var myNotes = await BuildMyNotesAsync(orgId, user.Id, 8);

            await using var db = await _tenantDbFactory.CreateAsync(orgId);
            var myOpenRisksCount = await db.Risks.AsNoTracking()
                .Where(r => r.OrgId == orgId && r.ReportByUserId == user.Id && r.DeletedAt == null)
                .CountAsync(r => r.Status != "Closed_Controlled" && r.Status != "Closed_Invalid" && r.Status != "Rejected");

            var myAssignedTasksCount = await db.MitigationTasks.AsNoTracking()
                .Where(t => t.AssignedToUserId == user.Id && t.Plan != null && t.Plan.Risk.OrgId == orgId && t.Plan.Risk.DeletedAt == null)
                .CountAsync(t => t.Status != "Done");

            var myOverdueTasksCount = await db.MitigationTasks.AsNoTracking()
                .Where(t => t.AssignedToUserId == user.Id
                    && t.Plan != null
                    && t.Plan.Risk.OrgId == orgId
                    && t.Plan.Risk.DeletedAt == null
                    && t.Status != "Done"
                    && t.DueDate.HasValue)
                .CountAsync(t => t.DueDate!.Value.Date < DateTime.UtcNow.Date);

            var vm = new MyWorkDashboardViewModel
            {
                MyOpenRisksCount = myOpenRisksCount,
                MyAssignedTasksCount = myAssignedTasksCount,
                MyOverdueTasksCount = myOverdueTasksCount,
                MyTasks = myTasks,
                MyRecentRisks = myRecentRisks,
                RecentAssessedRisks = recentAssessedRisks,
                Notes = myNotes
            };

            return View(vm);
        }

        /// <summary>My risks list (tenant + created-by filter).</summary>
        [HttpGet]
        public IActionResult MyRisks()
        {
            return RedirectToAction(nameof(Index));
        }

        /// <summary>My assigned tasks (tenant + assigned-to filter).</summary>
        [HttpGet]
        public async Task<IActionResult> MyTasks()
        {
            ViewData["Title"] = "My Tasks";
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();
            if (user.OrganizationId <= 0) return View(new List<MitigationTaskViewModel>());

            var tasks = await BuildMyTasksAsync(user.OrganizationId, user.Id, 200);
            return View(tasks);
        }

        /// <summary>Assessments assigned to me (tenant + assignee filter).</summary>
        [HttpGet]
        public IActionResult MyAssessments()
        {
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddNote(string? title, string? body)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();
            if (user.OrganizationId <= 0) return RedirectToAction(nameof(Index));

            var safeTitle = string.IsNullOrWhiteSpace(title) ? "Quick note" : title.Trim();
            var safeBody = string.IsNullOrWhiteSpace(body) ? null : body.Trim();
            if (safeBody == null) return RedirectToAction(nameof(Index));

            await using var db = await _tenantDbFactory.CreateAsync(user.OrganizationId);
            db.EmployeeNotes.Add(new EmployeeNote
            {
                OrgId = user.OrganizationId,
                UserId = user.Id,
                Title = safeTitle.Length > 120 ? safeTitle[..120] : safeTitle,
                Body = safeBody.Length > 1200 ? safeBody[..1200] : safeBody,
                Pinned = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleNotePin(int noteId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();
            if (user.OrganizationId <= 0) return RedirectToAction(nameof(Index));

            await using var db = await _tenantDbFactory.CreateAsync(user.OrganizationId);
            var note = await db.EmployeeNotes.FirstOrDefaultAsync(n => n.EmployeeNoteId == noteId && n.OrgId == user.OrganizationId && n.UserId == user.Id);
            if (note != null)
            {
                note.Pinned = !note.Pinned;
                note.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteNote(int noteId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();
            if (user.OrganizationId <= 0) return RedirectToAction(nameof(Index));

            await using var db = await _tenantDbFactory.CreateAsync(user.OrganizationId);
            var note = await db.EmployeeNotes.FirstOrDefaultAsync(n => n.EmployeeNoteId == noteId && n.OrgId == user.OrganizationId && n.UserId == user.Id);
            if (note != null)
            {
                db.EmployeeNotes.Remove(note);
                await db.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        private async Task<List<RiskIdentificationViewModel>> BuildMyRisksAsync(int orgId, string userId, int take)
        {
            await using var db = await _tenantDbFactory.CreateAsync(orgId);

            var rows = await db.Risks.AsNoTracking()
                .Where(r => r.OrgId == orgId && r.ReportByUserId == userId && r.DeletedAt == null)
                .OrderByDescending(r => r.CreatedAt)
                .Take(take)
                .Select(r => new RiskIdentificationViewModel
                {
                    Id = r.RiskId,
                    Title = r.Title,
                    Category = r.Category ?? "Operational",
                    Priority = r.Priority ?? "Unassessed",
                    Status = r.Status,
                    DateLogged = r.CreatedAt,
                    DateReported = r.CreatedAt,
                    SiteId = r.SiteId,
                    ProjectId = r.ProjectId
                })
                .ToListAsync();

            return rows;
        }

        private async Task<List<MitigationTaskViewModel>> BuildMyTasksAsync(int orgId, string userId, int take)
        {
            await using var db = await _tenantDbFactory.CreateAsync(orgId);

            var rows = await db.MitigationTasks.AsNoTracking()
                .Where(t => t.AssignedToUserId == userId && t.Plan != null && t.Plan.Risk.OrgId == orgId && t.Plan.Risk.DeletedAt == null && (t.Plan.DeletedAt == null))
                .OrderBy(t => t.Status == "Done" ? 1 : 0)
                .ThenBy(t => t.DueDate)
                .ThenByDescending(t => t.UpdatedAt)
                .Take(take)
                .Select(t => new MitigationTaskViewModel
                {
                    Id = t.TaskId,
                    RiskId = t.Plan.RiskId,
                    Title = t.Title,
                    AssignedToUserId = t.AssignedToUserId,
                    Status = t.Status,
                    DueDate = t.DueDate,
                    ProgressPercent = t.ProgressPercent,
                    Priority = t.Plan.Risk.Priority ?? "Unassessed",
                    RiskTitle = t.Plan.Risk.Title
                })
                .ToListAsync();

            return rows;
        }

        private async Task<List<RiskAssessmentItemViewModel>> BuildMyAssessmentsAsync(int orgId, string userId, int take)
        {
            await using var db = await _tenantDbFactory.CreateAsync(orgId);

            var assessmentStatuses = new[] { "For_Review", "Submitted", "Reviewed", "Approved", "Monitoring", "MitigationRequired" };

            var rows = await db.Risks.AsNoTracking()
                .Where(r => r.OrgId == orgId && r.DeletedAt == null && (r.RiskOwnerId == userId || r.AccountableId == userId) && assessmentStatuses.Contains(r.Status))
                .OrderBy(r => r.NextReviewDate ?? DateTime.MaxValue)
                .ThenByDescending(r => r.UpdatedAt ?? r.CreatedAt)
                .Take(take)
                .Select(r => new RiskAssessmentItemViewModel
                {
                    RiskId = r.RiskId,
                    RiskTitle = r.Title,
                    Status = r.Status,
                    DueDate = r.NextReviewDate
                })
                .ToListAsync();

            return rows;
        }

        private async Task<List<RiskAssessmentItemViewModel>> BuildMyAssessedRisksAsync(int orgId, string userId, int take)
        {
            await using var db = await _tenantDbFactory.CreateAsync(orgId);
            var assessedStatuses = new[] { "Submitted", "Reviewed", "Approved", "Monitoring", "MitigationRequired", "ResidualAssessed" };

            return await db.Risks.AsNoTracking()
                .Where(r => r.OrgId == orgId && r.ReportByUserId == userId && r.DeletedAt == null && assessedStatuses.Contains(r.Status))
                .OrderByDescending(r => r.UpdatedAt ?? r.CreatedAt)
                .Take(take)
                .Select(r => new RiskAssessmentItemViewModel
                {
                    RiskId = r.RiskId,
                    RiskTitle = r.Title,
                    Status = r.Status,
                    DueDate = r.NextReviewDate
                })
                .ToListAsync();
        }

        private async Task<List<EmployeeNoteItemViewModel>> BuildMyNotesAsync(int orgId, string userId, int take)
        {
            await using var db = await _tenantDbFactory.CreateAsync(orgId);
            return await db.EmployeeNotes.AsNoTracking()
                .Where(n => n.OrgId == orgId && n.UserId == userId)
                .OrderByDescending(n => n.Pinned)
                .ThenByDescending(n => n.UpdatedAt)
                .Take(take)
                .Select(n => new EmployeeNoteItemViewModel
                {
                    NoteId = n.EmployeeNoteId,
                    Title = n.Title,
                    Body = n.Body,
                    Pinned = n.Pinned,
                    UpdatedAt = n.UpdatedAt
                })
                .ToListAsync();
        }
    }

    public class MyWorkDashboardViewModel
    {
        public int MyOpenRisksCount { get; set; }
        public int MyAssignedTasksCount { get; set; }
        public int MyOverdueTasksCount { get; set; }
        public List<MitigationTaskViewModel> MyTasks { get; set; } = new();
        public List<RiskAssessmentItemViewModel> RecentAssessedRisks { get; set; } = new();
        public List<RiskIdentificationViewModel> MyRecentRisks { get; set; } = new();
        public List<EmployeeNoteItemViewModel> Notes { get; set; } = new();
    }

    public class RiskAssessmentItemViewModel
    {
        public int RiskId { get; set; }
        public string RiskTitle { get; set; } = "";
        public string Status { get; set; } = "";
        public DateTime? DueDate { get; set; }
    }

    public class EmployeeNoteItemViewModel
    {
        public int NoteId { get; set; }
        public string Title { get; set; } = "";
        public string Body { get; set; } = "";
        public bool Pinned { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
