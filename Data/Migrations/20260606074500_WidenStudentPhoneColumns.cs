using LMS.Api.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LMS.Api.Data.Migrations
{
    [DbContext(typeof(LmsDbContext))]
    [Migration("20260606074500_WidenStudentPhoneColumns")]
    public partial class WidenStudentPhoneColumns : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH('Students', 'Phone') IS NOT NULL
                   AND COL_LENGTH('Students', 'Phone') < 100
                BEGIN
                    ALTER TABLE [Students] ALTER COLUMN [Phone] nvarchar(50) NOT NULL;
                END
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH('Students', 'EmergencyContactPhone') IS NOT NULL
                   AND COL_LENGTH('Students', 'EmergencyContactPhone') < 100
                BEGIN
                    ALTER TABLE [Students] ALTER COLUMN [EmergencyContactPhone] nvarchar(50) NULL;
                END
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH('Students', 'EmergencyContactPhone') IS NOT NULL
                   AND COL_LENGTH('Students', 'EmergencyContactPhone') > 40
                BEGIN
                    ALTER TABLE [Students] ALTER COLUMN [EmergencyContactPhone] nvarchar(20) NULL;
                END
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH('Students', 'Phone') IS NOT NULL
                   AND COL_LENGTH('Students', 'Phone') > 40
                BEGIN
                    ALTER TABLE [Students] ALTER COLUMN [Phone] nvarchar(20) NOT NULL;
                END
                """);
        }
    }
}
