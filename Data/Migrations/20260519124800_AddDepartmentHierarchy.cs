using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LMS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDepartmentHierarchy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Programs_Departments_DepartmentId",
                table: "Programs");

            migrationBuilder.DropForeignKey(
                name: "FK_Programs_Faculties_FacultyId",
                table: "Programs");

            migrationBuilder.DropIndex(
                name: "IX_Programs_FacultyId",
                table: "Programs");

            migrationBuilder.DropColumn(
                name: "FacultyId",
                table: "Programs");

            migrationBuilder.AlterColumn<Guid>(
                name: "DepartmentId",
                table: "Programs",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            // Fix orphaned rows: assign programs with zero DepartmentId to the first available department
            migrationBuilder.Sql(@"
                DECLARE @FallbackDeptId UNIQUEIDENTIFIER;
                SELECT TOP(1) @FallbackDeptId = Id FROM Departments;
                IF @FallbackDeptId IS NOT NULL
                BEGIN
                    UPDATE Programs SET DepartmentId = @FallbackDeptId WHERE DepartmentId = '00000000-0000-0000-0000-000000000000';
                END");

            migrationBuilder.AddForeignKey(
                name: "FK_Programs_Departments_DepartmentId",
                table: "Programs",
                column: "DepartmentId",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Programs_Departments_DepartmentId",
                table: "Programs");

            migrationBuilder.AlterColumn<Guid>(
                name: "DepartmentId",
                table: "Programs",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<Guid>(
                name: "FacultyId",
                table: "Programs",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_Programs_FacultyId",
                table: "Programs",
                column: "FacultyId");

            migrationBuilder.AddForeignKey(
                name: "FK_Programs_Departments_DepartmentId",
                table: "Programs",
                column: "DepartmentId",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Programs_Faculties_FacultyId",
                table: "Programs",
                column: "FacultyId",
                principalTable: "Faculties",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
