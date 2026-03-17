using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using WEB_Sentro.Areas.Vendor.Models;
using WEB_Sentro.Data;
using WEB_Sentro.Data.Entities;
using WEB_Sentro.Models.Identity;

namespace WEB_Sentro.Services;

public interface IOrganizationGovernanceService
{
    Task<GovernanceResult> ProvisionOrganizationAsync(OrganizationCreateViewModel model, string actorId, string? ipAddress, CancellationToken ct = default);
    Task<GovernanceResult> UpdateSubscriptionPlanAsync(int organizationId, string planCode, string actorId, string? ipAddress, CancellationToken ct = default);
    Task<GovernanceResult> ToggleSubscriptionStatusAsync(int organizationId, string actorId, string? ipAddress, CancellationToken ct = default);
    Task<GovernanceResult> ToggleOrganizationStatusAsync(int organizationId, string actorId, string? ipAddress, CancellationToken ct = default);
    Task<GovernanceResult> ArchiveOrganizationAsync(int organizationId, string actorId, string? ipAddress, CancellationToken ct = default);
    Task<GovernanceResult> CreateOrgAdminAsync(int organizationId, string fullName, string email, string actorId, string? ipAddress, CancellationToken ct = default);
    Task<GovernanceResult> ToggleOrgAdminStatusAsync(string userId, string actorId, string? ipAddress, CancellationToken ct = default);
    Task<GovernanceResult> SendOrgAdminPasswordResetAsync(string userId, string actorId, string? ipAddress, CancellationToken ct = default);
}

public sealed class GovernanceResult
{
    public bool Succeeded { get; init; }
    public string Message { get; init; } = string.Empty;

    public static GovernanceResult Success(string message) => new() { Succeeded = true, Message = message };
    public static GovernanceResult Failure(string message) => new() { Succeeded = false, Message = message };
}

