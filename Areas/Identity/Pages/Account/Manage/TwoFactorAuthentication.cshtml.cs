// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
﻿#nullable disable

using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using QRCoder;
using WEB_Sentro.Models.Identity;

namespace WEB_Sentro.Areas.Identity.Pages.Account.Manage
{
    public class TwoFactorAuthenticationModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;

        public TwoFactorAuthenticationModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [BindProperty]
        public InputModel Input { get; set; } = new();

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public bool Is2faEnabled { get; set; }

        public string SharedKey { get; set; }

        public string QrCodeImage { get; set; }

        public string OtpAuthUri { get; set; }

        public class InputModel
        {
            [Required]
            [StringLength(6, MinimumLength = 6)]
            [Display(Name = "Verification code")]
            public string Code { get; set; }
        }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [TempData]
        public string StatusMessage { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }
            Is2faEnabled = await _userManager.GetTwoFactorEnabledAsync(user);
            await LoadSetupDataAsync(user);

            return Page();
        }

        public async Task<IActionResult> OnPostVerifyAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            if (!ModelState.IsValid)
            {
                Is2faEnabled = await _userManager.GetTwoFactorEnabledAsync(user);
                await LoadSetupDataAsync(user);
                return Page();
            }

            var code = Input.Code.Replace(" ", string.Empty).Replace("-", string.Empty);
            var isValid = await _userManager.VerifyTwoFactorTokenAsync(user, _userManager.Options.Tokens.AuthenticatorTokenProvider, code);
            if (!isValid)
            {
                ModelState.AddModelError("Input.Code", "Invalid verification code.");
                Is2faEnabled = await _userManager.GetTwoFactorEnabledAsync(user);
                await LoadSetupDataAsync(user);
                return Page();
            }

            await _userManager.SetTwoFactorEnabledAsync(user, true);
            await _signInManager.RefreshSignInAsync(user);
            StatusMessage = "Two-factor authentication has been enabled.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDisableAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            await _userManager.SetTwoFactorEnabledAsync(user, false);
            await _signInManager.RefreshSignInAsync(user);
            StatusMessage = "Two-factor authentication has been disabled.";
            return RedirectToPage();
        }

        private async Task LoadSetupDataAsync(ApplicationUser user)
        {
            var key = await _userManager.GetAuthenticatorKeyAsync(user);
            if (string.IsNullOrWhiteSpace(key))
            {
                await _userManager.ResetAuthenticatorKeyAsync(user);
                key = await _userManager.GetAuthenticatorKeyAsync(user);
            }

            SharedKey = key ?? string.Empty;
            var issuer = "Sentro";
            var email = user.Email ?? user.UserName ?? "user";
            var label = Uri.EscapeDataString($"{issuer}:{email}");
            OtpAuthUri = $"otpauth://totp/{label}?secret={SharedKey}&issuer={Uri.EscapeDataString(issuer)}&digits=6";

            using var qrGenerator = new QRCodeGenerator();
            using var qrData = qrGenerator.CreateQrCode(OtpAuthUri, QRCodeGenerator.ECCLevel.Q);
            var qrPng = new PngByteQRCode(qrData);
            var qrBytes = qrPng.GetGraphic(20);
            QrCodeImage = $"data:image/png;base64,{Convert.ToBase64String(qrBytes)}";
        }
    }
}
