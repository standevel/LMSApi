using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LMS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMenuItemAllergensAndDietary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Allergens",
                table: "CafeteriaMenuItems",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Calories",
                table: "CafeteriaMenuItems",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DietaryFlags",
                table: "CafeteriaMenuItems",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Allergens",
                table: "CafeteriaMenuItems");

            migrationBuilder.DropColumn(
                name: "Calories",
                table: "CafeteriaMenuItems");

            migrationBuilder.DropColumn(
                name: "DietaryFlags",
                table: "CafeteriaMenuItems");
        }
    }
}
