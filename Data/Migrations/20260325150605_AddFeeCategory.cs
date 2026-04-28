using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LMS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFeeCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Category",
                table: "FeeTemplates");

            migrationBuilder.AddColumn<Guid>(
                name: "FeeCategoryId",
                table: "FeeTemplates",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "FeeCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeeCategories", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FeeTemplates_FeeCategoryId",
                table: "FeeTemplates",
                column: "FeeCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_FeeCategories_Name",
                table: "FeeCategories",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_FeeTemplates_FeeCategories_FeeCategoryId",
                table: "FeeTemplates",
                column: "FeeCategoryId",
                principalTable: "FeeCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FeeTemplates_FeeCategories_FeeCategoryId",
                table: "FeeTemplates");

            migrationBuilder.DropTable(
                name: "FeeCategories");

            migrationBuilder.DropIndex(
                name: "IX_FeeTemplates_FeeCategoryId",
                table: "FeeTemplates");

            migrationBuilder.DropColumn(
                name: "FeeCategoryId",
                table: "FeeTemplates");

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "FeeTemplates",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");
        }
    }
}
