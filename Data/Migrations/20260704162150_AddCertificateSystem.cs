using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LMS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCertificateSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CertificateRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CertificateType = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DeliveryMethod = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DeliveryEmail = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    FeeAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    FeePaid = table.Column<bool>(type: "bit", nullable: false),
                    DocumentUrl = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CredentialId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProcessedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CertificateRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CertificateRequests_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CertificateRequests_Users_ProcessedBy",
                        column: x => x.ProcessedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CertificateRequests_Users_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SystemCertificateConfigurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChargeForCertificates = table.Column<bool>(type: "bit", nullable: false),
                    OfficialCertificateFee = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SignatoryName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SignatoryPosition = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SignatorySignatureBase64 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RegistrarName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RegistrarPosition = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RegistrarSignatureBase64 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemCertificateConfigurations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CertificateRequests_CreatedAt",
                table: "CertificateRequests",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CertificateRequests_CreatedById",
                table: "CertificateRequests",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_CertificateRequests_CredentialId",
                table: "CertificateRequests",
                column: "CredentialId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CertificateRequests_ProcessedBy",
                table: "CertificateRequests",
                column: "ProcessedBy");

            migrationBuilder.CreateIndex(
                name: "IX_CertificateRequests_Status",
                table: "CertificateRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_CertificateRequests_StudentId",
                table: "CertificateRequests",
                column: "StudentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CertificateRequests");

            migrationBuilder.DropTable(
                name: "SystemCertificateConfigurations");
        }
    }
}
