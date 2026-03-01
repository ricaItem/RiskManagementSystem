using Microsoft.EntityFrameworkCore;
using WEB_Sentro.Data.Entities;

namespace WEB_Sentro.Data
{
    /// <summary>
    /// Tenant database context: operational tenant data only.
    /// </summary>
    public class TenantDbContext : DbContext
    {
        public TenantDbContext(DbContextOptions<TenantDbContext> options)
            : base(options)
        {
        }

        public DbSet<Site> Sites { get; set; } = null!;
        public DbSet<Risk> Risks { get; set; } = null!;
        public DbSet<RiskEvaluation> RiskEvaluations { get; set; } = null!;
        public DbSet<AuditLog> AuditLogs { get; set; } = null!;
        public DbSet<Attachment> Attachments { get; set; } = null!;
        public DbSet<MitigationPlan> MitigationPlans { get; set; } = null!;
        public DbSet<MitigationTask> MitigationTasks { get; set; } = null!;
        public DbSet<MonitoringSite> MonitoringSites { get; set; } = null!;
        public DbSet<MonitoringAlert> MonitoringAlerts { get; set; } = null!;
        public DbSet<MonitoringSnapshot> MonitoringSnapshots { get; set; } = null!;
        public DbSet<MonitoringRule> MonitoringRules { get; set; } = null!;
        public DbSet<Supplier> Suppliers { get; set; } = null!;
        public DbSet<PurchaseOrder> PurchaseOrders { get; set; } = null!;
        public DbSet<PurchaseOrderLine> PurchaseOrderLines { get; set; } = null!;
        public DbSet<Expense> Expenses { get; set; } = null!;
        public DbSet<Notification> Notifications { get; set; } = null!;


        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Site>(e =>
            {
                e.ToTable("Sites");
                e.HasKey(x => x.SiteId);
                e.Property(x => x.SiteCode).HasMaxLength(30);
                e.Property(x => x.SiteName).HasMaxLength(150);
                e.Property(x => x.Status).HasMaxLength(20);
                e.Property(x => x.AddressLine).HasMaxLength(200);
                e.Property(x => x.City).HasMaxLength(80);
                e.Property(x => x.Province).HasMaxLength(80);
                e.Property(x => x.Latitude).HasPrecision(9, 6);
                e.Property(x => x.Longitude).HasPrecision(9, 6);
                e.Property(x => x.ProjectManagerUserId).HasMaxLength(450);
                e.Property(x => x.BudgetAllocated).HasPrecision(18, 2);
                e.HasIndex(x => x.SiteCode).IsUnique();
                e.HasIndex(x => x.Status);
                e.HasIndex(x => x.ProjectManagerUserId);
                e.HasIndex(x => x.OrgId);
            });

