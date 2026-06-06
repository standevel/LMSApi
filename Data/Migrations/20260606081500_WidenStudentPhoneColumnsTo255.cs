using LMS.Api.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LMS.Api.Data.Migrations
{
    [DbContext(typeof(LmsDbContext))]
    [Migration("20260606081500_WidenStudentPhoneColumnsTo255")]
    public partial class WidenStudentPhoneColumnsTo255 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH('Students', 'Phone') IS NOT NULL
                BEGIN
                    ALTER TABLE [Students] ALTER COLUMN [Phone] nvarchar(255) NOT NULL;
                END
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH('Students', 'EmergencyContactPhone') IS NOT NULL
                BEGIN
                    ALTER TABLE [Students] ALTER COLUMN [EmergencyContactPhone] nvarchar(255) NULL;
                END
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH('Students', 'EmergencyContactPhone') IS NOT NULL
                BEGIN
                    ALTER TABLE [Students] ALTER COLUMN [EmergencyContactPhone] nvarchar(50) NULL;
                END
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH('Students', 'Phone') IS NOT NULL
                BEGIN
                    ALTER TABLE [Students] ALTER COLUMN [Phone] nvarchar(50) NOT NULL;
                END
                """);
        }
    }
}
