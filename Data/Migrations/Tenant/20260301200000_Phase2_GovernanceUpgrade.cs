using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WEB_Sentro.Data.Migrations.Tenant
{
    [DbContext(typeof(TenantDbContext))]
    [Migration("20260301200000_Phase2_GovernanceUpgrade")]
    public partial class Phase2_GovernanceUpgrade : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RiskOwnerId",
                table: "Risks",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "AccountableId",
                table: "Risks",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "TreatmentDecision",
                table: "Risks",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "TreatmentJustification",
                table: "Risks",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
            migrationBuilder.AddColumn<DateTime>(
                name: "TreatmentSelectedAt",
                table: "Risks",
                type: "datetime2",
                nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "TreatmentSelectedByUserId",
                table: "Risks",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);
            migrationBuilder.AddColumn<DateTime>(
                name: "NextReviewDate",
                table: "Risks",
                type: "date",
                nullable: true);
            migrationBuilder.AddColumn<DateTime>(
                name: "LastReviewedAt",
                table: "Risks",
                type: "datetime2",
                nullable: true);
            migrationBuilder.AddColumn<bool>(
                name: "OverdueFlag",
                table: "Risks",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(name: "IX_Risks_OrgId_NextReviewDate", table: "Risks", columns: new[] { "OrgId", "NextReviewDate" });
            migrationBuilder.CreateIndex(name: "IX_Risks_OrgId_OverdueFlag", table: "Risks", columns: new[] { "OrgId", "OverdueFlag" });

            migrationBuilder.CreateTable(
                name: "RiskVersions",
                columns: table => new
                {
                    RiskVersionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RiskId = table.Column<int>(type: "int", nullable: false),
                    VersionNo = table.Column<int>(type: "int", nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ChangedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ChangeSummary = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskVersions", x => x.RiskVersionId);
                    table.ForeignKey(
                        name: "FK_RiskVersions_Risks_RiskId",
                        column: x => x.RiskId,
                        principalTable: "Risks",
                        principalColumn: "RiskId",
                        onDelete: ReferentialAction.Cascade);
                });
            migrationBuilder.CreateIndex(name: "IX_RiskVersions_RiskId", table: "RiskVersions", column: "RiskId");
            migrationBuilder.CreateIndex(name: "IX_RiskVersions_RiskId_VersionNo", table: "RiskVersions", columns: new[] { "RiskId", "VersionNo" });

            migrationBuilder.CreateTable(
                name: "RiskMatrixConfigs",
                columns: table => new
                {
                    RiskMatrixConfigId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrgId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table => table.PrimaryKey("PK_RiskMatrixConfigs", x => x.RiskMatrixConfigId));
            migrationBuilder.CreateIndex(name: "IX_RiskMatrixConfigs_OrgId", table: "RiskMatrixConfigs", column: "OrgId");
            migrationBuilder.CreateIndex(name: "IX_RiskMatrixConfigs_OrgId_IsActive", table: "RiskMatrixConfigs", columns: new[] { "OrgId", "IsActive" });

            migrationBuilder.CreateTable(
                name: "RiskMatrixCells",
                columns: table => new
                {
                    RiskMatrixCellId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RiskMatrixConfigId = table.Column<int>(type: "int", nullable: false),
                    Likelihood = table.Column<int>(type: "int", nullable: false),
                    Impact = table.Column<int>(type: "int", nullable: false),
                    Score = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskMatrixCells", x => x.RiskMatrixCellId);
                    table.ForeignKey(
                        name: "FK_RiskMatrixCells_RiskMatrixConfigs_RiskMatrixConfigId",
                        column: x => x.RiskMatrixConfigId,
                        principalTable: "RiskMatrixConfigs",
                        principalColumn: "RiskMatrixConfigId",
                        onDelete: ReferentialAction.Cascade);
                });
            migrationBuilder.CreateIndex(name: "IX_RiskMatrixCells_RiskMatrixConfigId_Likelihood_Impact", table: "RiskMatrixCells", columns: new[] { "RiskMatrixConfigId", "Likelihood", "Impact" }, unique: true);

            migrationBuilder.CreateTable(
                name: "RiskAppetiteBands",
                columns: table => new
                {
                    RiskAppetiteBandId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RiskMatrixConfigId = table.Column<int>(type: "int", nullable: false),
                    MinScore = table.Column<int>(type: "int", nullable: false),
                    MaxScore = table.Column<int>(type: "int", nullable: false),
                    BandName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ReviewFrequencyDays = table.Column<int>(type: "int", nullable: true),
                    TreatmentTrigger = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskAppetiteBands", x => x.RiskAppetiteBandId);
                    table.ForeignKey(
                        name: "FK_RiskAppetiteBands_RiskMatrixConfigs_RiskMatrixConfigId",
                        column: x => x.RiskMatrixConfigId,
                        principalTable: "RiskMatrixConfigs",
                        principalColumn: "RiskMatrixConfigId",
                        onDelete: ReferentialAction.Cascade);
                });
            migrationBuilder.CreateIndex(name: "IX_RiskAppetiteBands_RiskMatrixConfigId", table: "RiskAppetiteBands", column: "RiskMatrixConfigId");

            migrationBuilder.CreateTable(
                name: "RiskTreatmentTriggers",
                columns: table => new
                {
                    RiskTreatmentTriggerId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RiskMatrixConfigId = table.Column<int>(type: "int", nullable: false),
                    BandName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    MinScore = table.Column<int>(type: "int", nullable: true),
                    MaxScore = table.Column<int>(type: "int", nullable: true),
                    AllowedDecisions = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RequiresJustification = table.Column<bool>(type: "bit", nullable: false),
                    RequiresApproval = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskTreatmentTriggers", x => x.RiskTreatmentTriggerId);
                    table.ForeignKey(
                        name: "FK_RiskTreatmentTriggers_RiskMatrixConfigs_RiskMatrixConfigId",
                        column: x => x.RiskMatrixConfigId,
                        principalTable: "RiskMatrixConfigs",
                        principalColumn: "RiskMatrixConfigId",
                        onDelete: ReferentialAction.Cascade);
                });
            migrationBuilder.CreateIndex(name: "IX_RiskTreatmentTriggers_RiskMatrixConfigId", table: "RiskTreatmentTriggers", column: "RiskMatrixConfigId");

            migrationBuilder.CreateTable(
                name: "Controls",
                columns: table => new
                {
                    ControlId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrgId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    OwnerId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Frequency = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table => table.PrimaryKey("PK_Controls", x => x.ControlId));
            migrationBuilder.CreateIndex(name: "IX_Controls_OrgId", table: "Controls", column: "OrgId");

            migrationBuilder.CreateTable(
                name: "RiskControls",
                columns: table => new
                {
                    RiskControlId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RiskId = table.Column<int>(type: "int", nullable: false),
                    ControlId = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LinkedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskControls", x => x.RiskControlId);
                    table.ForeignKey(
                        name: "FK_RiskControls_Risks_RiskId",
                        column: x => x.RiskId,
                        principalTable: "Risks",
                        principalColumn: "RiskId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RiskControls_Controls_ControlId",
                        column: x => x.ControlId,
                        principalTable: "Controls",
                        principalColumn: "ControlId",
                        onDelete: ReferentialAction.Restrict);
                });
            migrationBuilder.CreateIndex(name: "IX_RiskControls_RiskId_ControlId", table: "RiskControls", columns: new[] { "RiskId", "ControlId" }, unique: true);
            migrationBuilder.CreateIndex(name: "IX_RiskControls_ControlId", table: "RiskControls", column: "ControlId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "RiskControls");
            migrationBuilder.DropTable(name: "Controls");
            migrationBuilder.DropTable(name: "RiskTreatmentTriggers");
            migrationBuilder.DropTable(name: "RiskAppetiteBands");
            migrationBuilder.DropTable(name: "RiskMatrixCells");
            migrationBuilder.DropTable(name: "RiskMatrixConfigs");
            migrationBuilder.DropTable(name: "RiskVersions");

            migrationBuilder.DropIndex(name: "IX_Risks_OrgId_NextReviewDate", table: "Risks");
            migrationBuilder.DropIndex(name: "IX_Risks_OrgId_OverdueFlag", table: "Risks");
            migrationBuilder.DropColumn(name: "RiskOwnerId", table: "Risks");
            migrationBuilder.DropColumn(name: "AccountableId", table: "Risks");
            migrationBuilder.DropColumn(name: "TreatmentDecision", table: "Risks");
            migrationBuilder.DropColumn(name: "TreatmentJustification", table: "Risks");
            migrationBuilder.DropColumn(name: "TreatmentSelectedAt", table: "Risks");
            migrationBuilder.DropColumn(name: "TreatmentSelectedByUserId", table: "Risks");
            migrationBuilder.DropColumn(name: "NextReviewDate", table: "Risks");
            migrationBuilder.DropColumn(name: "LastReviewedAt", table: "Risks");
            migrationBuilder.DropColumn(name: "OverdueFlag", table: "Risks");
        }
    }
}
