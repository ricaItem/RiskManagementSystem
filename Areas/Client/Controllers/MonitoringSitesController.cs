using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WEB_Sentro.Data.Entities;
using WEB_Sentro.Models.Identity;
using WEB_Sentro.Services;

namespace WEB_Sentro.Areas.Client.Controllers
{
    [Area("Client")]
    [Authorize]
    public class MonitoringSitesController : Controller
    {
        private readonly ITenantDbFactory _tenantDbFactory;
        private readonly UserManager<ApplicationUser> _userManager;

        public MonitoringSitesController(ITenantDbFactory tenantDbFactory, UserManager<ApplicationUser> userManager)
        {
            _tenantDbFactory = tenantDbFactory;
            _userManager = userManager;
        }

        private async Task<int?> GetMyOrgIdAsync()
        {
            var me = await _userManager.GetUserAsync(User);
            return me?.OrganizationId;
        }
        private bool IsSuperAdmin() => User.IsInRole("SuperAdmin");

        public async Task<IActionResult> Index()
        {
            ViewData["Title"] = "Monitoring Sites";
            var orgId = IsSuperAdmin() ? null : await GetMyOrgIdAsync();
            if (!orgId.HasValue) return View(new List<MonitoringSiteRowViewModel>());

            await using var db = await _tenantDbFactory.CreateAsync(orgId.Value);
            var list = await db.MonitoringSites.AsNoTracking()
                .OrderBy(m => m.Name)
                .Select(m => new { m.MonitoringSiteId, m.Name, m.Latitude, m.Longitude, m.SiteId })
                .ToListAsync();
            var siteIds = list.Where(x => x.SiteId.HasValue).Select(x => x.SiteId!.Value).Distinct().ToList();
            var siteNames = siteIds.Count > 0
                ? await db.Sites.AsNoTracking().Where(s => siteIds.Contains(s.SiteId)).ToDictionaryAsync(s => s.SiteId, s => s.SiteName)
                : new Dictionary<int, string>();

            var items = list.Select(m => new MonitoringSiteRowViewModel
            {
                MonitoringSiteId = m.MonitoringSiteId,
                Name = m.Name,
                Latitude = m.Latitude,
                Longitude = m.Longitude,
                SiteName = m.SiteId.HasValue && siteNames.TryGetValue(m.SiteId.Value, out var n) ? n : null
            }).ToList();
            return View(items);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            ViewData["Title"] = "Edit Monitoring Site";
            var orgId = IsSuperAdmin() ? null : await GetMyOrgIdAsync();
            if (!orgId.HasValue) return RedirectToAction(nameof(Index));

            await using var db = await _tenantDbFactory.CreateAsync(orgId.Value);
            var m = await db.MonitoringSites.AsNoTracking().FirstOrDefaultAsync(x => x.MonitoringSiteId == id && x.OrgId == orgId.Value);
            if (m == null) return NotFound();

            var sites = await db.Sites.AsNoTracking().Where(s => s.OrgId == orgId.Value && s.Status != "Archived").OrderBy(s => s.SiteName).Select(s => new { s.SiteId, s.SiteName, s.SiteCode }).ToListAsync();
            ViewBag.Sites = sites.Select(s => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = s.SiteId.ToString(), Text = $"{s.SiteName} ({s.SiteCode})" }).ToList();
            ViewBag.Sites.Insert(0, new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = "", Text = "— Not linked" });

            return View(new MonitoringSiteEditViewModel { MonitoringSiteId = m.MonitoringSiteId, Name = m.Name, Latitude = m.Latitude, Longitude = m.Longitude, SiteId = m.SiteId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(MonitoringSiteEditViewModel model)
        {
            var orgId = IsSuperAdmin() ? null : await GetMyOrgIdAsync();
            if (!orgId.HasValue) return RedirectToAction(nameof(Index));

            await using var db = await _tenantDbFactory.CreateAsync(orgId.Value);
            var m = await db.MonitoringSites.FirstOrDefaultAsync(x => x.MonitoringSiteId == model.MonitoringSiteId && x.OrgId == orgId.Value);
            if (m == null) return NotFound();

            m.SiteId = model.SiteId;
            await db.SaveChangesAsync();
            TempData["Message"] = "Monitoring site updated.";
            return RedirectToAction(nameof(Index));
        }
    }

    public class MonitoringSiteRowViewModel
    {
        public int MonitoringSiteId { get; set; }
        public string Name { get; set; } = "";
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string? SiteName { get; set; }
    }

    public class MonitoringSiteEditViewModel
    {
        public int MonitoringSiteId { get; set; }
        public string Name { get; set; } = "";
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public int? SiteId { get; set; }
    }
}
