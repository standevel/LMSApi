using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
#nullable disable

namespace LMS.Api.Data.Migrations
{
    [DbContext(typeof(LmsDbContext))]
    [Migration("20260630120000_AddSystemParentPortalConfiguration")]
    public partial class AddSystemParentPortalConfiguration : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SystemParentPortalConfigurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AutoCreateGuardianAccountsOnStudentCreation = table.Column<bool>(type: "bit", nullable: false),
                    SendGuardianInvitationEmail = table.Column<bool>(type: "bit", nullable: false),
                    DefaultRelationship = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemParentPortalConfigurations", x => x.Id);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SystemParentPortalConfigurations");
        }
    }
}
