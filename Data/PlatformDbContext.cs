using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using WEB_Sentro.Data.Entities;
using WEB_Sentro.Models.Identity;

namespace WEB_Sentro.Data
{
    /// <summary>
    /// Platform database context: Identity + future platform tables.
    /// </summary>
    public class PlatformDbContext : IdentityDbContext<ApplicationUser>
    {
        public DbSet<Organization> Organizations { get; set; } = null!;

        public PlatformDbContext(DbContextOptions<PlatformDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // ApplicationUser configuration (kept in platform DB)
            builder.Entity<ApplicationUser>(e =>
            {
                e.Property(x => x.FirstName).IsRequired();
                e.Property(x => x.LastName).IsRequired();
                e.Property(x => x.CreatedAt);
                e.Property(x => x.LastLoginAt);

                e.HasIndex(x => x.OrganizationId);
            });

            builder.Entity<Organization>(e =>
            {
                e.HasKey(x => x.OrganizationId);

                e.Property(x => x.OrgCode).IsRequired().HasMaxLength(30);
                e.Property(x => x.OrgName).IsRequired().HasMaxLength(200);
                e.Property(x => x.AddressLine).HasMaxLength(200);
                e.Property(x => x.City).HasMaxLength(80);
                e.Property(x => x.Province).HasMaxLength(80);
                e.Property(x => x.Country).HasMaxLength(80);
                e.Property(x => x.PrimaryEmail).HasMaxLength(256);
                e.Property(x => x.PrimaryPhone).HasMaxLength(50);
                e.Property(x => x.PlanName).IsRequired().HasMaxLength(50).HasDefaultValue("Basic");
                e.Property(x => x.Status).IsRequired().HasMaxLength(20).HasDefaultValue("Active");
                e.Property(x => x.CreatedByUserId).HasMaxLength(450);

                e.HasIndex(x => x.OrgCode).IsUnique();
                e.HasIndex(x => x.OrgName);
                e.HasIndex(x => x.Status);
            });
        }
    }
}
