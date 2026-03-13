using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WEB_Sentro.Data.Migrations.Tenant
{
    [DbContext(typeof(TenantDbContext))]
    [Migration("20260301000000_AddMonitoringSnapshotsRulesAndAlertLifecycle")]
    public partial class AddMonitoringSnapshotsRulesAndAlertLifecycle : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MonitoringSnapshots",
                columns: table => new
                {
                    SnapshotId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrgId = table.Column<int>(type: "int", nullable: false),
                    MonitoringSiteId = table.Column<int>(type: "int", nullable: false),
                    CapturedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Temperature = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: true),
                    WindSpeed = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: true),
                    Humidity = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    RainMm = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: true),
                    Condition = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RawJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table => table.PrimaryKey("PK_MonitoringSnapshots", x => x.SnapshotId));

            migrationBuilder.CreateIndex(
                name: "IX_MonitoringSnapshots_OrgId_MonitoringSiteId",
                table: "MonitoringSnapshots",
                columns: new[] { "OrgId", "MonitoringSiteId" });

            migrationBuilder.CreateIndex(
                name: "IX_MonitoringSnapshots_CapturedAtUtc",
                table: "MonitoringSnapshots",
                column: "CapturedAtUtc");

            migrationBuilder.CreateTable(
                name: "MonitoringRules",
                columns: table => new
                {
                    RuleId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrgId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Metric = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Threshold = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    Operator = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CooldownMinutes = table.Column<int>(type: "int", nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_MonitoringRules", x => x.RuleId));

            migrationBuilder.CreateIndex(
                name: "IX_MonitoringRules_OrgId",
                table: "MonitoringRules",
                column: "OrgId");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "MonitoringAlerts",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Active");

            migrationBuilder.AddColumn<DateTime>(
                name: "ResolvedAtUtc",
                table: "MonitoringAlerts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AcknowledgedAtUtc",
                table: "MonitoringAlerts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AcknowledgedByUserId",
                table: "MonitoringAlerts",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RuleId",
                table: "MonitoringAlerts",
                type: "int",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Status", table: "MonitoringAlerts");
            migrationBuilder.DropColumn(name: "ResolvedAtUtc", table: "MonitoringAlerts");
            migrationBuilder.DropColumn(name: "AcknowledgedAtUtc", table: "MonitoringAlerts");
            migrationBuilder.DropColumn(name: "AcknowledgedByUserId", table: "MonitoringAlerts");
            migrationBuilder.DropColumn(name: "RuleId", table: "MonitoringAlerts");
            migrationBuilder.DropTable(name: "MonitoringSnapshots");
            migrationBuilder.DropTable(name: "MonitoringRules");
        }
    }
}
