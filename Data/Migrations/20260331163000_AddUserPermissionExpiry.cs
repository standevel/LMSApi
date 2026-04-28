using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LMS.Api.Data.Migrations
{
    public partial class AddUserPermissionExpiry : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiresUtc",
                table: "UserPermissions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserPermissions_ExpiresUtc",
                table: "UserPermissions",
                column: "ExpiresUtc");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserPermissions_ExpiresUtc",
                table: "UserPermissions");

            migrationBuilder.DropColumn(
                name: "ExpiresUtc",
                table: "UserPermissions");
        }
    }
}
