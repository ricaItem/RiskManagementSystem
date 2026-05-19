using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WEB_Sentro.Data;
using WEB_Sentro.Data.Entities;
using WEB_Sentro.Models.Identity;
using WEB_Sentro.Services;

namespace WEB_Sentro.Areas.Client.Controllers
{
    [Area("Client")]
    [Authorize(Policy = "MainAdminOnly")]
    public class SitesController : Controller
    {
        private readonly ITenantDbFactory _tenantDbFactory;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly PlatformDbContext _platformDb;

        public SitesController(ITenantDbFactory tenantDbFactory, UserManager<ApplicationUser> userManager, PlatformDbContext platformDb)
        {
            _tenantDbFactory = tenantDbFactory;
            _userManager = userManager;
            _platformDb = platformDb;
        }

        private async Task<ApplicationUser?> GetCurrentUserAsync() => await _userManager.GetUserAsync(User);
        private async Task<int?> GetMyOrgIdAsync()
        {
            var me = await GetCurrentUserAsync();
            return me?.OrganizationId;
        }
        private bool IsSuperAdmin() => User.IsInRole("SuperAdmin");

        public async Task<IActionResult> Index(string? search, string? status, int page = 1, int pageSize = 10)
        {
            ViewData["Title"] = "Sites";
            var user = await GetCurrentUserAsync();
            if (user == null) return Challenge();
            var orgId = IsSuperAdmin() ? null : await GetMyOrgIdAsync();
            if (!orgId.HasValue) return View(new SiteIndexViewModel { Items = new List<SiteRowViewModel>(), TotalCount = 0, PageNumber = 1, PageSize = pageSize });

            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 5, 50);

            await using var db = await _tenantDbFactory.CreateAsync(orgId.Value);
            var q = db.Sites.AsNoTracking().Where(s => s.OrgId == orgId.Value);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                q = q.Where(s => s.SiteName.Contains(term) || s.SiteCode.Contains(term));
            }
            if (!string.IsNullOrWhiteSpace(status))
                q = q.Where(s => s.Status == status.Trim());

