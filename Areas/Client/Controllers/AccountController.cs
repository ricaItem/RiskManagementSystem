using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;
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
        private readonly IWebHostEnvironment _env;

        public AccountController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, IWebHostEnvironment env)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _env = env;
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
                ProfileImagePath = user.ProfileImagePath,
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfilePhoto(IFormFile? profilePhoto)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Challenge();

            if (profilePhoto == null || profilePhoto.Length == 0)
                return RedirectToAction(nameof(Index), new { error = "Please choose an image file to upload." });

            const long maxBytes = 2 * 1024 * 1024;
            if (profilePhoto.Length > maxBytes)
                return RedirectToAction(nameof(Index), new { error = "Profile photo must be 2 MB or smaller." });

            var ext = Path.GetExtension(profilePhoto.FileName)?.ToLowerInvariant();
            var allowedExt = new HashSet<string> { ".jpg", ".jpeg", ".png", ".webp" };
            if (string.IsNullOrEmpty(ext) || !allowedExt.Contains(ext))
                return RedirectToAction(nameof(Index), new { error = "Allowed file types: JPG, PNG, WEBP." });

            var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "profile", user.Id);
            Directory.CreateDirectory(uploadsFolder);

            var fileName = $"avatar_{Guid.NewGuid():N}{ext}";
            var filePath = Path.Combine(uploadsFolder, fileName);
            await using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await profilePhoto.CopyToAsync(stream);
            }

            if (!string.IsNullOrWhiteSpace(user.ProfileImagePath) && user.ProfileImagePath.StartsWith("/uploads/profile/", StringComparison.OrdinalIgnoreCase))
            {
                var previous = Path.Combine(_env.WebRootPath, user.ProfileImagePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                if (System.IO.File.Exists(previous))
                    System.IO.File.Delete(previous);
            }

            user.ProfileImagePath = $"/uploads/profile/{user.Id}/{fileName}";
            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                var err = string.Join(" ", result.Errors.Select(e => e.Description));
                return RedirectToAction(nameof(Index), new { error = err });
            }

            await _signInManager.RefreshSignInAsync(user);
            return RedirectToAction(nameof(Index), new { message = "Profile photo updated successfully." });
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
