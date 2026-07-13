using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LMS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGradingRoundingAndGraceRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "GraceThreshold",
                table: "SystemGradingConfigurations",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0.00m);

            migrationBuilder.AddColumn<int>(
                name: "RoundingDecimalPlaces",
                table: "SystemGradingConfigurations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "RoundingStrategy",
                table: "SystemGradingConfigurations",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "Standard");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GraceThreshold",
                table: "SystemGradingConfigurations");

            migrationBuilder.DropColumn(
                name: "RoundingDecimalPlaces",
                table: "SystemGradingConfigurations");

            migrationBuilder.DropColumn(
                name: "RoundingStrategy",
                table: "SystemGradingConfigurations");
        }
    }
}