            var totalCount = await q.CountAsync();
            var sites = await q
                .OrderBy(s => s.SiteName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(s => new { s.SiteId, s.SiteCode, s.SiteName, s.Status, s.ProjectManagerUserId, s.City, s.Province, s.Latitude, s.Longitude })
                .ToListAsync();

            var siteIds = sites.Select(x => x.SiteId).ToList();
            var riskCounts = await db.Risks.AsNoTracking()
                .Where(r => r.SiteId != null && siteIds.Contains(r.SiteId.Value) && r.DeletedAt == null)
                .GroupBy(r => r.SiteId!.Value)
                .Select(g => new { SiteId = g.Key, Active = g.Count(), Critical = g.Count(r => r.Priority == "Critical") })
                .ToListAsync();
            var riskBySite = riskCounts.ToDictionary(x => x.SiteId, x => (x.Active, x.Critical));

            var managerIds = sites.Where(x => !string.IsNullOrEmpty(x.ProjectManagerUserId)).Select(x => x.ProjectManagerUserId!).Distinct().ToList();
            Dictionary<string, string> managerNames = new();
            if (managerIds.Count > 0)
            {
                var managers = await _platformDb.Users.AsNoTracking().Where(u => managerIds.Contains(u.Id)).Select(u => new { u.Id, u.FirstName, u.LastName }).ToListAsync();
                managerNames = managers.ToDictionary(u => u.Id, u => $"{u.FirstName} {u.LastName}".Trim());
            }

            var items = sites.Select(s => new SiteRowViewModel
            {
                SiteId = s.SiteId,
                SiteCode = s.SiteCode,
                SiteName = s.SiteName,
                Status = s.Status,
                ManagerName = !string.IsNullOrEmpty(s.ProjectManagerUserId) && managerNames.TryGetValue(s.ProjectManagerUserId, out var mn) ? mn : null,
                Location = string.Join(", ", new[] { s.City, s.Province }.Where(x => !string.IsNullOrEmpty(x))),
                ActiveRisks = riskBySite.TryGetValue(s.SiteId, out var r) ? r.Active : 0,
                CriticalRisks = riskBySite.TryGetValue(s.SiteId, out var r2) ? r2.Critical : 0,
                Latitude = s.Latitude,
                Longitude = s.Longitude
            }).ToList();

            var model = new SiteIndexViewModel
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
            ViewData["Title"] = "New Site";
            var user = await GetCurrentUserAsync();
            if (user == null) return Challenge();
            var orgId = IsSuperAdmin() ? null : await GetMyOrgIdAsync();
            if (!orgId.HasValue) return RedirectToAction(nameof(Index));
            return View(new SiteEditViewModel { Status = "Active" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SiteEditViewModel model)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Challenge();
            var orgId = IsSuperAdmin() ? null : await GetMyOrgIdAsync();
            if (!orgId.HasValue) return RedirectToAction(nameof(Index));

            if (string.IsNullOrWhiteSpace(model.SiteCode) || string.IsNullOrWhiteSpace(model.SiteName))
            {
                ModelState.AddModelError("", "Site Code and Site Name are required.");
                return View(model);
            }

            await using var db = await _tenantDbFactory.CreateAsync(orgId.Value);
            if (await db.Sites.AnyAsync(s => s.SiteCode == model.SiteCode.Trim()))
            {
                ModelState.AddModelError(nameof(model.SiteCode), "Site Code already exists.");
                return View(model);
            }

            var now = DateTime.UtcNow;
            var site = new Site
            {
                OrgId = orgId.Value,
                SiteCode = model.SiteCode.Trim(),
                SiteName = model.SiteName.Trim(),
                Status = model.Status ?? "Active",
                AddressLine = model.AddressLine?.Trim(),
                City = model.City?.Trim(),
                Province = model.Province?.Trim(),
                Latitude = model.Latitude,
                Longitude = model.Longitude,
                ProjectManagerUserId = model.ProjectManagerUserId?.Trim().NullIfEmpty(),
                BudgetAllocated = model.BudgetAllocated,
                CreatedAt = now,
                UpdatedAt = now
            };
            db.Sites.Add(site);
            await db.SaveChangesAsync();
            TempData["ToastSuccess"] = "Site created successfully.";
            return RedirectToAction(nameof(Details), new { id = site.SiteId });
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            ViewData["Title"] = "Edit Site";
            var user = await GetCurrentUserAsync();
            if (user == null) return Challenge();
            var orgId = IsSuperAdmin() ? null : await GetMyOrgIdAsync();
            if (!orgId.HasValue) return RedirectToAction(nameof(Index));

            await using var db = await _tenantDbFactory.CreateAsync(orgId.Value);
            var site = await db.Sites.AsNoTracking().FirstOrDefaultAsync(s => s.SiteId == id && s.OrgId == orgId.Value);
            if (site == null) return NotFound();

            var model = new SiteEditViewModel
            {
                SiteId = site.SiteId,
                SiteCode = site.SiteCode,
                SiteName = site.SiteName,
                Status = site.Status,
                AddressLine = site.AddressLine,
                City = site.City,
                Province = site.Province,
                Latitude = site.Latitude,
                Longitude = site.Longitude,
                ProjectManagerUserId = site.ProjectManagerUserId,
                BudgetAllocated = site.BudgetAllocated
            };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(SiteEditViewModel model)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Challenge();
            var orgId = IsSuperAdmin() ? null : await GetMyOrgIdAsync();
            if (!orgId.HasValue) return RedirectToAction(nameof(Index));

            if (string.IsNullOrWhiteSpace(model.SiteCode) || string.IsNullOrWhiteSpace(model.SiteName))
            {
                ModelState.AddModelError("", "Site Code and Site Name are required.");
                return View(model);
            }

            await using var db = await _tenantDbFactory.CreateAsync(orgId.Value);
            var site = await db.Sites.FirstOrDefaultAsync(s => s.SiteId == model.SiteId && s.OrgId == orgId.Value);
            if (site == null) return NotFound();

            if (await db.Sites.AnyAsync(s => s.SiteCode == model.SiteCode.Trim() && s.SiteId != model.SiteId))
            {
                ModelState.AddModelError(nameof(model.SiteCode), "Site Code already exists.");
                return View(model);
            }

            site.SiteCode = model.SiteCode.Trim();
            site.SiteName = model.SiteName.Trim();
            site.Status = model.Status ?? "Active";
            site.AddressLine = model.AddressLine?.Trim().NullIfEmpty();
            site.City = model.City?.Trim().NullIfEmpty();
            site.Province = model.Province?.Trim().NullIfEmpty();
            site.Latitude = model.Latitude;
            site.Longitude = model.Longitude;
            site.ProjectManagerUserId = model.ProjectManagerUserId?.Trim().NullIfEmpty();
            site.BudgetAllocated = model.BudgetAllocated;
            site.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            TempData["ToastSuccess"] = "Site updated successfully.";
            return RedirectToAction(nameof(Details), new { id = site.SiteId });
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            ViewData["Title"] = "Site Details";
            var user = await GetCurrentUserAsync();
            if (user == null) return Challenge();
            var orgId = IsSuperAdmin() ? null : await GetMyOrgIdAsync();
            if (!orgId.HasValue) return RedirectToAction(nameof(Index));

            await using var db = await _tenantDbFactory.CreateAsync(orgId.Value);
            var site = await db.Sites.AsNoTracking().FirstOrDefaultAsync(s => s.SiteId == id && s.OrgId == orgId.Value);
            if (site == null) return NotFound();

            var activeRisks = await db.Risks.AsNoTracking().CountAsync(r => r.SiteId == id && r.DeletedAt == null);
            var criticalRisks = await db.Risks.AsNoTracking().CountAsync(r => r.SiteId == id && r.DeletedAt == null && r.Priority == "Critical");
            var openIncidents = await db.Incidents.AsNoTracking().CountAsync(i => i.SiteId == id && i.Status == "Open");

            var monitoringSite = await db.MonitoringSites.AsNoTracking().FirstOrDefaultAsync(m => m.SiteId == id && m.OrgId == orgId.Value);
            string? latestWeather = null;
            if (monitoringSite != null)
            {
                var lastAlert = await db.MonitoringAlerts.AsNoTracking()
                    .Where(a => a.MonitoringSiteId == monitoringSite.MonitoringSiteId)
                    .OrderByDescending(a => a.TriggeredAt)
                    .Select(a => a.RuleName + " @ " + a.TriggeredAt.ToString("g"))
                    .FirstOrDefaultAsync();
                latestWeather = lastAlert ?? "No alerts yet";
            }

            // Identity / Assignments
            // 1. Project Manager
            string? pmName = null;
            string? pmProfileImagePath = null;
            if (!string.IsNullOrEmpty(site.ProjectManagerUserId))
            {
                var pm = await _platformDb.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == site.ProjectManagerUserId);
                pmName = pm != null ? $"{pm.FirstName} {pm.LastName}" : "Unknown User";
                pmProfileImagePath = pm?.ProfileImagePath;
            }

            // 2. Derive "Assigned Employees" from activity (Risk Owners, Incident Reporters, Task Assignees)
            // This creates a dynamic "Team" based on real work
            var riskOwnerIds = await db.Risks.AsNoTracking()
                .Where(r => r.SiteId == id && r.RiskOwnerId != null)
                .Select(r => r.RiskOwnerId!)
                .Distinct()
                .ToListAsync();

            var incidentReporterIds = await db.Incidents.AsNoTracking()
                .Where(i => i.SiteId == id && i.ReportedByUserId != null)
                .Select(i => i.ReportedByUserId!)
                .Distinct()
                .ToListAsync();

            var taskAssigneeIds = await db.MitigationTasks.AsNoTracking()
                .Include(t => t.Plan).ThenInclude(p => p.Risk)
                .Where(t => t.Plan != null && t.Plan.Risk.SiteId == id && t.AssignedToUserId != null)
                .Select(t => t.AssignedToUserId!)
                .Distinct()
                .ToListAsync();

            var involvedUserIds = riskOwnerIds
                .Union(incidentReporterIds)
                .Union(taskAssigneeIds)
                .Distinct()
                .Where(uid => uid != site.ProjectManagerUserId) // Exclude PM from general list if desired, or keep
                .ToList();

            var assignedUsers = new List<SiteUserViewModel>();
            if (involvedUserIds.Any())
            {
                var users = await _platformDb.Users.AsNoTracking()
                    .Where(u => involvedUserIds.Contains(u.Id))
                    .Select(u => new { u.Id, u.FirstName, u.LastName, u.Email, u.ProfileImagePath })
                    .ToListAsync();

                foreach (var u in users)
                {
                    // Determine primary role for display
                    var roles = new List<string>();
                    if (riskOwnerIds.Contains(u.Id)) roles.Add("Risk Owner");
                    if (taskAssigneeIds.Contains(u.Id)) roles.Add("Mitigation Task");
                    if (incidentReporterIds.Contains(u.Id)) roles.Add("Reporter");
                    
                    assignedUsers.Add(new SiteUserViewModel
                    {
                        UserId = u.Id,
                        Name = $"{u.FirstName} {u.LastName}",
                        ProfileImagePath = u.ProfileImagePath,
                        Role = roles.Any() ? roles.First() : "Contributor" // Simplify to first found role
                    });
                }
            }

            // 3. All Org Users for Dropdown
            var allOrgUsers = await _platformDb.Users.AsNoTracking()
                .Where(u => u.OrganizationId == orgId.Value)
                .Select(u => new { u.Id, u.FirstName, u.LastName })
                .OrderBy(u => u.FirstName)
                .ToListAsync();

            var vm = new SiteDetailsViewModel
            {
                SiteId = site.SiteId,
                SiteCode = site.SiteCode,
                SiteName = site.SiteName,
                Status = site.Status,
                AddressLine = site.AddressLine,
                City = site.City,
                Province = site.Province,
                Latitude = site.Latitude,
                Longitude = site.Longitude,
                BudgetAllocated = site.BudgetAllocated,
                ActiveRisksCount = activeRisks,
                CriticalRisksCount = criticalRisks,
                OpenIncidentsCount = openIncidents,
                LatestWeatherCondition = latestWeather ?? "No monitoring configured",
                ProjectManagerUserId = site.ProjectManagerUserId,
                ProjectManagerName = pmName,
                ProjectManagerProfileImagePath = pmProfileImagePath,
                AssignedEmployees = assignedUsers,
                AllEmployees = allOrgUsers.Select(u => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
                {
                    Value = u.Id,
                    Text = $"{u.FirstName} {u.LastName}",
                    Selected = u.Id == site.ProjectManagerUserId
                }).ToList()
            };
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignManager(int siteId, string managerEmployeeId)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Challenge();
            var orgId = IsSuperAdmin() ? null : await GetMyOrgIdAsync();
            if (!orgId.HasValue) return RedirectToAction(nameof(Index));

            await using var db = await _tenantDbFactory.CreateAsync(orgId.Value);
            var site = await db.Sites.FirstOrDefaultAsync(s => s.SiteId == siteId && s.OrgId == orgId.Value);
            if (site == null) return NotFound();

            // verify user exists in org
            if (!string.IsNullOrEmpty(managerEmployeeId))
            {
                var targetUser = await _platformDb.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == managerEmployeeId && u.OrganizationId == orgId.Value);
                if (targetUser == null)
                {
                    TempData["ToastError"] = "User not found.";
                    return RedirectToAction(nameof(Details), new { id = siteId });
                }
            }

