using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LMS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAdmissionEnhancements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ExchangeOnly",
                table: "DocumentTypes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "FinancialProofAmount",
                table: "AdmissionApplications",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FinancialProofCurrency",
                table: "AdmissionApplications",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "FinancialProofProvided",
                table: "AdmissionApplications",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsExchangeProgram",
                table: "AdmissionApplications",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "VisaApplicationNumber",
                table: "AdmissionApplications",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "VisaRequired",
                table: "AdmissionApplications",
                type: "bit",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExchangeOnly",
                table: "DocumentTypes");

            migrationBuilder.DropColumn(
                name: "FinancialProofAmount",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "FinancialProofCurrency",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "FinancialProofProvided",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "IsExchangeProgram",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "VisaApplicationNumber",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "VisaRequired",
                table: "AdmissionApplications");
        }
    }
}
