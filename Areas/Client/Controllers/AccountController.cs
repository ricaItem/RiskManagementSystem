using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using WEB_Sentro.Areas.Client.Models;
using WEB_Sentro.Models.Identity;

namespace WEB_Sentro.Areas.Client.Controllers
{
    [Area("Client")]
    [Authorize]
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;

        public AccountController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        public async Task<IActionResult> Index(string? message = null, string? error = null)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Challenge();

            var roles = await _userManager.GetRolesAsync(user);
            var (levelDisplay, levelDescription) = GetAccountLevelDisplay(roles);

            var model = new MyAccountViewModel
            {
                FirstName = user.FirstName ?? "",
                LastName = user.LastName ?? "",
                Email = user.Email ?? user.UserName ?? "",
                AccountLevelDisplay = levelDisplay,
                AccountLevelDescription = levelDescription,
                LastLoginAt = user.LastLoginAt,
                Message = message,
                Error = error
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(UpdateProfileInput input)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Challenge();

            if (string.IsNullOrWhiteSpace(input.FirstName) || string.IsNullOrWhiteSpace(input.LastName))
            {
                return RedirectToAction(nameof(Index), new { error = "First name and last name are required." });
            }

            user.FirstName = input.FirstName.Trim();
            user.LastName = input.LastName.Trim();
            user.Email = input.Email?.Trim() ?? user.Email;
            user.UserName = user.Email; // keep UserName in sync if you use email as username

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                var err = string.Join(" ", result.Errors.Select(e => e.Description));
                return RedirectToAction(nameof(Index), new { error = err });
            }

            await _signInManager.RefreshSignInAsync(user);
            return RedirectToAction(nameof(Index), new { message = "Profile updated successfully." });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordInput input)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Challenge();

            if (string.IsNullOrEmpty(input.NewPassword) || input.NewPassword != input.ConfirmPassword)
            {
                return RedirectToAction(nameof(Index), new { error = "New password and confirmation do not match." });
            }

            var result = await _userManager.ChangePasswordAsync(user, input.CurrentPassword, input.NewPassword);
            if (!result.Succeeded)
            {
                var err = string.Join(" ", result.Errors.Select(e => e.Description));
                return RedirectToAction(nameof(Index), new { error = err });
            }

            await _signInManager.SignInAsync(user, isPersistent: false);
            return RedirectToAction(nameof(Index), new { message = "Password changed successfully." });
        }

        private static (string Display, string Description) GetAccountLevelDisplay(IList<string> roles)
        {
            if (roles.Contains("SuperAdmin"))
                return ("Super Admin", "You have global read/write access to all tenants, billing models, and system-wide security settings.");
            if (roles.Contains("Admin"))
                return ("Organization Admin", "You have organization-level admin access.");
            if (roles.Contains("Manager"))
                return ("Manager", "You have manager access within your organization.");
            if (roles.Contains("RiskManager"))
                return ("Risk Manager", "You can manage risks and assessments.");
            if (roles.Contains("ProcurementOfficer"))
                return ("Procurement Officer", "You can manage purchase orders and procurement.");
            if (roles.Contains("Employee"))
                return ("Employee", "You have standard employee access.");
            return ("Organization Member", "You have access to the client portal.");
        }
    }
}
