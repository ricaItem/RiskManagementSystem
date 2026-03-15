using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WEB_Sentro.Data.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddCostCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CostCodeId",
                table: "PurchaseOrderLines",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CostCodeId",
                table: "Expenses",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CostCodes",
                columns: table => new
                {
                    CostCodeId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrgId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    ParentCostCodeId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CostCodes", x => x.CostCodeId);
                    table.ForeignKey(
                        name: "FK_CostCodes_CostCodes_ParentCostCodeId",
                        column: x => x.ParentCostCodeId,
                        principalTable: "CostCodes",
                        principalColumn: "CostCodeId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderLines_CostCodeId",
                table: "PurchaseOrderLines",
                column: "CostCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_CostCodeId",
                table: "Expenses",
                column: "CostCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_CostCodes_Code",
                table: "CostCodes",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "IX_CostCodes_OrgId",
                table: "CostCodes",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_CostCodes_OrgId_Code",
                table: "CostCodes",
                columns: new[] { "OrgId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CostCodes_ParentCostCodeId",
                table: "CostCodes",
                column: "ParentCostCodeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Expenses_CostCodes_CostCodeId",
                table: "Expenses",
                column: "CostCodeId",
                principalTable: "CostCodes",
                principalColumn: "CostCodeId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrderLines_CostCodes_CostCodeId",
                table: "PurchaseOrderLines",
                column: "CostCodeId",
                principalTable: "CostCodes",
                principalColumn: "CostCodeId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Expenses_CostCodes_CostCodeId",
                table: "Expenses");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrderLines_CostCodes_CostCodeId",
                table: "PurchaseOrderLines");

            migrationBuilder.DropTable(
                name: "CostCodes");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrderLines_CostCodeId",
                table: "PurchaseOrderLines");

            migrationBuilder.DropIndex(
                name: "IX_Expenses_CostCodeId",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "CostCodeId",
                table: "PurchaseOrderLines");

            migrationBuilder.DropColumn(
                name: "CostCodeId",
                table: "Expenses");
        }
    }
}
