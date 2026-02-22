using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using WEB_Sentro.Data.Entities;
using WEB_Sentro.Models.Identity;

namespace WEB_Sentro.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Risk> Risks { get; set; }
        public DbSet<RiskEvaluation> RiskEvaluations { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<Attachment> Attachments { get; set; }
        public DbSet<MitigationPlan> MitigationPlans { get; set; }
        public DbSet<MitigationTask> MitigationTasks { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Risk>(e =>
            {
                e.ToTable("Risks");
                e.HasKey(x => x.RiskId);
                e.Property(x => x.Title).HasMaxLength(150);
                e.Property(x => x.Description).HasMaxLength(500);
                e.Property(x => x.Category).HasMaxLength(50);
                e.Property(x => x.SourceType).HasMaxLength(50);
                e.Property(x => x.Status).HasMaxLength(20);
                e.Property(x => x.Priority).HasMaxLength(20);
                e.Property(x => x.ProjectSite).HasMaxLength(200);
                e.HasIndex(x => x.OrgId);
                e.HasIndex(x => x.Status);
                e.HasIndex(x => x.CreatedAt);
                e.HasQueryFilter(x => x.DeletedAt == null);
            });

            builder.Entity<RiskEvaluation>(e =>
            {
                e.ToTable("RiskEvaluations");
                e.HasKey(x => x.EvalId);
                e.Property(x => x.RiskLevel).HasMaxLength(20);
                e.Property(x => x.Decision).HasMaxLength(50);
                e.Property(x => x.Remarks).HasMaxLength(255);
                e.HasOne(x => x.Risk).WithMany(r => r.Evaluations).HasForeignKey(x => x.RiskId).OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<AuditLog>(e =>
            {
                e.ToTable("AuditLogs");
                e.HasKey(x => x.AuditId);
                e.Property(x => x.EntityType).HasMaxLength(50);
                e.Property(x => x.ActionType).HasMaxLength(100);
                e.Property(x => x.Level).HasMaxLength(20);
                e.Property(x => x.Message).HasMaxLength(255);
                e.Property(x => x.IpAddress).HasMaxLength(45);
                e.HasIndex(x => x.OrgId);
                e.HasIndex(x => x.CreatedAt);
            });

            builder.Entity<Attachment>(e =>
            {
                e.ToTable("Attachments");
                e.HasKey(x => x.AttachmentId);
                e.Property(x => x.FileName).HasMaxLength(100);
                e.Property(x => x.FileRef).HasMaxLength(255);
                e.HasOne(x => x.Risk).WithMany(r => r.Attachments).HasForeignKey(x => x.RiskId).OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<MitigationPlan>(e =>
            {
                e.ToTable("MitigationPlans");
                e.HasKey(x => x.PlanId);
                e.Property(x => x.CreatedByUserId).HasMaxLength(450);
                e.Property(x => x.StrategyType).HasMaxLength(50);
                e.Property(x => x.Summary).HasMaxLength(255);
                e.Property(x => x.TargetCloseDate).HasColumnType("date");
                e.Property(x => x.Status).HasMaxLength(20);
                e.HasIndex(x => x.RiskId).IsUnique();
                e.HasOne(x => x.Risk).WithOne(r => r.MitigationPlan).HasForeignKey<MitigationPlan>(x => x.RiskId).OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<MitigationTask>(e =>
            {
                e.ToTable("MitigationTasks");
                e.HasKey(x => x.TaskId);
                e.Property(x => x.AssignedToUserId).HasMaxLength(450);
                e.Property(x => x.Title).HasMaxLength(100);
                e.Property(x => x.Description).HasMaxLength(255);
                e.Property(x => x.DueDate).HasColumnType("date");
                e.Property(x => x.Status).HasMaxLength(20);
                e.HasIndex(x => x.PlanId);
                e.HasOne(x => x.Plan).WithMany(p => p.Tasks).HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
