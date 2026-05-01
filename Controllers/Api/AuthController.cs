using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using OtpNet;
using QRCoder;
using WEB_Sentro.Models.Auth;
using WEB_Sentro.Models.Identity;
using WEB_Sentro.Services.Auth;

namespace WEB_Sentro.Controllers.Api;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IMemoryCache _cache;
    private readonly JwtOptions _jwtOptions;

    private const string ChallengePrefix = "2fa_challenge:";

    public AuthController(
        UserManager<ApplicationUser> userManager,
        IJwtTokenService jwtTokenService,
        IMemoryCache cache,
        IOptions<JwtOptions> jwtOptions)
    {
        _userManager = userManager;
        _jwtTokenService = jwtTokenService;
        _cache = cache;
        _jwtOptions = jwtOptions.Value;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var email = request.Email?.Trim();
        if (string.IsNullOrWhiteSpace(email))
            return BadRequest(new { error = "Email is required." });

        if (await _userManager.FindByEmailAsync(email) != null)
            return BadRequest(new { error = "Account already exists." });

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FirstName = request.FirstName?.Trim() ?? string.Empty,
            LastName = request.LastName?.Trim() ?? string.Empty,
            IsActive = true
        };

        var createResult = await _userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            return BadRequest(new { error = string.Join(" ", createResult.Errors.Select(e => e.Description)) });
        }

        return Ok(new { success = true, userId = user.Id });
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var email = request.Email?.Trim();
        if (string.IsNullOrWhiteSpace(email))
            return Unauthorized(new { error = "Invalid credentials." });

        var user = await _userManager.FindByEmailAsync(email);
        if (user == null || !user.IsActive)
            return Unauthorized(new { error = "Invalid credentials." });

        var validPassword = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!validPassword)
            return Unauthorized(new { error = "Invalid credentials." });

        if (await _userManager.GetTwoFactorEnabledAsync(user))
        {
            var challengeToken = Guid.NewGuid().ToString("N");
            _cache.Set(ChallengePrefix + challengeToken, user.Id, TimeSpan.FromMinutes(_jwtOptions.TwoFactorChallengeMinutes));

            return Ok(new
            {
                requiresTwoFactor = true,
                twoFactorToken = challengeToken,
                expiresInSeconds = _jwtOptions.TwoFactorChallengeMinutes * 60
            });
        }

        var roles = await _userManager.GetRolesAsync(user);
        var token = _jwtTokenService.CreateAccessToken(user, roles);
        return Ok(new AuthSuccessResponse(token, _jwtOptions.AccessTokenMinutes * 60));
    }

    [HttpPost("enable-2fa")]
    [Authorize]
    public async Task<IActionResult> EnableTwoFactor()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return Unauthorized();

        var secret = await _userManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrWhiteSpace(secret))
        {
            await _userManager.ResetAuthenticatorKeyAsync(user);
            secret = await _userManager.GetAuthenticatorKeyAsync(user);
        }

        if (string.IsNullOrWhiteSpace(secret))
            return StatusCode(500, new { error = "Unable to generate 2FA secret." });

        var issuer = "Sentro";
        var label = Uri.EscapeDataString($"{issuer}:{user.Email}");
        var otpauth = $"otpauth://totp/{label}?secret={secret}&issuer={Uri.EscapeDataString(issuer)}&digits=6";

        using var qrGenerator = new QRCodeGenerator();
        using var qrData = qrGenerator.CreateQrCode(otpauth, QRCodeGenerator.ECCLevel.Q);
        var qrPng = new PngByteQRCode(qrData);
        var qrBytes = qrPng.GetGraphic(20);
        var qrBase64 = Convert.ToBase64String(qrBytes);

        return Ok(new
        {
            secret,
            qrCodeImage = $"data:image/png;base64,{qrBase64}",
            otpauthUri = otpauth
        });
    }

    [HttpPost("verify-2fa")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyTwoFactor([FromBody] VerifyTwoFactorRequest request)
    {
        var code = request.Code?.Replace(" ", string.Empty).Replace("-", string.Empty);
        if (string.IsNullOrWhiteSpace(code) || code.Length != 6)
            return BadRequest(new { error = "A valid 6-digit code is required." });

        ApplicationUser? user;

        if (!string.IsNullOrWhiteSpace(request.TwoFactorToken))
        {
            if (!_cache.TryGetValue(ChallengePrefix + request.TwoFactorToken, out string? userId) || string.IsNullOrWhiteSpace(userId))
                return Unauthorized(new { error = "2FA challenge expired or invalid." });

            user = await _userManager.FindByIdAsync(userId);
        }
        else
        {
            user = await _userManager.GetUserAsync(User);
        }

        if (user == null)
            return Unauthorized(new { error = "Invalid request." });

        var valid = await _userManager.VerifyTwoFactorTokenAsync(
            user,
            _userManager.Options.Tokens.AuthenticatorTokenProvider,
            code);

        if (!valid)
            return Unauthorized(new { error = "Invalid verification code." });

        if (!await _userManager.GetTwoFactorEnabledAsync(user))
        {
            await _userManager.SetTwoFactorEnabledAsync(user, true);
        }

        if (!string.IsNullOrWhiteSpace(request.TwoFactorToken))
        {
            _cache.Remove(ChallengePrefix + request.TwoFactorToken);
        }

        var roles = await _userManager.GetRolesAsync(user);
        var token = _jwtTokenService.CreateAccessToken(user, roles);
        return Ok(new AuthSuccessResponse(token, _jwtOptions.AccessTokenMinutes * 60));
    }
}

public record RegisterRequest(
    [property: Required, EmailAddress] string Email,
    [property: Required, MinLength(8)] string Password,
    string? FirstName,
    string? LastName);

public record LoginRequest(
    [property: Required, EmailAddress] string Email,
    [property: Required] string Password);

public record VerifyTwoFactorRequest(
    [property: Required, StringLength(6, MinimumLength = 6)] string Code,
    string? TwoFactorToken);

public record AuthSuccessResponse(string AccessToken, int ExpiresInSeconds)
{
    public bool RequiresTwoFactor { get; init; } = false;
};
