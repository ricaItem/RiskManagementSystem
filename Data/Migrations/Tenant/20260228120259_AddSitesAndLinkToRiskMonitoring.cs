using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WEB_Sentro.Data.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddSitesAndLinkToRiskMonitoring : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Sites",
                columns: table => new
                {
                    SiteId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrgId = table.Column<int>(type: "int", nullable: false),
                    SiteCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    SiteName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AddressLine = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    City = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Province = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Latitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true),
                    Longitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true),
                    ProjectManagerUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    BudgetAllocated = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_Sites", x => x.SiteId));

            migrationBuilder.CreateIndex(name: "IX_Sites_OrgId", table: "Sites", column: "OrgId");
            migrationBuilder.CreateIndex(name: "IX_Sites_ProjectManagerUserId", table: "Sites", column: "ProjectManagerUserId");
            migrationBuilder.CreateIndex(name: "IX_Sites_SiteCode", table: "Sites", column: "SiteCode", unique: true);
            migrationBuilder.CreateIndex(name: "IX_Sites_Status", table: "Sites", column: "Status");

            migrationBuilder.AddColumn<int>(
                name: "SiteId",
                table: "Risks",
                type: "int",
                nullable: true);
            migrationBuilder.CreateIndex(name: "IX_Risks_SiteId", table: "Risks", column: "SiteId");
            migrationBuilder.AddForeignKey(
                name: "FK_Risks_Sites_SiteId",
                table: "Risks",
                column: "SiteId",
                principalTable: "Sites",
                principalColumn: "SiteId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.RenameColumn(
                name: "SiteId",
                table: "MonitoringSites",
                newName: "MonitoringSiteId");

            migrationBuilder.AddColumn<int>(
                name: "SiteId",
                table: "MonitoringSites",
                type: "int",
                nullable: true);
            migrationBuilder.CreateIndex(name: "IX_MonitoringSites_SiteId", table: "MonitoringSites", column: "SiteId");
            migrationBuilder.AddForeignKey(
                name: "FK_MonitoringSites_Sites_SiteId",
                table: "MonitoringSites",
                column: "SiteId",
                principalTable: "Sites",
                principalColumn: "SiteId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.RenameColumn(
                name: "SiteId",
                table: "MonitoringAlerts",
                newName: "MonitoringSiteId");
            migrationBuilder.DropIndex(name: "IX_MonitoringAlerts_OrgId_SiteId", table: "MonitoringAlerts");
            migrationBuilder.CreateIndex(name: "IX_MonitoringAlerts_OrgId_MonitoringSiteId", table: "MonitoringAlerts", columns: new[] { "OrgId", "MonitoringSiteId" });
            migrationBuilder.AddForeignKey(
                name: "FK_MonitoringAlerts_MonitoringSites_MonitoringSiteId",
                table: "MonitoringAlerts",
                column: "MonitoringSiteId",
                principalTable: "MonitoringSites",
                principalColumn: "MonitoringSiteId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(name: "FK_MonitoringAlerts_MonitoringSites_MonitoringSiteId", table: "MonitoringAlerts");
            migrationBuilder.DropIndex(name: "IX_MonitoringAlerts_OrgId_MonitoringSiteId", table: "MonitoringAlerts");
            migrationBuilder.RenameColumn(name: "MonitoringSiteId", table: "MonitoringAlerts", newName: "SiteId");
            migrationBuilder.CreateIndex(name: "IX_MonitoringAlerts_OrgId_SiteId", table: "MonitoringAlerts", columns: new[] { "OrgId", "SiteId" });

            migrationBuilder.DropForeignKey(name: "FK_MonitoringSites_Sites_SiteId", table: "MonitoringSites");
            migrationBuilder.DropIndex(name: "IX_MonitoringSites_SiteId", table: "MonitoringSites");
            migrationBuilder.DropColumn(name: "SiteId", table: "MonitoringSites");
            migrationBuilder.RenameColumn(name: "MonitoringSiteId", table: "MonitoringSites", newName: "SiteId");

            migrationBuilder.DropForeignKey(name: "FK_Risks_Sites_SiteId", table: "Risks");
            migrationBuilder.DropIndex(name: "IX_Risks_SiteId", table: "Risks");
            migrationBuilder.DropColumn(name: "SiteId", table: "Risks");

            migrationBuilder.DropTable(name: "Sites");
        }
    }
}
