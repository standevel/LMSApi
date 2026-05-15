using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LMS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTransferInternationalSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "DirectEntryOnly",
                table: "DocumentTypes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "InternationalOnly",
                table: "DocumentTypes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "NigeriaOnly",
                table: "DocumentTypes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "TransferOnly",
                table: "DocumentTypes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ApplicantType",
                table: "AdmissionApplications",
                type: "int",
                nullable: false,
                defaultValue: 1); // UTME = 1

            migrationBuilder.AddColumn<int>(
                name: "CreditsEarned",
                table: "AdmissionApplications",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EnglishProficiencyScore",
                table: "AdmissionApplications",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EnglishProficiencyType",
                table: "AdmissionApplications",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Nationality",
                table: "AdmissionApplications",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PassportNumber",
                table: "AdmissionApplications",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PreviousCGPA",
                table: "AdmissionApplications",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreviousInstitutionCountry",
                table: "AdmissionApplications",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreviousInstitutionName",
                table: "AdmissionApplications",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "StartingLevelId",
                table: "AdmissionApplications",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdmissionApplications_StartingLevelId",
                table: "AdmissionApplications",
                column: "StartingLevelId");

            migrationBuilder.AddForeignKey(
                name: "FK_AdmissionApplications_Levels_StartingLevelId",
                table: "AdmissionApplications",
                column: "StartingLevelId",
                principalTable: "Levels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AdmissionApplications_Levels_StartingLevelId",
                table: "AdmissionApplications");

            migrationBuilder.DropIndex(
                name: "IX_AdmissionApplications_StartingLevelId",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "DirectEntryOnly",
                table: "DocumentTypes");

            migrationBuilder.DropColumn(
                name: "InternationalOnly",
                table: "DocumentTypes");

            migrationBuilder.DropColumn(
                name: "NigeriaOnly",
                table: "DocumentTypes");

            migrationBuilder.DropColumn(
                name: "TransferOnly",
                table: "DocumentTypes");

            migrationBuilder.DropColumn(
                name: "ApplicantType",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "CreditsEarned",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "EnglishProficiencyScore",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "EnglishProficiencyType",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "Nationality",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "PassportNumber",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "PreviousCGPA",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "PreviousInstitutionCountry",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "PreviousInstitutionName",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "StartingLevelId",
                table: "AdmissionApplications");
        }
    }
}