public class OrganizationGovernanceService : IOrganizationGovernanceService
{
    private readonly PlatformDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditService _auditService;
    private readonly IRiskMatrixService _riskMatrixService;
    private readonly IEmailSender _emailSender;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public OrganizationGovernanceService(
        PlatformDbContext db,
        UserManager<ApplicationUser> userManager,
        IAuditService auditService,
        IRiskMatrixService riskMatrixService,
        IEmailSender emailSender,
        IHttpContextAccessor httpContextAccessor)
    {
        _db = db;
        _userManager = userManager;
        _auditService = auditService;
        _riskMatrixService = riskMatrixService;
        _emailSender = emailSender;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<GovernanceResult> ProvisionOrganizationAsync(OrganizationCreateViewModel model, string actorId, string? ipAddress, CancellationToken ct = default)
    {
        var normalizedEmail = (model.AdminEmail ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return GovernanceResult.Failure("Admin email is required.");
        }

        var existingUser = await _userManager.FindByEmailAsync(normalizedEmail);
        if (existingUser != null)
        {
            return GovernanceResult.Failure($"User with email {normalizedEmail} already exists.");
        }

        var orgCode = BuildOrgCode(model.OrgName);

        var planEntity = await _db.Plans.FirstOrDefaultAsync(p => p.Code == model.PlanName && p.IsActive, ct)
            ?? await _db.Plans.FirstOrDefaultAsync(p => p.Code == "Basic" && p.IsActive, ct);

        if (planEntity == null)
        {
            return GovernanceResult.Failure("No active plans are available. Seed plans first.");
        }

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var org = new Organization
        {
            OrgName = model.OrgName,
            OrgCode = orgCode,
            PlanName = planEntity.Code,
            Status = "Active",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            PrimaryEmail = normalizedEmail,
            CreatedByUserId = actorId
        };

        _db.Organizations.Add(org);
        await _db.SaveChangesAsync(ct);

        var sub = new Subscription
        {
            OrganizationId = org.OrganizationId,
            PlanId = planEntity.PlanId,
            Status = "Active",
            CurrentPeriodStart = DateTime.UtcNow,
            CurrentPeriodEnd = DateTime.UtcNow.AddMonths(planEntity.BillingInterval.Equals("Year", StringComparison.OrdinalIgnoreCase) ? 12 : 1),
            StartedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Subscriptions.Add(sub);
        await _db.SaveChangesAsync(ct);

        var user = new ApplicationUser
        {
            UserName = normalizedEmail,
            Email = normalizedEmail,
            EmailConfirmed = true,
            OrganizationId = org.OrganizationId,
            FirstName = "Admin",
            LastName = org.OrgName,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var createResult = await _userManager.CreateAsync(user, GenerateBootstrapPassword());
        if (!createResult.Succeeded)
        {
            await tx.RollbackAsync(ct);
            return GovernanceResult.Failure("Failed to create admin user: " + string.Join(", ", createResult.Errors.Select(e => e.Description)));
        }

        var roleResult = await _userManager.AddToRoleAsync(user, "Admin");
        if (!roleResult.Succeeded)
        {
            await _userManager.DeleteAsync(user);
            await tx.RollbackAsync(ct);
            return GovernanceResult.Failure("Failed to assign admin role: " + string.Join(", ", roleResult.Errors.Select(e => e.Description)));
        }

        try
        {
            await _riskMatrixService.EnsureDefaultMatrixAsync(org.OrganizationId);
        }
        catch (Exception ex)
        {
            await _auditService.LogAsync(
                org.OrganizationId,
                actorId,
                "RiskMatrix",
                org.OrganizationId,
                "RiskMatrixDefaultSeedFailed",
                $"Unable to seed default risk matrix for new organization: {ex.Message}",
                "Warning",
                ipAddress);
        }

        var inviteResult = await SendPasswordSetupEmailAsync(user, "You were invited as organization admin", ct);

        await _auditService.LogAsync(
            org.OrganizationId,
            actorId,
            "Organization",
            org.OrganizationId,
            "OrganizationProvisioned",
            $"Provisioned organization '{org.OrgName}' with plan {org.PlanName} and admin {user.Email}",
            "Info",
            ipAddress);

        await tx.CommitAsync(ct);
        return inviteResult.Succeeded
            ? GovernanceResult.Success($"Organization '{org.OrgName}' provisioned. Password setup link sent to {normalizedEmail}.")
            : GovernanceResult.Success($"Organization '{org.OrgName}' provisioned. Admin created but invite email failed: {inviteResult.Message}");
    }

    public async Task<GovernanceResult> UpdateSubscriptionPlanAsync(int organizationId, string planCode, string actorId, string? ipAddress, CancellationToken ct = default)
    {
        if (organizationId <= 0 || string.IsNullOrWhiteSpace(planCode))
        {
            return GovernanceResult.Failure("Invalid plan update request.");
        }

        var org = await _db.Organizations.FirstOrDefaultAsync(o => o.OrganizationId == organizationId, ct);
        if (org == null)
        {
            return GovernanceResult.Failure("Organization not found.");
        }

        if (string.Equals(org.Status, "Archived", StringComparison.OrdinalIgnoreCase))
        {
            return GovernanceResult.Failure("Cannot change plan for archived organizations.");
        }

        var plan = await _db.Plans.FirstOrDefaultAsync(p => p.Code == planCode && p.IsActive, ct);
        if (plan == null)
        {
            return GovernanceResult.Failure("Selected plan is unavailable.");
        }

        org.PlanName = plan.Code;
        org.UpdatedAt = DateTime.UtcNow;

        var subscription = await _db.Subscriptions
            .Where(s => s.OrganizationId == organizationId)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (subscription == null)
        {
            subscription = new Subscription
            {
                OrganizationId = organizationId,
                PlanId = plan.PlanId,
                Status = "Active",
                CurrentPeriodStart = DateTime.UtcNow,
                CurrentPeriodEnd = DateTime.UtcNow.AddMonths(1),
                StartedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.Subscriptions.Add(subscription);
        }
        else
        {
            subscription.PlanId = plan.PlanId;
            subscription.PendingPlanId = null;
            subscription.PendingChangeType = null;
            subscription.PendingChangeEffectiveAt = null;
            subscription.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);

        await _auditService.LogAsync(
            organizationId,
            actorId,
            "Subscription",
            subscription.SubscriptionId,
            "SubscriptionPlanUpdated",
            $"Updated plan to {plan.DisplayName}",
            "Info",
            ipAddress);

        return GovernanceResult.Success($"Updated {org.OrgName} to {plan.DisplayName}.");
    }

    public async Task<GovernanceResult> ToggleSubscriptionStatusAsync(int organizationId, string actorId, string? ipAddress, CancellationToken ct = default)
    {
        var subscription = await _db.Subscriptions
            .Include(s => s.Organization)
            .Where(s => s.OrganizationId == organizationId)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (subscription == null)
        {
            return GovernanceResult.Failure("No subscription found for this organization.");
        }

        if (string.Equals(subscription.Organization.Status, "Archived", StringComparison.OrdinalIgnoreCase))
        {
            return GovernanceResult.Failure("Cannot change subscription status for archived organizations.");
        }

        subscription.Status = string.Equals(subscription.Status, "Active", StringComparison.OrdinalIgnoreCase)
            ? "Suspended"
            : "Active";
        subscription.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _auditService.LogAsync(
            organizationId,
            actorId,
            "Subscription",
            subscription.SubscriptionId,
            "SubscriptionStatusChanged",
            $"Subscription status changed to {subscription.Status}",
            "Warning",
            ipAddress);

        return GovernanceResult.Success($"Subscription for {subscription.Organization.OrgName} is now {subscription.Status}.");
    }

    public async Task<GovernanceResult> ToggleOrganizationStatusAsync(int organizationId, string actorId, string? ipAddress, CancellationToken ct = default)
    {
        var org = await _db.Organizations.FirstOrDefaultAsync(o => o.OrganizationId == organizationId, ct);
        if (org == null)
        {
            return GovernanceResult.Failure("Organization not found.");
        }

        if (string.Equals(org.Status, "Archived", StringComparison.OrdinalIgnoreCase))
        {
            return GovernanceResult.Failure("Archived organizations cannot be reactivated from this action.");
        }

        org.Status = string.Equals(org.Status, "Active", StringComparison.OrdinalIgnoreCase) ? "Suspended" : "Active";
        org.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _auditService.LogAsync(
            org.OrganizationId,
            actorId,
            "Organization",
            org.OrganizationId,
            "OrganizationStatusChanged",
            $"Organization status changed to {org.Status}. Suspended tenants retain read-only user access.",
            "Warning",
            ipAddress);

        return GovernanceResult.Success($"Organization status changed to {org.Status}.");
    }

    public async Task<GovernanceResult> ArchiveOrganizationAsync(int organizationId, string actorId, string? ipAddress, CancellationToken ct = default)
    {
        var org = await _db.Organizations.FirstOrDefaultAsync(o => o.OrganizationId == organizationId, ct);
        if (org == null)
        {
            return GovernanceResult.Failure("Organization not found.");
        }

        var activeSubscription = await _db.Subscriptions.AsNoTracking()
            .Where(s => s.OrganizationId == organizationId)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (activeSubscription != null &&
            string.Equals(activeSubscription.Status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            return GovernanceResult.Failure("Archive blocked: cancel or suspend the active subscription first.");
        }

        org.Status = "Archived";
        org.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _auditService.LogAsync(
            org.OrganizationId,
            actorId,
            "Organization",
            org.OrganizationId,
            "OrganizationArchived",
            $"Organization {org.OrgName} archived",
            "Warning",
            ipAddress);

        return GovernanceResult.Success("Organization archived.");
    }

    public async Task<GovernanceResult> CreateOrgAdminAsync(int organizationId, string fullName, string email, string actorId, string? ipAddress, CancellationToken ct = default)
    {
        if (organizationId <= 0 || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(fullName))
        {
            return GovernanceResult.Failure("Organization, full name, and email are required.");
        }

        var org = await _db.Organizations.AsNoTracking().FirstOrDefaultAsync(o => o.OrganizationId == organizationId, ct);
        if (org == null)
        {
            return GovernanceResult.Failure("Organization not found.");
        }

        if (string.Equals(org.Status, "Archived", StringComparison.OrdinalIgnoreCase))
        {
            return GovernanceResult.Failure("Cannot create admins for archived organizations.");
        }

        email = email.Trim().ToLowerInvariant();
        var existing = await _userManager.FindByEmailAsync(email);
        if (existing != null)
        {
            return GovernanceResult.Failure("Email is already in use.");
        }

        var nameParts = fullName.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var firstName = nameParts.Length > 0 ? nameParts[0] : "Admin";
        var lastName = nameParts.Length > 1 ? nameParts[1] : org.OrgName;

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            OrganizationId = organizationId,
            FirstName = firstName,
            LastName = lastName,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var createResult = await _userManager.CreateAsync(user, GenerateBootstrapPassword());
        if (!createResult.Succeeded)
        {
            return GovernanceResult.Failure(string.Join(" | ", createResult.Errors.Select(e => e.Description)));
        }

        var roleResult = await _userManager.AddToRoleAsync(user, "Admin");
        if (!roleResult.Succeeded)
        {
            await _userManager.DeleteAsync(user);
            return GovernanceResult.Failure(string.Join(" | ", roleResult.Errors.Select(e => e.Description)));
        }

        await _auditService.LogAsync(
            organizationId,
            actorId,
            "User",
            0,
            "OrgAdminCreated",
            $"Created org admin account {user.Email}",
            "Info",
            ipAddress);

        var inviteResult = await SendPasswordSetupEmailAsync(user, "You were invited as organization admin", ct);
        return inviteResult.Succeeded
            ? GovernanceResult.Success($"Admin account created for {email}. Password setup link sent.")
            : GovernanceResult.Success($"Admin account created for {email}. Invite email failed: {inviteResult.Message}");
    }

    public async Task<GovernanceResult> ToggleOrgAdminStatusAsync(string userId, string actorId, string? ipAddress, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return GovernanceResult.Failure("Invalid user request.");
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user == null)
        {
            return GovernanceResult.Failure("User not found.");
        }

        var roles = await _userManager.GetRolesAsync(user);
        if (!roles.Contains("Admin"))
        {
            return GovernanceResult.Failure("Only organization admin accounts can be changed here.");
        }

        user.IsActive = !user.IsActive;
        await _db.SaveChangesAsync(ct);

        await _auditService.LogAsync(
            user.OrganizationId,
            actorId,
            "User",
            0,
            "OrgAdminStatusChanged",
            $"Admin account {user.Email} status set to {(user.IsActive ? "Active" : "Inactive")}",
            "Warning",
            ipAddress);

        return GovernanceResult.Success($"{user.Email} is now {(user.IsActive ? "Active" : "Inactive")}.");
    }

    public async Task<GovernanceResult> SendOrgAdminPasswordResetAsync(string userId, string actorId, string? ipAddress, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return GovernanceResult.Failure("Invalid reset request.");
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return GovernanceResult.Failure("User not found.");
        }

        var roles = await _userManager.GetRolesAsync(user);
        if (!roles.Contains("Admin"))
        {
            return GovernanceResult.Failure("Only organization admin accounts can be reset here.");
        }

        var inviteResult = await SendPasswordSetupEmailAsync(user, "Reset your organization admin password", ct);
        if (!inviteResult.Succeeded)
        {
            return inviteResult;
        }

        await _auditService.LogAsync(
            user.OrganizationId,
            actorId,
            "User",
            0,
            "OrgAdminPasswordResetRequested",
            $"Password reset link sent for admin account {user.Email}",
            "Warning",
            ipAddress);

        return GovernanceResult.Success($"Password reset link sent to {user.Email}.");
    }

    private async Task<GovernanceResult> SendPasswordSetupEmailAsync(ApplicationUser user, string subject, CancellationToken ct)
    {
        try
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

            var callbackPath = $"/Identity/Account/ResetPassword?code={Uri.EscapeDataString(code)}&email={Uri.EscapeDataString(user.Email ?? string.Empty)}";
            var request = _httpContextAccessor.HttpContext?.Request;
            var callbackUrl = request != null
                ? $"{request.Scheme}://{request.Host}{callbackPath}"
                : callbackPath;
            var html = $@"
<div style='font-family: sans-serif; max-width: 520px;'>
  <h2 style='color: #0B1F33;'>Sentro Organization Access</h2>
  <p>Your administrator account has been created or updated. Please set your password to continue.</p>
  <p style='margin: 24px 0;'><a href='{HtmlEncoder.Default.Encode(callbackUrl)}' style='display: inline-block; padding: 12px 24px; background: #0B1F33; color: #fff; text-decoration: none; border-radius: 8px; font-weight: 600;'>Set password</a></p>
  <p style='color: #64748b; font-size: 14px;'>If the button does not work, copy this link and open it from your browser: {HtmlEncoder.Default.Encode(callbackUrl)}</p>
  <p style='margin-top: 24px;'>— Sentro</p>
</div>";

            await _emailSender.SendEmailAsync(user.Email ?? string.Empty, subject, html);
            return GovernanceResult.Success("Invite email sent.");
        }
        catch (Exception ex)
        {
            return GovernanceResult.Failure(ex.Message);
        }
    }

    private static string BuildOrgCode(string orgName)
    {
        var prefix = new string((orgName ?? string.Empty).Where(char.IsLetterOrDigit).Take(4).ToArray()).ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(prefix))
        {
            prefix = "ORG";
        }

        return prefix + Random.Shared.Next(100, 999).ToString();
    }

    private static string GenerateBootstrapPassword()
    {
        return "Tmp!" + Guid.NewGuid().ToString("N")[..20] + "9A";
    }
}
