using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WEB_Sentro.Data.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddEmployeeNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmployeeNotes",
                columns: table => new
                {
                    EmployeeNoteId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrgId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(1200)", maxLength: 1200, nullable: false),
                    Pinned = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeNotes", x => x.EmployeeNoteId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeNotes_OrgId",
                table: "EmployeeNotes",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeNotes_OrgId_UserId_Pinned",
                table: "EmployeeNotes",
                columns: new[] { "OrgId", "UserId", "Pinned" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeNotes_UpdatedAt",
                table: "EmployeeNotes",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeNotes_UserId",
                table: "EmployeeNotes",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmployeeNotes");
        }
    }
}