            site.ProjectManagerUserId = string.IsNullOrWhiteSpace(managerEmployeeId) ? null : managerEmployeeId;
            site.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            TempData["ToastSuccess"] = "Project Manager updated.";
            return RedirectToAction(nameof(Details), new { id = siteId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Archive(int id)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Challenge();
            var orgId = IsSuperAdmin() ? null : await GetMyOrgIdAsync();
            if (!orgId.HasValue) return RedirectToAction(nameof(Index));

            await using var db = await _tenantDbFactory.CreateAsync(orgId.Value);
            var site = await db.Sites.FirstOrDefaultAsync(s => s.SiteId == id && s.OrgId == orgId.Value);
            if (site == null) return NotFound();

            site.Status = "Archived";
            site.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            TempData["ToastSuccess"] = "Site archived.";
            return RedirectToAction(nameof(Index));
        }
    }

    public static class StringExtensions
    {
        public static string? NullIfEmpty(this string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }

    public class SiteRowViewModel
    {
        public int SiteId { get; set; }
        public string SiteCode { get; set; } = "";
        public string SiteName { get; set; } = "";
        public string Status { get; set; } = "";
        public string? ManagerName { get; set; }
        public string Location { get; set; } = "";
        public int ActiveRisks { get; set; }
        public int CriticalRisks { get; set; }
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
    }

