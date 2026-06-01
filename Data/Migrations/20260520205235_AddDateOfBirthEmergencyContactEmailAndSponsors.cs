using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LMS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDateOfBirthEmergencyContactEmailAndSponsors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "SponsorOrganizations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Phone",
                table: "SponsorOrganizations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateOfBirth",
                table: "AdmissionApplications",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmergencyContactEmail",
                table: "AdmissionApplications",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmergencyContactName",
                table: "AdmissionApplications",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmergencyContactPhone",
                table: "AdmissionApplications",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Email",
                table: "SponsorOrganizations");

            migrationBuilder.DropColumn(
                name: "Phone",
                table: "SponsorOrganizations");

            migrationBuilder.DropColumn(
                name: "DateOfBirth",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "EmergencyContactEmail",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "EmergencyContactName",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "EmergencyContactPhone",
                table: "AdmissionApplications");
        }
    }
}
