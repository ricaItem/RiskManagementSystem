using System.ComponentModel.DataAnnotations;

namespace WEB_Sentro.Areas.Vendor.Models;

public class MyAccountViewModel
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public string AccountLevelDisplay { get; set; } = "";
    public string AccountLevelDescription { get; set; } = "";
    public string? ProfileImagePath { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public string? Message { get; set; }
    public string? Error { get; set; }
    public bool IsTwoFactorEnabled { get; set; }
    public bool HasAuthenticator { get; set; }
    public int RecoveryCodesLeft { get; set; }
    public string? AuthenticatorKey { get; set; }
    public string? AuthenticatorUri { get; set; }
    public string? AuthenticatorQrCodeUrl { get; set; }
}

public class UpdateProfileInput
{
    [Required(ErrorMessage = "First name is required.")]
    [StringLength(100)]
    public string FirstName { get; set; } = "";

    [Required(ErrorMessage = "Last name is required.")]
    [StringLength(100)]
    public string LastName { get; set; } = "";

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress]
    public string Email { get; set; } = "";
}

public class ChangePasswordInput
{
    [Required(ErrorMessage = "Current password is required.")]
    [DataType(DataType.Password)]
    public string CurrentPassword { get; set; } = "";

    [Required(ErrorMessage = "New password is required.")]
    [StringLength(100, MinimumLength = 12, ErrorMessage = "Password must be at least 12 characters.")]
    [DataType(DataType.Password)]
    public string NewPassword { get; set; } = "";

    [DataType(DataType.Password)]
    [Compare("NewPassword", ErrorMessage = "The new password and confirmation do not match.")]
    public string ConfirmPassword { get; set; } = "";
}

public class EnableTwoFactorInput
{
    [Required(ErrorMessage = "Authenticator code is required.")]
    [StringLength(7, MinimumLength = 6, ErrorMessage = "Code must be 6 or 7 digits.")]
    public string Code { get; set; } = "";
}
