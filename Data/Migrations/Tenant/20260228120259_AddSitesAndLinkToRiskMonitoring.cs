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

            // Fix MonitoringSites: rename SiteId -> MonitoringSiteId if needed, then add SiteId FK to Sites.
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[dbo].[MonitoringSites]', N'U') IS NOT NULL
BEGIN
    -- If legacy PK column is still named SiteId, rename it to MonitoringSiteId.
    IF COL_LENGTH(N'dbo.MonitoringSites', N'SiteId') IS NOT NULL
       AND COL_LENGTH(N'dbo.MonitoringSites', N'MonitoringSiteId') IS NULL
    BEGIN
        EXEC sp_rename N'[dbo].[MonitoringSites].[SiteId]', N'MonitoringSiteId', N'COLUMN';
    END

    -- Ensure Sites FK column exists
    IF COL_LENGTH(N'dbo.MonitoringSites', N'SiteId') IS NULL
    BEGIN
        ALTER TABLE [dbo].[MonitoringSites] ADD [SiteId] int NULL;
    END

    -- Ensure index on SiteId exists
    IF NOT EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE name = N'IX_MonitoringSites_SiteId'
          AND object_id = OBJECT_ID(N'dbo.MonitoringSites')
    )
    BEGIN
        CREATE INDEX [IX_MonitoringSites_SiteId] ON [dbo].[MonitoringSites]([SiteId]);
    END

    -- Ensure FK exists
    IF NOT EXISTS (
        SELECT 1 FROM sys.foreign_keys
        WHERE name = N'FK_MonitoringSites_Sites_SiteId'
          AND parent_object_id = OBJECT_ID(N'dbo.MonitoringSites')
    )
    BEGIN
        ALTER TABLE [dbo].[MonitoringSites] ADD CONSTRAINT [FK_MonitoringSites_Sites_SiteId]
            FOREIGN KEY ([SiteId]) REFERENCES [dbo].[Sites]([SiteId]) ON DELETE NO ACTION;
    END
