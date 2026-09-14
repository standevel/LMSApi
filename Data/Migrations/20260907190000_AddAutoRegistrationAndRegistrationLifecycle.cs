using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LMS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAutoRegistrationAndRegistrationLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsRegistrationOpen",
                table: "AcademicSessions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "RegistrationStartDate",
                table: "AcademicSessions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RegistrationEndDate",
                table: "AcademicSessions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRegistrationClosed",
                table: "CourseOfferings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EnableAutoRegistration",
                table: "SystemRegistrationConfigurations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AutoRegisterOnEnrollment",
                table: "SystemRegistrationConfigurations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AutoRegisterOnRegistrationStart",
                table: "SystemRegistrationConfigurations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "AutoRegisterCourseCategories",
                table: "SystemRegistrationConfigurations",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Compulsory");

            migrationBuilder.AddColumn<bool>(
                name: "AutoRegisterCarryovers",
                table: "SystemRegistrationConfigurations",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "AutoRegisterCreditLimitHandling",
                table: "SystemRegistrationConfigurations",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Strict");

            migrationBuilder.AddColumn<string>(
                name: "AutoRegisterTargetLevels",
                table: "SystemRegistrationConfigurations",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "All");

            // Ensure active session has registration open by default so students are not disrupted
            migrationBuilder.Sql("UPDATE [AcademicSessions] SET [IsRegistrationOpen] = 1 WHERE [IsActive] = 1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsRegistrationOpen",
                table: "AcademicSessions");

            migrationBuilder.DropColumn(
                name: "RegistrationStartDate",
                table: "AcademicSessions");

            migrationBuilder.DropColumn(
                name: "RegistrationEndDate",
                table: "AcademicSessions");

            migrationBuilder.DropColumn(
                name: "IsRegistrationClosed",
                table: "CourseOfferings");

            migrationBuilder.DropColumn(
                name: "EnableAutoRegistration",
                table: "SystemRegistrationConfigurations");

            migrationBuilder.DropColumn(
                name: "AutoRegisterOnEnrollment",
                table: "SystemRegistrationConfigurations");

            migrationBuilder.DropColumn(
                name: "AutoRegisterOnRegistrationStart",
                table: "SystemRegistrationConfigurations");

            migrationBuilder.DropColumn(
                name: "AutoRegisterCourseCategories",
                table: "SystemRegistrationConfigurations");

            migrationBuilder.DropColumn(
                name: "AutoRegisterCarryovers",
                table: "SystemRegistrationConfigurations");

            migrationBuilder.DropColumn(
                name: "AutoRegisterCreditLimitHandling",
                table: "SystemRegistrationConfigurations");

            migrationBuilder.DropColumn(
                name: "AutoRegisterTargetLevels",
                table: "SystemRegistrationConfigurations");
        }
    }
}
