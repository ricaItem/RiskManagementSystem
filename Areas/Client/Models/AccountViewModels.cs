using System.ComponentModel.DataAnnotations;

namespace WEB_Sentro.Areas.Client.Models
{
    /// <summary>
    /// View model for the My Account index page (display + profile form).
    /// </summary>
    public class MyAccountViewModel
    {
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string Email { get; set; } = "";
        public string AccountLevelDisplay { get; set; } = "";
        public string AccountLevelDescription { get; set; } = "";
        public string? ProfileImagePath { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public bool IsTwoFactorEnabled { get; set; }
        public bool ShowTwoFactorSetup { get; set; }
        public string? TwoFactorQrCodeImage { get; set; }
        public string? TwoFactorSharedKey { get; set; }
        public string? TwoFactorOtpAuthUri { get; set; }
        public string? Message { get; set; }
        public string? Error { get; set; }
    }

    /// <summary>
    /// Input for updating profile (full name and email).
    /// </summary>
    public class UpdateProfileInput
    {
        [Required(ErrorMessage = "First name is required.")]
        [StringLength(100)]
        [Display(Name = "First name")]
        public string FirstName { get; set; } = "";

        [Required(ErrorMessage = "Last name is required.")]
        [StringLength(100)]
        [Display(Name = "Last name")]
        public string LastName { get; set; } = "";

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; } = "";
    }

    /// <summary>
    /// Input for changing password.
    /// </summary>
    public class ChangePasswordInput
    {
        [Required(ErrorMessage = "Current password is required.")]
        [DataType(DataType.Password)]
        [Display(Name = "Current password")]
        public string CurrentPassword { get; set; } = "";

        [Required(ErrorMessage = "New password is required.")]
        [StringLength(100, MinimumLength = 12, ErrorMessage = "Password must be at least 12 characters.")]
        [DataType(DataType.Password)]
        [Display(Name = "New password")]
        public string NewPassword { get; set; } = "";

        [DataType(DataType.Password)]
        [Display(Name = "Confirm new password")]
        [Compare("NewPassword", ErrorMessage = "The new password and confirmation do not match.")]
        public string ConfirmPassword { get; set; } = "";
    }
}
