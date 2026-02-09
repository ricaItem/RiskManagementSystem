using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using WEB_Sentro.Models.Identity;

namespace WEB_Sentro.Data.Seed
{
    public static class IdentitySeeder
    {
        public static async Task SeedAsync(IServiceProvider services, IConfiguration config)
        {
            var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

            var roles = new[] { "SuperAdmin", "Admin", "Manager", "Employee", "ProcurementOfficer" };

            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                    await roleManager.CreateAsync(new IdentityRole(role));
            }

            var email = config["Seed:SuperAdminEmail"];
            var password = config["Seed:SuperAdminPassword"];

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                return;

            var user = await userManager.FindByEmailAsync(email);
            if (user == null)
            {
                user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true,

                    OrganizationId = 0, // required (int not null)

                    FirstName = config["Seed:SuperAdminFirstName"] ?? "System",
                    LastName = config["Seed:SuperAdminLastName"] ?? "Administrator",

                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };



                var createResult = await userManager.CreateAsync(user, password);
                if (!createResult.Succeeded)
                    throw new Exception(string.Join("; ", createResult.Errors.Select(e => e.Description)));
            }

            if (!await userManager.IsInRoleAsync(user, "SuperAdmin"))
            {
                var addRoleResult = await userManager.AddToRoleAsync(user, "SuperAdmin");
                if (!addRoleResult.Succeeded)
                    throw new Exception(string.Join("; ", addRoleResult.Errors.Select(e => e.Description)));
            }

            // Seed an Admin account 
            var adminEmail = config["Seed:AdminEmail"];
            var adminPassword = config["Seed:AdminPassword"];

            if (!string.IsNullOrWhiteSpace(adminEmail) && !string.IsNullOrWhiteSpace(adminPassword))
            {
                var adminUser = await userManager.FindByEmailAsync(adminEmail);
                if (adminUser == null)
                {
                    adminUser = new ApplicationUser
                    {
                        UserName = adminEmail,
                        Email = adminEmail,
                        EmailConfirmed = true,

                        OrganizationId = 0,

                        FirstName = config["Seed:AdminFirstName"] ?? "System",
                        LastName = config["Seed:AdminLastName"] ?? "Admin",

                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    };

                    var createAdminResult = await userManager.CreateAsync(adminUser, adminPassword);
                    if (!createAdminResult.Succeeded)
                        throw new Exception(string.Join("; ", createAdminResult.Errors.Select(e => e.Description)));
                }

                if (!await userManager.IsInRoleAsync(adminUser, "Admin"))
                {
                    var addAdminRoleResult = await userManager.AddToRoleAsync(adminUser, "Admin");
                    if (!addAdminRoleResult.Succeeded)
                        throw new Exception(string.Join("; ", addAdminRoleResult.Errors.Select(e => e.Description)));
                }
            }
        }
    }
}
