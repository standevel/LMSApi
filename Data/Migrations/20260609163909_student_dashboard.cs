using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LMS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class student_dashboard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Students_AdmissionApplicationId",
                table: "Students");

            migrationBuilder.DropIndex(
                name: "IX_Students_EntraObjectId",
                table: "Students");

            migrationBuilder.AlterColumn<string>(
                name: "EntraObjectId",
                table: "Students",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<Guid>(
                name: "AdmissionApplicationId",
                table: "Students",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.CreateIndex(
                name: "IX_Students_AdmissionApplicationId",
                table: "Students",
                column: "AdmissionApplicationId",
                unique: true,
                filter: "[AdmissionApplicationId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Students_EntraObjectId",
                table: "Students",
                column: "EntraObjectId",
                unique: true,
                filter: "[EntraObjectId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Students_AdmissionApplicationId",
                table: "Students");

            migrationBuilder.DropIndex(
                name: "IX_Students_EntraObjectId",
                table: "Students");

            migrationBuilder.AlterColumn<string>(
                name: "EntraObjectId",
                table: "Students",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "AdmissionApplicationId",
                table: "Students",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Students_AdmissionApplicationId",
                table: "Students",
                column: "AdmissionApplicationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Students_EntraObjectId",
                table: "Students",
                column: "EntraObjectId",
                unique: true);
        }
    }
}
