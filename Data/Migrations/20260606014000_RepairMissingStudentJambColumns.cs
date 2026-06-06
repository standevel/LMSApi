using LMS.Api.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LMS.Api.Data.Migrations
{
    [DbContext(typeof(LmsDbContext))]
    [Migration("20260606014000_RepairMissingStudentJambColumns")]
    public partial class RepairMissingStudentJambColumns : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH('Students', 'JambRegistrationNumber') IS NULL
                BEGIN
                    ALTER TABLE [Students]
                    ADD [JambRegistrationNumber] nvarchar(50) NULL;
                END
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH('Students', 'JambScore') IS NULL
                BEGIN
                    ALTER TABLE [Students]
                    ADD [JambScore] int NULL;
                END
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH('Students', 'JambScore') IS NOT NULL
                BEGIN
                    ALTER TABLE [Students] DROP COLUMN [JambScore];
                END
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH('Students', 'JambRegistrationNumber') IS NOT NULL
                BEGIN
                    ALTER TABLE [Students] DROP COLUMN [JambRegistrationNumber];
                END
                """);
        }
    }
}
