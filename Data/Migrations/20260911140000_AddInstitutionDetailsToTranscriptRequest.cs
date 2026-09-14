using LMS.Api.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LMS.Api.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(LmsDbContext))]
    [Migration("20260911140000_AddInstitutionDetailsToTranscriptRequest")]
    public partial class AddInstitutionDetailsToTranscriptRequest : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InstitutionName",
                table: "TranscriptRequests",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InstitutionEmail",
                table: "TranscriptRequests",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InstitutionAddress",
                table: "TranscriptRequests",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InstitutionName",
                table: "TranscriptRequests");

            migrationBuilder.DropColumn(
                name: "InstitutionEmail",
                table: "TranscriptRequests");

            migrationBuilder.DropColumn(
                name: "InstitutionAddress",
                table: "TranscriptRequests");
        }
    }
}
