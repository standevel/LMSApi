using System;
using LMS.Api.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LMS.Api.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(LmsDbContext))]
    [Migration("20260628170500_AddQuizIpConstraints")]
    public partial class AddQuizIpConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AllowedCbtHallIdsJson",
                table: "QuizSettings",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AllowedIpRangesJson",
                table: "QuizSettings",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RestrictToAllowedIps",
                table: "QuizSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "CbtHalls",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IpRangesJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CbtHalls", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CbtHalls_Code",
                table: "CbtHalls",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CbtHalls_IsActive",
                table: "CbtHalls",
                column: "IsActive");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CbtHalls");

            migrationBuilder.DropColumn(
                name: "AllowedCbtHallIdsJson",
                table: "QuizSettings");

            migrationBuilder.DropColumn(
                name: "AllowedIpRangesJson",
                table: "QuizSettings");

            migrationBuilder.DropColumn(
                name: "RestrictToAllowedIps",
                table: "QuizSettings");
        }
    }
}
