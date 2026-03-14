using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WEB_Sentro.Areas.Vendor.Models;
using WEB_Sentro.Data;
using WEB_Sentro.Data.Entities;
using WEB_Sentro.Models.Identity;

namespace WEB_Sentro.Areas.Vendor.Controllers
{
    [Area("Vendor")]
    [Authorize(Roles = "SuperAdmin")]
    public class OrganizationsController : Controller
    {
        private readonly PlatformDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IUserStore<ApplicationUser> _userStore;
        private readonly IUserEmailStore<ApplicationUser> _emailStore;

        public OrganizationsController(
            PlatformDbContext db,
            UserManager<ApplicationUser> userManager,
            IUserStore<ApplicationUser> userStore)
        {
            _db = db;
            _userManager = userManager;
            _userStore = userStore;
            _emailStore = GetEmailStore();
        }

        private IUserEmailStore<ApplicationUser> GetEmailStore()
        {
            if (!_userManager.SupportsUserEmail)
            {
                throw new NotSupportedException("The default UI requires a user store with email support.");
            }
            return (IUserEmailStore<ApplicationUser>)_userStore;
        }

        public async Task<IActionResult> Index(string? search, string? plan, string? status, CancellationToken ct = default)
        {
            var adminRole = await _db.Roles.FirstOrDefaultAsync(r => r.Name == "Admin", ct);
            var adminUserIds = adminRole != null
                ? await _db.UserRoles.Where(ur => ur.RoleId == adminRole.Id).Select(ur => ur.UserId).ToListAsync(ct)
                : new List<string>();
            var adminCountByOrg = await _db.Users
                .Where(u => adminUserIds.Contains(u.Id))
                .GroupBy(u => u.OrganizationId)
                .Select(g => new { OrgId = g.Key, Count = g.Count() })
                .ToListAsync(ct);
            var adminCountLookup = adminCountByOrg.ToDictionary(x => x.OrgId, x => x.Count);

            var totalRevenueCentavos = await _db.Payments
                .Where(p => p.Status == "Succeeded")
                .SumAsync(p => p.AmountCentavos, ct);
            var totalRevenueDisplay = totalRevenueCentavos % 100 == 0
                ? $"₱{totalRevenueCentavos / 100:N0}"
                : $"₱{totalRevenueCentavos / 100.0:N2}";

            var query = _db.Organizations.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                query = query.Where(o => o.OrgName.Contains(s) || o.OrgCode.Contains(s));
            }
            if (!string.IsNullOrWhiteSpace(plan))
                query = query.Where(o => o.PlanName == plan);
            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(o => o.Status == status);

            var orgs = await query.OrderBy(o => o.OrgName).ToListAsync(ct);
            var activeTenants = await _db.Organizations.CountAsync(o => o.Status == "Active", ct);

            var rows = orgs.Select(o => new OrganizationRowViewModel
            {
                OrganizationId = o.OrganizationId,
                OrgCode = o.OrgCode,
                OrgName = o.OrgName,
                PlanName = o.PlanName,
                AdminCount = adminCountLookup.GetValueOrDefault(o.OrganizationId, 0),
                RiskLoad = null,
                Status = o.Status,
                StatusColor = o.Status == "Active" ? "emerald" : o.Status == "Pending" ? "amber" : "rose"
            }).ToList();

