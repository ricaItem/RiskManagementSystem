using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WEB_Sentro.Data.Migrations.Tenant
{
    [DbContext(typeof(TenantDbContext))]
    [Migration("20260305100000_SupplierRiskAndPOOverdue")]
    public partial class SupplierRiskAndPOOverdue : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ReliabilityScore",
                table: "Suppliers",
                type: "int",
                nullable: false,
                defaultValue: 80);

            migrationBuilder.AddColumn<string>(
                name: "FinancialStatus",
                table: "Suppliers",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Stable");

            migrationBuilder.AddColumn<string>(
                name: "DeliveryTrend",
                table: "Suppliers",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "OnTime");

            migrationBuilder.AddColumn<decimal>(
                name: "ContractValue",
                table: "Suppliers",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "RiskProfileUpdatedAt",
                table: "Suppliers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SupplierId",
                table: "Risks",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpectedDeliveryDate",
                table: "PurchaseOrders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Risks_SupplierId",
                table: "Risks",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_ExpectedDeliveryDate",
                table: "PurchaseOrders",
                column: "ExpectedDeliveryDate");

            migrationBuilder.AddForeignKey(
                name: "FK_Risks_Suppliers_SupplierId",
                table: "Risks",
                column: "SupplierId",
                principalTable: "Suppliers",
                principalColumn: "SupplierId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.CreateTable(
                name: "ProcurementAlerts",
                columns: table => new
                {
                    AlertId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrgId = table.Column<int>(type: "int", nullable: false),
                    PurchaseOrderId = table.Column<int>(type: "int", nullable: false),
                    SupplierId = table.Column<int>(type: "int", nullable: false),
                    AlertCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TriggeredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RiskId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcurementAlerts", x => x.AlertId);
                    table.ForeignKey(
                        name: "FK_ProcurementAlerts_PurchaseOrders_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalTable: "PurchaseOrders",
                        principalColumn: "PurchaseOrderId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementAlerts_Risks_RiskId",
                        column: x => x.RiskId,
                        principalTable: "Risks",
                        principalColumn: "RiskId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProcurementAlerts_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "SupplierId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementAlerts_OrgId",
                table: "ProcurementAlerts",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementAlerts_TriggeredAt",
                table: "ProcurementAlerts",
                column: "TriggeredAt");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementAlerts_PurchaseOrderId",
                table: "ProcurementAlerts",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementAlerts_SupplierId",
                table: "ProcurementAlerts",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementAlerts_RiskId",
                table: "ProcurementAlerts",
                column: "RiskId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Risks_Suppliers_SupplierId",
                table: "Risks");

            migrationBuilder.DropTable(
                name: "ProcurementAlerts");

            migrationBuilder.DropIndex(
                name: "IX_Risks_SupplierId",
                table: "Risks");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrders_ExpectedDeliveryDate",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "ReliabilityScore",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "FinancialStatus",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "DeliveryTrend",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "ContractValue",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "RiskProfileUpdatedAt",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "SupplierId",
                table: "Risks");

            migrationBuilder.DropColumn(
                name: "ExpectedDeliveryDate",
                table: "PurchaseOrders");
        }
    }
}
