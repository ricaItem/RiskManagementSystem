// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using WEB_Sentro.Models.Identity;
using WEB_Sentro.Services;

namespace WEB_Sentro.Areas.Identity.Pages.Account
{
    public class LoginModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<LoginModel> _logger;
        private readonly IAuditService _auditService;

        public LoginModel(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            ILogger<LoginModel> logger,
            IAuditService auditService)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _logger = logger;
            _auditService = auditService;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public IList<AuthenticationScheme> ExternalLogins { get; set; }

        public string ReturnUrl { get; set; }

        [TempData]
        public string ErrorMessage { get; set; }

        public class InputModel
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; }

            [Required]
            [DataType(DataType.Password)]
            public string Password { get; set; }

            [Display(Name = "Remember me?")]
            public bool RememberMe { get; set; }
        }

        public async Task<IActionResult> OnGetAsync(string returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                if (User.IsInRole("SuperAdmin")) return LocalRedirect(Url.Content("~/Vendor/Dashboard"));
                if (User.IsInRole("Admin")) return LocalRedirect(Url.Content("~/Client/Dashboard"));
                if (User.IsInRole("RiskManager")) return LocalRedirect(Url.Content("~/Client/Risks/Identification"));
                if (User.IsInRole("ProcurementOfficer")) return LocalRedirect(Url.Content("~/Client/Supplier/Index"));
                if (User.IsInRole("Employee")) return LocalRedirect(Url.Content("~/Client/MyWork"));
                return LocalRedirect(Url.Content("~/Client/Dashboard"));
            }

            if (!string.IsNullOrEmpty(ErrorMessage))
            {
                ModelState.AddModelError(string.Empty, ErrorMessage);
            }

            returnUrl ??= Url.Content("~/");

            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            ReturnUrl = returnUrl;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");

            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            if (!ModelState.IsValid)
                return Page();

            var user = await _userManager.FindByEmailAsync(Input.Email);

            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Invalid credentials.");
                return Page();
            }

            var check = await _signInManager.CheckPasswordSignInAsync(user, Input.Password, lockoutOnFailure: false);
            if (!check.Succeeded)
            {
                if (check.IsLockedOut)
                {
                    await _auditService.LogAsync(user.OrganizationId, user.Id, "Identity", 0, "LoginLocked", "Account locked due to failures", "Warning", HttpContext.Connection.RemoteIpAddress?.ToString());
                    ModelState.AddModelError(string.Empty, "Your account has been locked. Please contact administrator.");
                    return Page();
                }
                if (check.IsNotAllowed)
                {
                    ModelState.AddModelError(string.Empty, "Login not allowed. Please confirm your email first.");
                    return Page();
                }

                await _auditService.LogAsync(user.OrganizationId, user.Id, "Identity", 0, "LoginFailed", "Invalid password attempt", "Warning", HttpContext.Connection.RemoteIpAddress?.ToString());
                ModelState.AddModelError(string.Empty, "Invalid credentials.");
                return Page();
            }

            if (!user.IsActive)
            {
                await _auditService.LogAsync(user.OrganizationId, user.Id, "Identity", 0, "LoginFailed", "Inactive user attempt", "Warning", HttpContext.Connection.RemoteIpAddress?.ToString());
                _logger.LogWarning("Inactive user attempted login. UserId: {UserId}, Email: {Email}", user.Id, user.Email);
                ModelState.AddModelError(string.Empty, "This account has been deactivated. You are not able to log in. Please contact your administrator.");
                return Page();
            }

            await _signInManager.SignInAsync(user, Input.RememberMe);
            await _auditService.LogAsync(user.OrganizationId, user.Id, "Identity", 0, "LoginSuccess", "User logged in successfully", "Success", HttpContext.Connection.RemoteIpAddress?.ToString());
            _logger.LogInformation("User logged in (explicit sign-in). UserId: {UserId}, UserName: {UserName}", user.Id, user.UserName);

            var roles = await _userManager.GetRolesAsync(user);
            _logger.LogDebug("Roles for user {UserName}: {Roles}", user.UserName, string.Join(",", roles));

            if (roles.Contains("SuperAdmin"))
            {
                return LocalRedirect(Url.Content("~/Vendor/Dashboard"));
            }
            if (roles.Contains("Admin"))
            {
                return LocalRedirect(Url.Content("~/Client/Dashboard"));
            }
            if (roles.Contains("RiskManager"))
            {
                return LocalRedirect(Url.Content("~/Client/Risks/Identification"));
            }
            if (roles.Contains("ProcurementOfficer"))
            {
                return LocalRedirect(Url.Content("~/Client/Supplier/Index"));
            }
            if (roles.Contains("Employee"))
            {
                return LocalRedirect(Url.Content("~/Client/MyWork"));
            }

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return LocalRedirect(returnUrl);
            return LocalRedirect(Url.Content("~/Client/Dashboard"));
        }

    }
}