    public class SiteIndexViewModel
    {
        public List<SiteRowViewModel> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public string? Search { get; set; }
        public string? StatusFilter { get; set; }
    }

    public class SiteEditViewModel
    {
        public int SiteId { get; set; }
        public string SiteCode { get; set; } = "";
        public string SiteName { get; set; } = "";
        public string? Status { get; set; }
        public string? AddressLine { get; set; }
        public string? City { get; set; }
        public string? Province { get; set; }
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public string? ProjectManagerUserId { get; set; }
        public decimal? BudgetAllocated { get; set; }
    }

    public class SiteDetailsViewModel
    {
        public int SiteId { get; set; }
        public string SiteCode { get; set; } = "";
        public string SiteName { get; set; } = "";
        public string Status { get; set; } = "";
        public string? AddressLine { get; set; }
        public string? City { get; set; }
        public string? Province { get; set; }
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public decimal? BudgetAllocated { get; set; }
        public int ActiveRisksCount { get; set; }
        public int CriticalRisksCount { get; set; }
        public int OpenIncidentsCount { get; set; }
        public string LatestWeatherCondition { get; set; } = "";

        // Identity / Assignments
        public string? ProjectManagerUserId { get; set; }
        public string? ProjectManagerName { get; set; }
        public string? ProjectManagerProfileImagePath { get; set; }
        public List<SiteUserViewModel> AssignedEmployees { get; set; } = new();
        public List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem> AllEmployees { get; set; } = new();
    }

    public class SiteUserViewModel
    {
        public string UserId { get; set; } = "";
        public string Name { get; set; } = "";
        public string? ProfileImagePath { get; set; }
        public string Role { get; set; } = ""; // Derived from their activity (e.g. "Risk Owner", "Safety Officer")
        public string Initials => Name.Split(' ').Select(x => x[0]).Take(2).Aggregate("", (x, y) => x + y).ToUpper();
    }
}
