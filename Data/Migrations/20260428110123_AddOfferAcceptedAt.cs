using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LMS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOfferAcceptedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "EntraObjectId",
                table: "AdmissionApplications",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OfferAcceptedAt",
                table: "AdmissionApplications",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdmissionApplications_EntraObjectId",
                table: "AdmissionApplications",
                column: "EntraObjectId",
                filter: "[EntraObjectId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AdmissionApplications_Status_OfferAcceptedAt",
                table: "AdmissionApplications",
                columns: new[] { "Status", "OfferAcceptedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AdmissionApplications_EntraObjectId",
                table: "AdmissionApplications");

            migrationBuilder.DropIndex(
                name: "IX_AdmissionApplications_Status_OfferAcceptedAt",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "OfferAcceptedAt",
                table: "AdmissionApplications");

            migrationBuilder.AlterColumn<string>(
                name: "EntraObjectId",
                table: "AdmissionApplications",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);
        }
    }
}
