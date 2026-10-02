using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LMS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMealSessionEnforcement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowPreOrdersOutsideWindows",
                table: "SystemCafeteriaConfigurations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EnforceMealSessionWindows",
                table: "SystemCafeteriaConfigurations",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllowPreOrdersOutsideWindows",
                table: "SystemCafeteriaConfigurations");

            migrationBuilder.DropColumn(
                name: "EnforceMealSessionWindows",
                table: "SystemCafeteriaConfigurations");
        }
    }
}