            builder.Entity<Risk>(e =>
            {
                e.ToTable("Risks");
                e.HasKey(x => x.RiskId);
                e.Property(x => x.Title).HasMaxLength(150);
                e.Property(x => x.Description).HasMaxLength(500);
                e.Property(x => x.Category).HasMaxLength(50);
                e.Property(x => x.SourceType).HasMaxLength(50);
                e.Property(x => x.MonitoringRuleCode).HasMaxLength(100);
                e.Property(x => x.Status).HasMaxLength(20);
                e.Property(x => x.Priority).HasMaxLength(20);
                e.Property(x => x.ProjectSite).HasMaxLength(200);
                e.HasIndex(x => x.OrgId);
                e.HasIndex(x => x.SiteId);
                e.HasIndex(x => x.Status);
                e.HasIndex(x => x.CreatedAt);
                e.HasQueryFilter(x => x.DeletedAt == null);
                e.HasOne(x => x.Site).WithMany(s => s.Risks).HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.Restrict);
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
                e.Property(x => x.DeletedAt);
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

            builder.Entity<MonitoringSite>(e =>
            {
                e.ToTable("MonitoringSites");
                e.HasKey(x => x.MonitoringSiteId);
                e.Property(x => x.Name).HasMaxLength(100);
                e.HasIndex(x => x.OrgId);
                e.HasIndex(x => x.SiteId);
                e.HasOne(x => x.Site).WithMany(s => s.MonitoringSites).HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<MonitoringAlert>(e =>
            {
                e.ToTable("MonitoringAlerts");
                e.HasKey(x => x.AlertId);
                e.Property(x => x.RuleCode).HasMaxLength(50);
                e.Property(x => x.RuleName).HasMaxLength(100);
                e.Property(x => x.MeasuredValues).HasMaxLength(500);
                e.Property(x => x.Severity).HasMaxLength(20);
                e.Property(x => x.Status).HasMaxLength(20);
                e.Property(x => x.AcknowledgedByUserId).HasMaxLength(450);
                e.HasIndex(x => new { x.OrgId, x.MonitoringSiteId });
                e.HasIndex(x => x.TriggeredAt);
            });

            builder.Entity<MonitoringSnapshot>(e =>
            {
                e.ToTable("MonitoringSnapshots");
                e.HasKey(x => x.SnapshotId);
                e.Property(x => x.Temperature).HasPrecision(6, 2);
                e.Property(x => x.WindSpeed).HasPrecision(6, 2);
                e.Property(x => x.Humidity).HasPrecision(5, 2);
                e.Property(x => x.RainMm).HasPrecision(6, 2);
                e.Property(x => x.Condition).HasMaxLength(100);
                e.Property(x => x.RawJson).HasMaxLength(4000);
                e.HasIndex(x => new { x.OrgId, x.MonitoringSiteId });
                e.HasIndex(x => x.CapturedAtUtc);
            });

            builder.Entity<MonitoringRule>(e =>
            {
                e.ToTable("MonitoringRules");
                e.HasKey(x => x.RuleId);
                e.Property(x => x.Name).HasMaxLength(100);
                e.Property(x => x.Metric).HasMaxLength(50);
                e.Property(x => x.Operator).HasMaxLength(10);
                e.Property(x => x.Severity).HasMaxLength(20);
                e.Property(x => x.Threshold).HasPrecision(10, 2);
                e.HasIndex(x => x.OrgId);
            });

            builder.Entity<Supplier>(e =>
            {
                e.ToTable("Suppliers");
                e.HasKey(x => x.SupplierId);
                e.Property(x => x.Name).HasMaxLength(150);
                e.Property(x => x.ContactPerson).HasMaxLength(100);
                e.Property(x => x.Email).HasMaxLength(150);
                e.Property(x => x.Phone).HasMaxLength(50);
                e.Property(x => x.Category).HasMaxLength(50);
                e.HasIndex(x => x.OrgId);
                e.HasIndex(x => x.Category);
            });

            builder.Entity<PurchaseOrder>(e =>
            {
                e.ToTable("PurchaseOrders");
                e.HasKey(x => x.PurchaseOrderId);
                e.Property(x => x.OrderNumber).HasMaxLength(50);
                e.Property(x => x.Status).HasMaxLength(20);
                e.HasIndex(x => x.OrgId);
                e.HasIndex(x => new { x.SiteId, x.OrderNumber });
                e.HasIndex(x => x.Status);
                e.HasOne(x => x.Site).WithMany(s => s.PurchaseOrders).HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.Supplier).WithMany(s => s.PurchaseOrders).HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<PurchaseOrderLine>(e =>
            {
                e.ToTable("PurchaseOrderLines");
                e.HasKey(x => x.PurchaseOrderLineId);
                e.Property(x => x.Description).HasMaxLength(255);
                e.Property(x => x.Quantity).HasPrecision(18, 4);
                e.Property(x => x.UnitCost).HasPrecision(18, 2);
                e.HasOne(x => x.PurchaseOrder).WithMany(p => p.LineItems).HasForeignKey(x => x.PurchaseOrderId).OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<Expense>(e =>
            {
                e.ToTable("Expenses");
                e.HasKey(x => x.ExpenseId);
                e.Property(x => x.Amount).HasPrecision(18, 2);
                e.Property(x => x.Category).HasMaxLength(50);
                e.HasIndex(x => x.OrgId);
                e.HasIndex(x => x.SiteId);
                e.HasIndex(x => x.Category);
                e.HasIndex(x => x.Date);
                e.HasOne(x => x.Site).WithMany(s => s.Expenses).HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.Risk).WithMany(r => r.Expenses).HasForeignKey(x => x.RiskId).OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.PurchaseOrder).WithMany(p => p.Expenses).HasForeignKey(x => x.PurchaseOrderId).OnDelete(DeleteBehavior.SetNull);
            });

            builder.Entity<Notification>(e =>
            {
                e.ToTable("Notifications");
                e.HasKey(x => x.NotificationId);
                e.Property(x => x.UserId).HasMaxLength(450);
                e.Property(x => x.Title).HasMaxLength(200);
                e.Property(x => x.Message).HasMaxLength(500);
                e.Property(x => x.EntityType).HasMaxLength(50);
                e.HasIndex(x => x.OrgId);
                e.HasIndex(x => x.UserId);
                e.HasIndex(x => x.CreatedAt);
            });
        }
    }
}
