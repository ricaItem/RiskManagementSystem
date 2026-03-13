using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WEB_Sentro.Data.Migrations.Tenant
{
    [DbContext(typeof(TenantDbContext))]
    [Migration("20260313123000_AddExpenseAttachmentPathHotfix")]
    public partial class AddExpenseAttachmentPathHotfix : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('Expenses', 'AttachmentPath') IS NULL
BEGIN
    ALTER TABLE [Expenses] ADD [AttachmentPath] nvarchar(max) NULL;
END
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('Expenses', 'AttachmentPath') IS NOT NULL
BEGIN
    ALTER TABLE [Expenses] DROP COLUMN [AttachmentPath];
END
");
        }
    }
}
