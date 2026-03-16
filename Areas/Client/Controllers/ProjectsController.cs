using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using WEB_Sentro.Data;
using WEB_Sentro.Data.Entities;
using WEB_Sentro.Models.Identity;
using WEB_Sentro.Services;

namespace WEB_Sentro.Areas.Client.Controllers
{
    [Area("Client")]
    [Authorize(Policy = "MainAdminOnly")]
    public class ProjectsController : Controller
    {
        private readonly ProjectService _projectService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ITenantDbFactory _tenantDbFactory;
        private readonly PlatformDbContext _platformDb;

        public ProjectsController(ProjectService projectService, UserManager<ApplicationUser> userManager, ITenantDbFactory tenantDbFactory, PlatformDbContext platformDb)
        {
            _projectService = projectService;
            _userManager = userManager;
            _tenantDbFactory = tenantDbFactory;
            _platformDb = platformDb;
        }

        private async Task<ApplicationUser?> GetCurrentUserAsync() => await _userManager.GetUserAsync(User);
        
        private async Task<int?> GetMyOrgIdAsync()
        {
            var user = await GetCurrentUserAsync();
            return user?.OrganizationId;
        }
        
        private bool IsSuperAdmin() => User.IsInRole("SuperAdmin");

        public async Task<IActionResult> Index(string? status, string? search, int page = 1, int pageSize = 12)
        {
            ViewData["Title"] = "Projects Portfolio";
            var user = await GetCurrentUserAsync();
            if (user == null) return Challenge();
            
            int? orgId = IsSuperAdmin() ? null : user.OrganizationId;
            if (!orgId.HasValue) return View(new ProjectIndexViewModel());

            // Get projects
            var projects = await _projectService.GetProjectsAsync(orgId.Value, status, search, null);

            // Pagination
            var totalCount = projects.Count;
            var items = projects
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new ProjectRowViewModel
                {
                    ProjectId = p.ProjectId,
                    ProjectCode = p.ProjectCode,
                    Name = p.Name,
                    Status = p.Status,
                    Budget = p.Budget,
                    StartDate = p.StartDate,
                    EndDate = p.EndDate,
                    SiteName = p.Site?.SiteName,
                    ManagerName = "Loading..." // We'll populate this later or client-side, but for now let's try to fetch
                })
                .ToList();

            // Fetch Manager Names
            var managerIds = projects.Where(p => !string.IsNullOrEmpty(p.ManagerUserId)).Select(p => p.ManagerUserId!).Distinct().ToList();
            if (managerIds.Any())
            {
                var managers = await _platformDb.Users.AsNoTracking()
                    .Where(u => managerIds.Contains(u.Id))
                    .ToDictionaryAsync(u => u.Id, u => $"{u.FirstName} {u.LastName}");
                
                foreach (var item in items)
                {
                    var p = projects.First(x => x.ProjectId == item.ProjectId);
                    if (!string.IsNullOrEmpty(p.ManagerUserId) && managers.TryGetValue(p.ManagerUserId, out var name))
                    {
                        item.ManagerName = name;
                    }
                    else
                    {
                        item.ManagerName = "Unassigned";
                    }
                }
            }

            var model = new ProjectIndexViewModel
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = page,
                PageSize = pageSize,
                Search = search,
                StatusFilter = status
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var orgId = await GetMyOrgIdAsync();
            if (!orgId.HasValue) return RedirectToAction(nameof(Index));

            await using var db = await _tenantDbFactory.CreateAsync(orgId.Value);
            var sites = await db.Sites.AsNoTracking()
                .Where(s => s.OrgId == orgId.Value && s.Status == "Active")
                .OrderBy(s => s.SiteName)
                .Select(s => new SelectListItem { Value = s.SiteId.ToString(), Text = $"{s.SiteName} ({s.SiteCode})" })
                .ToListAsync();

            var managers = await _platformDb.Users.AsNoTracking()
                .Where(u => u.OrganizationId == orgId.Value)
                .OrderBy(u => u.FirstName)
                .Select(u => new SelectListItem { Value = u.Id, Text = $"{u.FirstName} {u.LastName}" })
                .ToListAsync();

            var model = new ProjectCreateViewModel
            {
                AvailableSites = sites,
                AvailableManagers = managers,
                StartDate = DateTime.Today,
                Status = "Draft"
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ProjectCreateViewModel model)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Challenge();
            var orgId = user.OrganizationId;
            // if (!orgId.HasValue) return RedirectToAction(nameof(Index));

