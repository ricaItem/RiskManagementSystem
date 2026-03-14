using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WEB_Sentro.Data;
using WEB_Sentro.Models.Identity;
using Microsoft.AspNetCore.Identity;
using System.Linq;

namespace WEB_Sentro.ViewComponents
{
    public class OrganizationHeaderViewComponent : ViewComponent
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly PlatformDbContext _platformDb;

        public OrganizationHeaderViewComponent(UserManager<ApplicationUser> userManager, PlatformDbContext platformDb)
        {
            _userManager = userManager;
            _platformDb = platformDb;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var user = await _userManager.GetUserAsync(UserClaimsPrincipal);
            if (user == null || user.OrganizationId <= 0)
            {
                // Fallback
                return View(new OrganizationHeaderViewModel { OrgName = "Sentro", LogoPath = "/images/logo1.png" });
            }

            var org = await _platformDb.Organizations
                .AsNoTracking()
                .Where(o => o.OrganizationId == user.OrganizationId)
                .Select(o => new { o.OrgName, o.LogoPath })
                .FirstOrDefaultAsync();

            if (org == null)
            {
                return View(new OrganizationHeaderViewModel { OrgName = "Sentro", LogoPath = "/images/logo1.png" });
            }

            return View(new OrganizationHeaderViewModel
            {
                OrgName = org.OrgName,
                LogoPath = !string.IsNullOrWhiteSpace(org.LogoPath) ? org.LogoPath : "/images/logo1.png"
            });
        }
    }

    public class OrganizationHeaderViewModel
    {
        public string OrgName { get; set; }
        public string LogoPath { get; set; }
    }
}
