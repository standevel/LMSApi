using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LMS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class CafeteriaHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProfileCompletionRequirements_AcademicSessions_AcademicSessionId",
                table: "ProfileCompletionRequirements");

            migrationBuilder.DropForeignKey(
                name: "FK_ProfileCompletionRequirements_Faculties_FacultyId",
                table: "ProfileCompletionRequirements");

            migrationBuilder.DropForeignKey(
                name: "FK_ProfileCompletionRequirements_Levels_AcademicLevelId",
                table: "ProfileCompletionRequirements");

            migrationBuilder.DropForeignKey(
                name: "FK_ProfileCompletionRequirements_Programs_AcademicProgramId",
                table: "ProfileCompletionRequirements");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyToken",
                table: "CafeteriaWalletAccounts",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiresAt",
                table: "CafeteriaVendorOrders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ProfileCompletionRequirements_AcademicSessions_AcademicSessionId",
                table: "ProfileCompletionRequirements",
                column: "AcademicSessionId",
                principalTable: "AcademicSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProfileCompletionRequirements_Faculties_FacultyId",
                table: "ProfileCompletionRequirements",
                column: "FacultyId",
                principalTable: "Faculties",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProfileCompletionRequirements_Levels_AcademicLevelId",
                table: "ProfileCompletionRequirements",
                column: "AcademicLevelId",
                principalTable: "Levels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProfileCompletionRequirements_Programs_AcademicProgramId",
                table: "ProfileCompletionRequirements",
                column: "AcademicProgramId",
                principalTable: "Programs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProfileCompletionRequirements_AcademicSessions_AcademicSessionId",
                table: "ProfileCompletionRequirements");

            migrationBuilder.DropForeignKey(
                name: "FK_ProfileCompletionRequirements_Faculties_FacultyId",
                table: "ProfileCompletionRequirements");

            migrationBuilder.DropForeignKey(
                name: "FK_ProfileCompletionRequirements_Levels_AcademicLevelId",
                table: "ProfileCompletionRequirements");

            migrationBuilder.DropForeignKey(
                name: "FK_ProfileCompletionRequirements_Programs_AcademicProgramId",
                table: "ProfileCompletionRequirements");

            migrationBuilder.DropColumn(
                name: "ConcurrencyToken",
                table: "CafeteriaWalletAccounts");

            migrationBuilder.DropColumn(
                name: "ExpiresAt",
                table: "CafeteriaVendorOrders");

            migrationBuilder.AddForeignKey(
                name: "FK_ProfileCompletionRequirements_AcademicSessions_AcademicSessionId",
                table: "ProfileCompletionRequirements",
                column: "AcademicSessionId",
                principalTable: "AcademicSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ProfileCompletionRequirements_Faculties_FacultyId",
                table: "ProfileCompletionRequirements",
                column: "FacultyId",
                principalTable: "Faculties",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ProfileCompletionRequirements_Levels_AcademicLevelId",
                table: "ProfileCompletionRequirements",
                column: "AcademicLevelId",
                principalTable: "Levels",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ProfileCompletionRequirements_Programs_AcademicProgramId",
                table: "ProfileCompletionRequirements",
                column: "AcademicProgramId",
                principalTable: "Programs",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