            var model = new OrganizationsIndexViewModel
            {
                Search = search,
                PlanFilter = plan,
                StatusFilter = status,
                Organizations = rows,
                TotalCount = rows.Count,
                TotalRevenueDisplay = totalRevenueDisplay,
                ActiveTenantsCount = activeTenants,
                SystemAvailabilityDisplay = "—"
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(OrganizationCreateViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Invalid form data.";
                return RedirectToAction(nameof(Index));
            }

            // check if email exists
            var existingUser = await _userManager.FindByEmailAsync(model.AdminEmail);
            if (existingUser != null)
            {
                TempData["Error"] = $"User with email {model.AdminEmail} already exists.";
                return RedirectToAction(nameof(Index));
            }

            // Create Organization
            // Generate a simple OrgCode based on name
            var orgCode = new string(model.OrgName.Where(char.IsLetterOrDigit).Take(4).ToArray()).ToUpperInvariant()
                          + new Random().Next(100, 999).ToString();

            var org = new Organization
            {
                OrgName = model.OrgName,
                OrgCode = orgCode,
                PlanName = model.PlanName,
                Status = "Active", // Default to active on creation
                CreatedAt = DateTime.UtcNow,
                PrimaryEmail = model.AdminEmail
            };

            _db.Organizations.Add(org);
            await _db.SaveChangesAsync();

            // Create Admin User
            var user = new ApplicationUser();

            await _userStore.SetUserNameAsync(user, model.AdminEmail, CancellationToken.None);
            await _emailStore.SetEmailAsync(user, model.AdminEmail, CancellationToken.None);

            user.FirstName = "Admin"; // Placeholder
            user.LastName = model.OrgName; // Placeholder
            user.OrganizationId = org.OrganizationId;
            user.IsActive = true;
            user.EmailConfirmed = true; // Auto confirm for manually provisioned accounts

            var result = await _userManager.CreateAsync(user, model.AdminPassword);
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, "Admin");
                TempData["Success"] = $"Organization '{model.OrgName}' provisioned successfully.";
            }
            else
            {
                // Rollback org creation if user creation fails
                _db.Organizations.Remove(org);
                await _db.SaveChangesAsync();

                TempData["Error"] = "Failed to create admin user: " + string.Join(", ", result.Errors.Select(e => e.Description));
            }

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var org = await _db.Organizations.FindAsync(id);
            if (org == null) return NotFound();

            var model = new OrganizationEditViewModel
            {
                OrganizationId = org.OrganizationId,
                OrgName = org.OrgName,
                OrgCode = org.OrgCode,
                PlanName = org.PlanName,
                Status = org.Status,
                Website = org.Website,
                PrimaryEmail = org.PrimaryEmail,
                PrimaryPhone = org.PrimaryPhone,
                AddressLine = org.AddressLine,
                City = org.City,
                Country = org.Country,
                TaxId = org.TaxId
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(int id, OrganizationEditViewModel model)
        {
            if (id != model.OrganizationId) return BadRequest();
            
            if (!ModelState.IsValid) return View("Edit", model);

            var org = await _db.Organizations.FindAsync(id);
            if (org == null) return NotFound();

            org.OrgName = model.OrgName;
            org.OrgCode = model.OrgCode;
            org.PlanName = model.PlanName;
            org.Status = model.Status;
            org.Website = model.Website;
            org.PrimaryEmail = model.PrimaryEmail;
            org.PrimaryPhone = model.PrimaryPhone;
            org.AddressLine = model.AddressLine;
            org.City = model.City;
            org.Country = model.Country;
            org.TaxId = model.TaxId;
            org.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            TempData["Success"] = "Organization updated successfully.";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var org = await _db.Organizations.FindAsync(id);
            if (org == null) return NotFound();

            if (org.Status == "Active")
                org.Status = "Suspended";
            else
                org.Status = "Active";

            org.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            TempData["Success"] = $"Organization status changed to {org.Status}.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
             var org = await _db.Organizations.FindAsync(id);
             if (org == null) return NotFound();

             // Soft delete? Or hard delete?
             // Usually soft delete is better, but schema might not have IsDeleted.
             // Checking Organization entity... it doesn't seem to have IsDeleted. 
             // But status can be "Suspended". Let's assume Delete means really remove or mark as some archived status.
             // For now, let's just set Status to "Archived" if valid, or just delete if it's a test data.
             // Given the request "Soft Delete", let's use a status or see if we can add a property.
             // The entity doesn't have IsDeleted.
             // Let's check `Organization.cs` again...
             // It has Status. I can use "Archived" status as Soft Delete.

             org.Status = "Archived";
             org.UpdatedAt = DateTime.UtcNow;
             await _db.SaveChangesAsync();
             
             TempData["Success"] = "Organization archived.";
             return RedirectToAction(nameof(Index));
        }
    }
}
