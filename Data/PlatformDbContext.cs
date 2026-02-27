using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using WEB_Sentro.Models.Identity;

namespace WEB_Sentro.Data
{
    /// <summary>
    /// Platform database context: Identity + future platform tables.
    /// </summary>
    public class PlatformDbContext : IdentityDbContext<ApplicationUser>
    {
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
        }
    }
}