            if (!ModelState.IsValid)
            {
                // Reload lists
                 await using var db = await _tenantDbFactory.CreateAsync(orgId);
                model.AvailableSites = await db.Sites.AsNoTracking()
                    .Where(s => s.OrgId == orgId && s.Status == "Active")
                    .OrderBy(s => s.SiteName)
                    .Select(s => new SelectListItem { Value = s.SiteId.ToString(), Text = $"{s.SiteName} ({s.SiteCode})" })
                    .ToListAsync();

                model.AvailableManagers = await _platformDb.Users.AsNoTracking()
                    .Where(u => u.OrganizationId == orgId)
                    .OrderBy(u => u.FirstName)
                    .Select(u => new SelectListItem { Value = u.Id, Text = $"{u.FirstName} {u.LastName}" })
                    .ToListAsync();
                    
                return View(model);
            }

            var project = new Project
            {
                Name = model.Name,
                ProjectCode = model.ProjectCode,
                Description = model.Description,
                Status = model.Status,
                StartDate = model.StartDate,
                EndDate = model.EndDate,
                Budget = model.Budget,
                SiteId = model.SiteId,
                ManagerUserId = model.ManagerUserId
            };

            await _projectService.CreateProjectAsync(orgId, user.Id, project);

            TempData["Message"] = "Project created successfully.";
            return RedirectToAction(nameof(Details), new { id = project.ProjectId });
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var orgId = await GetMyOrgIdAsync();
            if (!orgId.HasValue) return RedirectToAction(nameof(Index));

            var project = await _projectService.GetProjectByIdAsync(orgId.Value, id);
            if (project == null) return NotFound();

            // Get Manager Name
            string managerName = "Unassigned";
            if (!string.IsNullOrEmpty(project.ManagerUserId))
            {
                var manager = await _platformDb.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == project.ManagerUserId);
                if (manager != null) managerName = $"{manager.FirstName} {manager.LastName}";
            }

            // Stats
            dynamic stats = await _projectService.GetProjectStatsAsync(orgId.Value, id);
            
            // Map to ViewModel
            var model = new ProjectDetailsViewModel
            {
                ProjectId = project.ProjectId,
                ProjectCode = project.ProjectCode,
                Name = project.Name,
                Description = project.Description,
                Status = project.Status,
                StartDate = project.StartDate,
                EndDate = project.EndDate,
                Budget = project.Budget,
                SiteName = project.Site?.SiteName,
                SiteId = project.SiteId,
                ManagerName = managerName,
                
                // Stats from service
                HighRiskCount = stats.HighRiskCount,
                TotalExpenses = stats.TotalExpenses,
                Progress = stats.Progress,
                TaskCount = stats.TaskCount,
                OpenPOCount = stats.OpenPOCount,
                
                // Related counts
                RiskCount = project.Risks.Count,
                POCount = project.PurchaseOrders.Count,
                IncidentCount = project.Incidents.Count,
                ExpenseCount = project.Expenses.Count,
                Risks = project.Risks.Select(r => new Web_Sentro.Areas.Client.Models.RiskIdentificationViewModel
                {
                    Id = r.RiskId,
                    Title = r.Title,
                    Category = r.Category ?? "Uncategorized",
                    Priority = r.Priority ?? "Medium",
                    Status = r.Status,
                    DateLogged = r.CreatedAt,
                    ReportedBy = "Loading..." // Can fetch if needed
                }).OrderByDescending(r => r.Priority == "Critical" || r.Priority == "High").ThenByDescending(r => r.DateLogged).ToList()
            };

