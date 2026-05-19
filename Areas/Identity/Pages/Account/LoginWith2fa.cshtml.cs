using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using WEB_Sentro.Models.Identity;
using WEB_Sentro.Services;

namespace WEB_Sentro.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class LoginWith2faModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<LoginWith2faModel> _logger;
        private readonly IAuditService _auditService;

        public LoginWith2faModel(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            ILogger<LoginWith2faModel> logger,
            IAuditService auditService)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _logger = logger;
            _auditService = auditService;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public bool RememberMe { get; set; }

        public string ReturnUrl { get; set; } = "~/";

        public class InputModel
        {
            [Required]
            [StringLength(7, MinimumLength = 6, ErrorMessage = "Verification code must be 6 or 7 digits.")]
            [DataType(DataType.Text)]
            [Display(Name = "Authenticator code")]
            public string TwoFactorCode { get; set; } = string.Empty;
        }

        public async Task<IActionResult> OnGetAsync(bool rememberMe, string? returnUrl = null)
        {
            var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
            if (user == null)
            {
                return RedirectToPage("./Login");
            }

            ReturnUrl = returnUrl ?? Url.Content("~/");
            RememberMe = rememberMe;

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(bool rememberMe, string? returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");

            if (!ModelState.IsValid)
            {
                ReturnUrl = returnUrl;
                RememberMe = rememberMe;
                return Page();
            }

            var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
            if (user == null)
            {
                return RedirectToPage("./Login");
            }

            var authenticatorCode = Input.TwoFactorCode.Replace(" ", string.Empty).Replace("-", string.Empty);
            var result = await _signInManager.TwoFactorAuthenticatorSignInAsync(authenticatorCode, rememberMe, rememberClient: false);

            if (result.Succeeded)
            {
                await _auditService.LogAsync(user.OrganizationId, user.Id, "Identity", 0, "LoginSuccess", "User completed 2FA login successfully", "Success", HttpContext.Connection.RemoteIpAddress?.ToString());
                _logger.LogInformation("User logged in with 2FA. UserId: {UserId}", user.Id);

                var roles = await _userManager.GetRolesAsync(user);
                if (roles.Contains("SuperAdmin")) return LocalRedirect(Url.Content("~/Vendor/Dashboard"));
                if (roles.Contains("Admin")) return LocalRedirect(Url.Content("~/Client/Dashboard"));
                if (roles.Contains("RiskManager")) return LocalRedirect(Url.Content("~/Client/Risks/Identification"));
                if (roles.Contains("ProcurementOfficer")) return LocalRedirect(Url.Content("~/Client/Supplier/Index"));
                if (roles.Contains("Employee")) return LocalRedirect(Url.Content("~/Client/MyWork"));

                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    return LocalRedirect(returnUrl);
                return LocalRedirect(Url.Content("~/Client/Dashboard"));
            }

            if (result.IsLockedOut)
            {
                await _auditService.LogAsync(user.OrganizationId, user.Id, "Identity", 0, "LoginLocked", "Account locked during 2FA verification", "Warning", HttpContext.Connection.RemoteIpAddress?.ToString());
                ModelState.AddModelError(string.Empty, "Your account has been locked due to multiple failed attempts.");
                ReturnUrl = returnUrl;
                RememberMe = rememberMe;
                return Page();
            }

            await _auditService.LogAsync(user.OrganizationId, user.Id, "Identity", 0, "LoginFailed", "Invalid 2FA code", "Warning", HttpContext.Connection.RemoteIpAddress?.ToString());
            ModelState.AddModelError(string.Empty, "Invalid authenticator code.");
            ReturnUrl = returnUrl;
            RememberMe = rememberMe;
            return Page();
        }
    }
}
