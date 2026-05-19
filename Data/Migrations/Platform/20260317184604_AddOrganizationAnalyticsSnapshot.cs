using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WEB_Sentro.Data.Migrations.Platform
{
    /// <inheritdoc />
    public partial class AddOrganizationAnalyticsSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OrganizationAnalyticsSnapshots",
                columns: table => new
                {
                    SnapshotId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizationId = table.Column<int>(type: "int", nullable: false),
                    RangeKey = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    SnapshotAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OrganizationName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PlanName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SubscriptionStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    UserCount = table.Column<int>(type: "int", nullable: false),
                    SeatLimit = table.Column<int>(type: "int", nullable: true),
                    SeatUtilizationPercent = table.Column<int>(type: "int", nullable: false),
                    LoginsInRange = table.Column<int>(type: "int", nullable: false),
                    EventCountInRange = table.Column<int>(type: "int", nullable: false),
                    ActivityChangePercent = table.Column<decimal>(type: "decimal(9,2)", nullable: false),
                    UserChangePercent = table.Column<decimal>(type: "decimal(9,2)", nullable: false),
                    FeatureAdoptionPercent = table.Column<int>(type: "int", nullable: false),
                    AdoptionChangePercent = table.Column<decimal>(type: "decimal(9,2)", nullable: false),
                    ErrorRatePercent = table.Column<decimal>(type: "decimal(9,2)", nullable: false),
                    LastActivityAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    HealthScore = table.Column<int>(type: "int", nullable: false),
                    ChurnRiskLabel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SegmentLabel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TrendLabel = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ActivityTrendJson = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    RenewalDateDisplay = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationAnalyticsSnapshots", x => x.SnapshotId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationAnalyticsSnapshots_OrganizationId_RangeKey",
                table: "OrganizationAnalyticsSnapshots",
                columns: new[] { "OrganizationId", "RangeKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationAnalyticsSnapshots_RangeKey_ChurnRiskLabel",
                table: "OrganizationAnalyticsSnapshots",
                columns: new[] { "RangeKey", "ChurnRiskLabel" });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationAnalyticsSnapshots_RangeKey_EventCountInRange",
                table: "OrganizationAnalyticsSnapshots",
                columns: new[] { "RangeKey", "EventCountInRange" });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationAnalyticsSnapshots_RangeKey_HealthScore",
                table: "OrganizationAnalyticsSnapshots",
                columns: new[] { "RangeKey", "HealthScore" });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationAnalyticsSnapshots_RangeKey_OrganizationName",
                table: "OrganizationAnalyticsSnapshots",
                columns: new[] { "RangeKey", "OrganizationName" });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationAnalyticsSnapshots_SnapshotAtUtc",
                table: "OrganizationAnalyticsSnapshots",
                column: "SnapshotAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrganizationAnalyticsSnapshots");
        }
    }
}
