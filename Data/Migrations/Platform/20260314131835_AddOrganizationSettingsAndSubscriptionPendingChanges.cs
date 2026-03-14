using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WEB_Sentro.Data.Migrations.Platform
{
    /// <inheritdoc />
    public partial class AddOrganizationSettingsAndSubscriptionPendingChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PendingChangeEffectiveAt",
                table: "Subscriptions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PendingChangeType",
                table: "Subscriptions",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PendingPlanId",
                table: "Subscriptions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BillingAddressLine1",
                table: "Organizations",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BillingAddressLine2",
                table: "Organizations",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BillingCity",
                table: "Organizations",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BillingContactName",
                table: "Organizations",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BillingCountry",
                table: "Organizations",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BillingEmail",
                table: "Organizations",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BillingPhone",
                table: "Organizations",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BillingPostalCode",
                table: "Organizations",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BillingProvince",
                table: "Organizations",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DisplayName",
                table: "Organizations",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegalAddressLine1",
                table: "Organizations",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegalAddressLine2",
                table: "Organizations",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegalCity",
                table: "Organizations",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegalCountry",
                table: "Organizations",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegalPostalCode",
                table: "Organizations",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegalProvince",
                table: "Organizations",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LogoPath",
                table: "Organizations",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TaxId",
                table: "Organizations",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Website",
                table: "Organizations",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_PendingPlanId",
                table: "Subscriptions",
                column: "PendingPlanId");

            migrationBuilder.AddForeignKey(
                name: "FK_Subscriptions_Plans_PendingPlanId",
                table: "Subscriptions",
                column: "PendingPlanId",
                principalTable: "Plans",
                principalColumn: "PlanId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Subscriptions_Plans_PendingPlanId",
                table: "Subscriptions");

            migrationBuilder.DropIndex(
                name: "IX_Subscriptions_PendingPlanId",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "PendingChangeEffectiveAt",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "PendingChangeType",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "PendingPlanId",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "BillingAddressLine1",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "BillingAddressLine2",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "BillingCity",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "BillingContactName",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "BillingCountry",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "BillingEmail",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "BillingPhone",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "BillingPostalCode",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "BillingProvince",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "DisplayName",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "LegalAddressLine1",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "LegalAddressLine2",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "LegalCity",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "LegalCountry",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "LegalPostalCode",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "LegalProvince",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "LogoPath",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "TaxId",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "Website",
                table: "Organizations");
        }
    }
}
