using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LMS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ParentAccessPolicyAndConsent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AllowedCategoriesJson",
                table: "SystemParentPortalConfigurations",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "RequireStudentConsentForSensitive",
                table: "SystemParentPortalConfigurations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "TreatAdultStudentsAsRestricted",
                table: "SystemParentPortalConfigurations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ParentStudentConsents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    IsAllowed = table.Column<bool>(type: "bit", nullable: false),
                    SetAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SetById = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParentStudentConsents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ParentStudentConsents_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ParentStudentConsents_StudentId_Category",
                table: "ParentStudentConsents",
                columns: new[] { "StudentId", "Category" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ParentStudentConsents");

            migrationBuilder.DropColumn(
                name: "AllowedCategoriesJson",
                table: "SystemParentPortalConfigurations");

            migrationBuilder.DropColumn(
                name: "RequireStudentConsentForSensitive",
                table: "SystemParentPortalConfigurations");

            migrationBuilder.DropColumn(
                name: "TreatAdultStudentsAsRestricted",
                table: "SystemParentPortalConfigurations");
        }
    }
}
