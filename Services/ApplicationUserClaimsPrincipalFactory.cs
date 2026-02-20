using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using WEB_Sentro.Models.Identity;

namespace WEB_Sentro.Services;

/// <summary>
/// Adds FirstName, LastName, and FullName claims to the user principal at sign-in
/// so they are available in views (e.g. layout) without loading the user from the database.
/// </summary>
public class ApplicationUserClaimsPrincipalFactory : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>
{
    public const string FullNameClaimType = "FullName";

    public ApplicationUserClaimsPrincipalFactory(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IOptions<IdentityOptions> options)
        : base(userManager, roleManager, options)
    {
    }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        var fullName = $"{user.FirstName} {user.LastName}".Trim();
        if (string.IsNullOrEmpty(fullName))
            fullName = user.Email ?? user.UserName ?? "User";

        identity.AddClaim(new Claim(ClaimTypes.GivenName, user.FirstName ?? ""));
        identity.AddClaim(new Claim(ClaimTypes.Surname, user.LastName ?? ""));
        identity.AddClaim(new Claim(FullNameClaimType, fullName));
        return identity;
    }
}
