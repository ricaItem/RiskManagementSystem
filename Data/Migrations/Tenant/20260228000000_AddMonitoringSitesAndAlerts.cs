using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WEB_Sentro.Data.Migrations.Tenant
{
    [DbContext(typeof(TenantDbContext))]
    [Migration("20260228000000_AddMonitoringSitesAndAlerts")]
    public partial class AddMonitoringSitesAndAlerts : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MonitoringSites",
                columns: table => new
                {   
                    SiteId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrgId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Latitude = table.Column<double>(type: "float", nullable: false),
                    Longitude = table.Column<double>(type: "float", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_MonitoringSites", x => x.SiteId));

            migrationBuilder.CreateIndex(name: "IX_MonitoringSites_OrgId", table: "MonitoringSites", column: "OrgId");

            migrationBuilder.CreateTable(
                name: "MonitoringAlerts",
                columns: table => new
                {
                    AlertId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrgId = table.Column<int>(type: "int", nullable: false),
                    SiteId = table.Column<int>(type: "int", nullable: false),
                    RuleCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RuleName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MeasuredValues = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Severity = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TriggeredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RiskId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table => table.PrimaryKey("PK_MonitoringAlerts", x => x.AlertId));

            migrationBuilder.CreateIndex(name: "IX_MonitoringAlerts_OrgId_SiteId", table: "MonitoringAlerts", columns: new[] { "OrgId", "SiteId" });
            migrationBuilder.CreateIndex(name: "IX_MonitoringAlerts_TriggeredAt", table: "MonitoringAlerts", column: "TriggeredAt");

        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "MonitoringAlerts");
            migrationBuilder.DropTable(name: "MonitoringSites");
        }
    }
}
