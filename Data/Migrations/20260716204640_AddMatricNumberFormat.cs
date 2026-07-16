using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LMS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMatricNumberFormat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MatricNumberFormat",
                table: "SystemRegistrationConfigurations",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MatricNumberFormat",
                table: "SystemRegistrationConfigurations");
        }
    }
}
