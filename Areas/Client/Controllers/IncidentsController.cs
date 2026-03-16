using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Rendering;
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
    public class IncidentsController : Controller
    {
        private readonly IIncidentService _incidentService;
        private readonly ProjectService _projectService;
        private readonly ITenantDbFactory _tenantDbFactory;
        private readonly UserManager<ApplicationUser> _userManager;

        public IncidentsController(IIncidentService incidentService, ProjectService projectService, ITenantDbFactory tenantDbFactory, UserManager<ApplicationUser> userManager)
        {
            _incidentService = incidentService;
            _projectService = projectService;
            _tenantDbFactory = tenantDbFactory;
            _userManager = userManager;
        }

        private async Task<int?> GetMyOrgIdAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            return user?.OrganizationId;
        }

        public async Task<IActionResult> Index(int? siteId, string? status, DateTime? startDate, DateTime? endDate)
        {
            ViewData["Title"] = "HSE Incidents";
            var orgId = await GetMyOrgIdAsync();
            if (!orgId.HasValue) return View();

            return View();
        }

        public async Task<IActionResult> IndexContent(int? siteId, string? status, DateTime? startDate, DateTime? endDate)
        {
            ViewData["Title"] = "HSE Incidents";
            var orgId = await GetMyOrgIdAsync();
            if (!orgId.HasValue)
            {
                return PartialView("_IndexContent", new List<IncidentViewModel>());
            }

            var incidents = await _incidentService.GetIncidentsAsync(orgId.Value, siteId, status, startDate, endDate);

            // Calculate Stats for Dashboard Cards
            ViewBag.TotalIncidents = incidents.Count;
            ViewBag.OpenIncidents = incidents.Count(i => i.Status == "Open" || i.Status == "Investigating");
            ViewBag.CriticalIncidents = incidents.Count(i => i.Severity == "Critical");
            
            var model = incidents.Select(i => new IncidentViewModel
            {
                IncidentId = i.IncidentId,
                Title = i.Title,
                SiteId = i.SiteId,
                SiteName = i.Site?.SiteName ?? "Unknown",
                ProjectId = i.ProjectId,
                ProjectName = i.Project?.Name,
                Description = i.Description,
                IncidentDate = i.IncidentDate,
                Type = i.Type,
                Severity = i.Severity,
                Status = i.Status,
                ReportedAt = i.ReportedAt
            }).ToList();

            await PopulateSiteDropdown(orgId.Value, siteId);
            await PopulateProjectDropdown(orgId.Value);
            ViewBag.StatusFilter = status;
            ViewBag.StartDate = startDate?.ToString("yyyy-MM-dd");
            ViewBag.EndDate = endDate?.ToString("yyyy-MM-dd");

            return PartialView("_IndexContent", model);
        }

        public async Task<IActionResult> Details(int id)
        {
            var orgId = await GetMyOrgIdAsync();
            if (!orgId.HasValue) return RedirectToAction(nameof(Index));

            var incident = await _incidentService.GetIncidentByIdAsync(id, orgId.Value);
            if (incident == null) return NotFound();

            var reportedByUser = await _userManager.FindByIdAsync(incident.ReportedByUserId);
            ViewBag.ReportedByName = reportedByUser != null ? $"{reportedByUser.FirstName} {reportedByUser.LastName}" : "Unknown";

            return View(incident);
        }

        public async Task<IActionResult> Create()
        {
            var orgId = await GetMyOrgIdAsync();
            if (!orgId.HasValue) return RedirectToAction(nameof(Index));

            await PopulateSiteDropdown(orgId.Value);
            await PopulateProjectDropdown(orgId.Value);
            return View(new IncidentEditViewModel { IncidentDate = DateTime.Now, Status = "Open" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(IncidentEditViewModel model)
        {
            var orgId = await GetMyOrgIdAsync();
            if (!orgId.HasValue) return RedirectToAction(nameof(Index));

            if (ModelState.IsValid)
            {
                var user = await _userManager.GetUserAsync(User);
                var incident = new Incident
                {
                    OrgId = orgId.Value,
                    SiteId = model.SiteId.Value,
                    ProjectId = model.ProjectId,
                    ReportedByUserId = user!.Id,
                    Title = model.Title,
                    Description = model.Description,
                    IncidentDate = model.IncidentDate,
                    Type = model.Type,
                    Severity = model.Severity,
                    Status = model.Status,
                    RootCause = model.RootCause,
                    CorrectiveActions = model.CorrectiveActions,
                    WeatherConditions = model.WeatherConditions
                };

                await _incidentService.CreateIncidentAsync(incident);
                
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = true });
                }

                return RedirectToAction(nameof(Index));
            }

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                 var errors = ModelState.Where(x => x.Value.Errors.Count > 0)
                    .ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value.Errors.Select(e => e.ErrorMessage).ToArray()
                    );
                return BadRequest(errors);
            }

            await PopulateSiteDropdown(orgId.Value, model.SiteId);
            await PopulateProjectDropdown(orgId.Value, model.ProjectId);
            return View(model);
        }

        public async Task<IActionResult> Edit(int id)
        {
            var orgId = await GetMyOrgIdAsync();
            if (!orgId.HasValue) return RedirectToAction(nameof(Index));

            var incident = await _incidentService.GetIncidentByIdAsync(id, orgId.Value);
            if (incident == null) return NotFound();

            var model = new IncidentEditViewModel
            {
                IncidentId = incident.IncidentId,
                SiteId = incident.SiteId,
                ProjectId = incident.ProjectId,
                Title = incident.Title,
                Description = incident.Description,
                IncidentDate = incident.IncidentDate,
                Type = incident.Type,
                Severity = incident.Severity,
                Status = incident.Status,
                RootCause = incident.RootCause,
                CorrectiveActions = incident.CorrectiveActions,
                WeatherConditions = incident.WeatherConditions
            };

            await PopulateSiteDropdown(orgId.Value, incident.SiteId);
            await PopulateProjectDropdown(orgId.Value, incident.ProjectId);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(IncidentEditViewModel model)
        {
            var orgId = await GetMyOrgIdAsync();
            if (!orgId.HasValue) return RedirectToAction(nameof(Index));

            if (ModelState.IsValid)
            {
                var user = await _userManager.GetUserAsync(User);
                var incident = new Incident
                {
                    IncidentId = model.IncidentId,
                    OrgId = orgId.Value,
                    SiteId = model.SiteId.Value,
                    ProjectId = model.ProjectId,
                    ReportedByUserId = user!.Id, // Note: This might not be accurate if we want to preserve original reporter, but Service uses this for Audit log. 
                                                 // Ideally update service to take userId separately or only use it for audit.
                                                 // The service UpdateIncidentAsync doesn't overwrite ReportedByUserId, so this property is ignored on update mapping in service.
                    Title = model.Title,
                    Description = model.Description,
                    IncidentDate = model.IncidentDate,
                    Type = model.Type,
                    Severity = model.Severity,
                    Status = model.Status,
                    RootCause = model.RootCause,
                    CorrectiveActions = model.CorrectiveActions,
                    WeatherConditions = model.WeatherConditions
                };

                await _incidentService.UpdateIncidentAsync(incident, user.Id);
                
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = true });
                }

                return RedirectToAction(nameof(Index));
            }

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                 var errors = ModelState.Where(x => x.Value.Errors.Count > 0)
                    .ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value.Errors.Select(e => e.ErrorMessage).ToArray()
                    );
                return BadRequest(errors);
            }

            await PopulateSiteDropdown(orgId.Value, model.SiteId);
            await PopulateProjectDropdown(orgId.Value, model.ProjectId);
            return View(model);
        }

        public async Task<IActionResult> Delete(int id)
        {
            var orgId = await GetMyOrgIdAsync();
            if (!orgId.HasValue) return RedirectToAction(nameof(Index));

            var incident = await _incidentService.GetIncidentByIdAsync(id, orgId.Value);
            if (incident == null) return NotFound();

            return View(incident);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var orgId = await GetMyOrgIdAsync();
            if (!orgId.HasValue) return RedirectToAction(nameof(Index));
            var user = await _userManager.GetUserAsync(User);

            await _incidentService.DeleteIncidentAsync(id, orgId.Value, user!.Id);
            return RedirectToAction(nameof(Index));
        }

        private async Task PopulateProjectDropdown(int orgId, int? selectedId = null)
        {
            var projects = await _projectService.GetProjectsAsync(orgId, "Active", null, null);
            ViewBag.Projects = new SelectList(projects.Select(p => new { p.ProjectId, p.Name }), "ProjectId", "Name", selectedId);
        }

        private async Task PopulateSiteDropdown(int orgId, int? selectedId = null)
        {
            await using var db = await _tenantDbFactory.CreateAsync(orgId);
            var sites = await db.Sites.AsNoTracking()
                .Where(s => s.OrgId == orgId && s.Status == "Active")
                .OrderBy(s => s.SiteName)
                .Select(s => new { s.SiteId, s.SiteName })
                .ToListAsync();

            ViewBag.Sites = new SelectList(sites, "SiteId", "SiteName", selectedId);
        }
    }
}
