using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace WEB_Sentro.Data.Migrations.Tenant
{
    public partial class AddIsInherentToRiskEvaluations : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsInherent",
                table: "RiskEvaluations",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsInherent",
                table: "RiskEvaluations");
        }
    }
}