            return View(model);
        }

        // --- WBS / Task API ---

        [HttpGet]
        public async Task<IActionResult> GetTasks(int projectId)
        {
            var orgId = await GetMyOrgIdAsync();
            if (!orgId.HasValue) return Unauthorized();

            var tasks = await _projectService.GetProjectTasksAsync(orgId.Value, projectId);
            
            // Map to flat list with parent info, client can build tree
            // Also need to fetch assignee names if we want to display them nicely
            var assigneeIds = tasks.Where(t => !string.IsNullOrEmpty(t.AssignedToUserId)).Select(t => t.AssignedToUserId!).Distinct().ToList();
            var assignees = new Dictionary<string, string>();
            if (assigneeIds.Any())
            {
                assignees = await _platformDb.Users.AsNoTracking()
                    .Where(u => assigneeIds.Contains(u.Id))
                    .ToDictionaryAsync(u => u.Id, u => $"{u.FirstName} {u.LastName}");
            }

            var dtos = tasks.Select(t => new
            {
                t.ProjectTaskId,
                t.ParentTaskId,
                t.TaskCode,
                t.Title,
                t.Description,
                t.Status,
                t.PercentComplete,
                t.StartDate,
                t.EndDate,
                t.Budget,
                AssignedToName = !string.IsNullOrEmpty(t.AssignedToUserId) && assignees.ContainsKey(t.AssignedToUserId) ? assignees[t.AssignedToUserId] : null,
                t.AssignedToUserId
            });

            return Json(dtos);
        }

        [HttpPost]
        public async Task<IActionResult> CreateTask([FromBody] ProjectTaskCreateDto dto)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Unauthorized();
            var orgId = user.OrganizationId;
            // if (!orgId.HasValue) return Unauthorized();

            var task = new ProjectTask
            {
                ProjectId = dto.ProjectId,
                ParentTaskId = dto.ParentTaskId,
                Title = dto.Title,
                Description = dto.Description,
                TaskCode = dto.TaskCode ?? "", // Generate or require?
                TaskType = "Task",
                Status = "Pending",
                StartDate = dto.StartDate,
                EndDate = dto.EndDate,
                Budget = dto.Budget,
                AssignedToUserId = dto.AssignedToUserId
            };

            var created = await _projectService.CreateTaskAsync(orgId, user.Id, task);
            return Json(new { success = true, taskId = created.ProjectTaskId });
        }

        [HttpPost]
        public async Task<IActionResult> UpdateTask([FromBody] ProjectTaskUpdateDto dto)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Unauthorized();
            var orgId = user.OrganizationId;
            // if (!orgId.HasValue) return Unauthorized();

            var success = await _projectService.UpdateTaskAsync(orgId, dto.ProjectTaskId, user.Id, 
                dto.Title, dto.Description, dto.Status, dto.PercentComplete, dto.StartDate, dto.EndDate, dto.AssignedToUserId, dto.Budget);
            
            if (!success) return NotFound();
            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteTask(int id)
        {
            var orgId = await GetMyOrgIdAsync();
            if (!orgId.HasValue) return Unauthorized();

            var success = await _projectService.DeleteTaskAsync(orgId.Value, id);
            if (!success) return NotFound();
            return Json(new { success = true });
        }
    }

    public class ProjectTaskCreateDto
    {
        public int ProjectId { get; set; }
        public int? ParentTaskId { get; set; }
        public string Title { get; set; } = "";
        public string? Description { get; set; }
        public string? TaskCode { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public decimal? Budget { get; set; }
        public string? AssignedToUserId { get; set; }
    }

    public class ProjectTaskUpdateDto
    {
        public int ProjectTaskId { get; set; }
        public string Title { get; set; } = "";
        public string? Description { get; set; }
        public string Status { get; set; } = "";
        public int PercentComplete { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string? AssignedToUserId { get; set; }
        public decimal? Budget { get; set; }
    }

    public class ProjectIndexViewModel
    {
        public List<ProjectRowViewModel> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public string? Search { get; set; }
        public string? StatusFilter { get; set; }
    }

    public class ProjectRowViewModel
    {
        public int ProjectId { get; set; }
        public string ProjectCode { get; set; } = "";
        public string Name { get; set; } = "";
        public string Status { get; set; } = "";
        public decimal? Budget { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string? SiteName { get; set; }
        public string? ManagerName { get; set; }
    }

    public class ProjectCreateViewModel
    {
        public string Name { get; set; } = "";
        public string ProjectCode { get; set; } = "";
        public string? Description { get; set; }
        public string Status { get; set; } = "Draft";
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public decimal? Budget { get; set; }
        public int? SiteId { get; set; }
        public string? ManagerUserId { get; set; }

        public List<SelectListItem> AvailableSites { get; set; } = new();
        public List<SelectListItem> AvailableManagers { get; set; } = new();
    }

    public class ProjectDetailsViewModel
    {
        public int ProjectId { get; set; }
        public string ProjectCode { get; set; } = "";
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public string Status { get; set; } = "";
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public decimal? Budget { get; set; }
        public string? SiteName { get; set; }
        public int? SiteId { get; set; }
        public string? ManagerName { get; set; }
        
        // Stats
        public int HighRiskCount { get; set; }
        public decimal TotalExpenses { get; set; }
        public double Progress { get; set; }
        public int TaskCount { get; set; }
        public int OpenPOCount { get; set; }

        public int RiskCount { get; set; }
        public int POCount { get; set; }
        public int IncidentCount { get; set; }
        public int ExpenseCount { get; set; }

        public List<Web_Sentro.Areas.Client.Models.RiskIdentificationViewModel> Risks { get; set; } = new();
    }
}
