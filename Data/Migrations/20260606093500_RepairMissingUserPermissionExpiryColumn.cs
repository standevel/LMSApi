using LMS.Api.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LMS.Api.Data.Migrations
{
    [DbContext(typeof(LmsDbContext))]
    [Migration("20260606093500_RepairMissingUserPermissionExpiryColumn")]
    public partial class RepairMissingUserPermissionExpiryColumn : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH('UserPermissions', 'ExpiresUtc') IS NULL
                BEGIN
                    ALTER TABLE [UserPermissions]
                    ADD [ExpiresUtc] datetime2 NULL;
                END
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH('UserPermissions', 'ExpiresUtc') IS NOT NULL
                   AND NOT EXISTS (
                       SELECT 1
                       FROM sys.indexes
                       WHERE [name] = 'IX_UserPermissions_ExpiresUtc'
                         AND [object_id] = OBJECT_ID('UserPermissions')
                   )
                BEGIN
                    CREATE INDEX [IX_UserPermissions_ExpiresUtc]
                    ON [UserPermissions] ([ExpiresUtc]);
                END
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE [name] = 'IX_UserPermissions_ExpiresUtc'
                      AND [object_id] = OBJECT_ID('UserPermissions')
                )
                BEGIN
                    DROP INDEX [IX_UserPermissions_ExpiresUtc]
                    ON [UserPermissions];
                END
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH('UserPermissions', 'ExpiresUtc') IS NOT NULL
                BEGIN
                    ALTER TABLE [UserPermissions] DROP COLUMN [ExpiresUtc];
                END
                """);
        }
    }
}
