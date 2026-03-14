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
        public DbSet<Plan> Plans { get; set; } = null!;
        public DbSet<Subscription> Subscriptions { get; set; } = null!;
        public DbSet<Invoice> Invoices { get; set; } = null!;
        public DbSet<Payment> Payments { get; set; } = null!;

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

            builder.Entity<Plan>(e =>
            {
                e.HasKey(x => x.PlanId);
                e.Property(x => x.Code).IsRequired().HasMaxLength(50);
                e.Property(x => x.DisplayName).IsRequired().HasMaxLength(100);
                e.Property(x => x.Currency).IsRequired().HasMaxLength(3);
                e.Property(x => x.BillingInterval).IsRequired().HasMaxLength(20);
                e.HasIndex(x => x.Code).IsUnique();
            });

            builder.Entity<Subscription>(e =>
            {
                e.HasKey(x => x.SubscriptionId);
                e.Property(x => x.Status).IsRequired().HasMaxLength(20);
                e.HasIndex(x => x.OrganizationId);
                e.HasIndex(x => x.PlanId);
                e.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.Plan).WithMany().HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<Invoice>(e =>
            {
                e.HasKey(x => x.InvoiceId);
                e.Property(x => x.InvoiceNumber).IsRequired().HasMaxLength(50);
                e.Property(x => x.Status).IsRequired().HasMaxLength(20);
                e.Property(x => x.Currency).IsRequired().HasMaxLength(3);
                e.Property(x => x.CreatedByUserId).HasMaxLength(450);
                e.HasIndex(x => x.InvoiceNumber).IsUnique();
                e.HasIndex(x => x.OrganizationId);
                e.HasIndex(x => x.SubscriptionId);
                e.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.Subscription).WithMany().HasForeignKey(x => x.SubscriptionId).OnDelete(DeleteBehavior.SetNull);
            });

            builder.Entity<Payment>(e =>
            {
                e.HasKey(x => x.PaymentId);
                e.Property(x => x.Gateway).IsRequired().HasMaxLength(30);
                e.Property(x => x.GatewayPaymentIntentId).IsRequired().HasMaxLength(100);
                e.Property(x => x.GatewayStatus).HasMaxLength(50);
                e.Property(x => x.Currency).IsRequired().HasMaxLength(3);
                e.Property(x => x.PaymentMethod).HasMaxLength(30);
                e.Property(x => x.Status).IsRequired().HasMaxLength(20);
                e.Property(x => x.CreatedByUserId).HasMaxLength(450);
                e.HasIndex(x => x.OrganizationId);
                e.HasIndex(x => x.InvoiceId);
                e.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.Invoice).WithMany().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.SetNull);
            });
        }
    }
}