END
");

            // Fix MonitoringAlerts: rename SiteId -> MonitoringSiteId safely (schema-qualified) and update index + FK.
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[dbo].[MonitoringAlerts]', N'U') IS NOT NULL
BEGIN
    -- Rename only if old column exists and new one doesn't
    IF COL_LENGTH(N'dbo.MonitoringAlerts', N'SiteId') IS NOT NULL
       AND COL_LENGTH(N'dbo.MonitoringAlerts', N'MonitoringSiteId') IS NULL
    BEGIN
        EXEC sp_rename N'[dbo].[MonitoringAlerts].[SiteId]', N'MonitoringSiteId', N'COLUMN';
    END

    -- Drop legacy index if it exists
    IF EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE name = N'IX_MonitoringAlerts_OrgId_SiteId'
          AND object_id = OBJECT_ID(N'dbo.MonitoringAlerts')
    )
    BEGIN
        DROP INDEX [IX_MonitoringAlerts_OrgId_SiteId] ON [dbo].[MonitoringAlerts];
    END

    -- Create new index if it doesn't exist
    IF NOT EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE name = N'IX_MonitoringAlerts_OrgId_MonitoringSiteId'
          AND object_id = OBJECT_ID(N'dbo.MonitoringAlerts')
    )
    BEGIN
        CREATE INDEX [IX_MonitoringAlerts_OrgId_MonitoringSiteId]
            ON [dbo].[MonitoringAlerts]([OrgId], [MonitoringSiteId]);
    END

    -- Add FK only if it doesn't exist AND column exists
    IF COL_LENGTH(N'dbo.MonitoringAlerts', N'MonitoringSiteId') IS NOT NULL
       AND OBJECT_ID(N'[dbo].[MonitoringSites]', N'U') IS NOT NULL
       AND NOT EXISTS (
           SELECT 1 FROM sys.foreign_keys
           WHERE name = N'FK_MonitoringAlerts_MonitoringSites_MonitoringSiteId'
             AND parent_object_id = OBJECT_ID(N'dbo.MonitoringAlerts')
       )
    BEGIN
        ALTER TABLE [dbo].[MonitoringAlerts] WITH CHECK
        ADD CONSTRAINT [FK_MonitoringAlerts_MonitoringSites_MonitoringSiteId]
            FOREIGN KEY ([MonitoringSiteId]) REFERENCES [dbo].[MonitoringSites]([MonitoringSiteId]) ON DELETE NO ACTION;
    END
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverse MonitoringAlerts changes safely
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[dbo].[MonitoringAlerts]', N'U') IS NOT NULL
BEGIN
    IF EXISTS (
        SELECT 1 FROM sys.foreign_keys
        WHERE name = N'FK_MonitoringAlerts_MonitoringSites_MonitoringSiteId'
          AND parent_object_id = OBJECT_ID(N'dbo.MonitoringAlerts')
    )
    BEGIN
        ALTER TABLE [dbo].[MonitoringAlerts]
        DROP CONSTRAINT [FK_MonitoringAlerts_MonitoringSites_MonitoringSiteId];
    END

    IF EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE name = N'IX_MonitoringAlerts_OrgId_MonitoringSiteId'
          AND object_id = OBJECT_ID(N'dbo.MonitoringAlerts')
    )
    BEGIN
        DROP INDEX [IX_MonitoringAlerts_OrgId_MonitoringSiteId] ON [dbo].[MonitoringAlerts];
    END

    IF NOT EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE name = N'IX_MonitoringAlerts_OrgId_SiteId'
          AND object_id = OBJECT_ID(N'dbo.MonitoringAlerts')
    )
    BEGIN
        -- Recreate old index only if SiteId exists
        IF COL_LENGTH(N'dbo.MonitoringAlerts', N'SiteId') IS NOT NULL
        BEGIN
            CREATE INDEX [IX_MonitoringAlerts_OrgId_SiteId] ON [dbo].[MonitoringAlerts]([OrgId], [SiteId]);
        END
    END

    -- Rename column back if needed
    IF COL_LENGTH(N'dbo.MonitoringAlerts', N'MonitoringSiteId') IS NOT NULL
       AND COL_LENGTH(N'dbo.MonitoringAlerts', N'SiteId') IS NULL
    BEGIN
        EXEC sp_rename N'[dbo].[MonitoringAlerts].[MonitoringSiteId]', N'SiteId', N'COLUMN';
    END
END
");

            // Reverse MonitoringSites changes safely
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[dbo].[MonitoringSites]', N'U') IS NOT NULL
BEGIN
    IF EXISTS (
        SELECT 1 FROM sys.foreign_keys
        WHERE name = N'FK_MonitoringSites_Sites_SiteId'
          AND parent_object_id = OBJECT_ID(N'dbo.MonitoringSites')
    )
        ALTER TABLE [dbo].[MonitoringSites] DROP CONSTRAINT [FK_MonitoringSites_Sites_SiteId];

    IF EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE name = N'IX_MonitoringSites_SiteId'
          AND object_id = OBJECT_ID(N'dbo.MonitoringSites')
    )
        DROP INDEX [IX_MonitoringSites_SiteId] ON [dbo].[MonitoringSites];

    IF COL_LENGTH(N'dbo.MonitoringSites', N'SiteId') IS NOT NULL
        ALTER TABLE [dbo].[MonitoringSites] DROP COLUMN [SiteId];

    IF COL_LENGTH(N'dbo.MonitoringSites', N'MonitoringSiteId') IS NOT NULL
       AND COL_LENGTH(N'dbo.MonitoringSites', N'SiteId') IS NULL
    BEGIN
        EXEC sp_rename N'[dbo].[MonitoringSites].[MonitoringSiteId]', N'SiteId', N'COLUMN';
    END
END
");

            migrationBuilder.DropForeignKey(name: "FK_Risks_Sites_SiteId", table: "Risks");
            migrationBuilder.DropIndex(name: "IX_Risks_SiteId", table: "Risks");
            migrationBuilder.DropColumn(name: "SiteId", table: "Risks");

            migrationBuilder.DropTable(name: "Sites");
        }
    }
}
