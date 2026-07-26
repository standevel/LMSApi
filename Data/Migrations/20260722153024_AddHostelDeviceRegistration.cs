using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LMS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHostelDeviceRegistration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HostelDevices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HostelAllocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeviceType = table.Column<int>(type: "int", nullable: false),
                    Brand = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModelNameNumber = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    SerialNumber = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    MacAddressOrImei = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ColorAndDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ProofOfOwnershipUrl = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RegisteredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    VerifiedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VerifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VerificationNotes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HostelDevices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HostelDevices_HostelAllocations_HostelAllocationId",
                        column: x => x.HostelAllocationId,
                        principalTable: "HostelAllocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_HostelDevices_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HostelDevices_Users_VerifiedByUserId",
                        column: x => x.VerifiedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HostelDevices_HostelAllocationId",
                table: "HostelDevices",
                column: "HostelAllocationId");

            migrationBuilder.CreateIndex(
                name: "IX_HostelDevices_SerialNumber",
                table: "HostelDevices",
                column: "SerialNumber");

            migrationBuilder.CreateIndex(
                name: "IX_HostelDevices_Status",
                table: "HostelDevices",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_HostelDevices_StudentId",
                table: "HostelDevices",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_HostelDevices_VerifiedByUserId",
                table: "HostelDevices",
                column: "VerifiedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HostelDevices");
        }
    }
}
