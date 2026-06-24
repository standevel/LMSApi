using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LMS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProgramSwitchRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProgramSwitchRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromProgramId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ToProgramId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    JambDocumentUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    JambDocumentFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    JambDocumentUploadedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    HoDReviewedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    HoDReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    HoDNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DeanReviewedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeanReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeanNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AdminCompletedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AdminCompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AdminNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RejectedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RejectedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProgramSwitchRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProgramSwitchRequests_Programs_FromProgramId",
                        column: x => x.FromProgramId,
                        principalTable: "Programs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProgramSwitchRequests_Programs_ToProgramId",
                        column: x => x.ToProgramId,
                        principalTable: "Programs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProgramSwitchRequests_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProgramSwitchRequests_Users_AdminCompletedById",
                        column: x => x.AdminCompletedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProgramSwitchRequests_Users_DeanReviewedById",
                        column: x => x.DeanReviewedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProgramSwitchRequests_Users_HoDReviewedById",
                        column: x => x.HoDReviewedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProgramSwitchRequests_Users_RejectedById",
                        column: x => x.RejectedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProgramSwitchRequests_AdminCompletedById",
                table: "ProgramSwitchRequests",
                column: "AdminCompletedById");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramSwitchRequests_DeanReviewedById",
                table: "ProgramSwitchRequests",
                column: "DeanReviewedById");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramSwitchRequests_FromProgramId",
                table: "ProgramSwitchRequests",
                column: "FromProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramSwitchRequests_HoDReviewedById",
                table: "ProgramSwitchRequests",
                column: "HoDReviewedById");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramSwitchRequests_RejectedById",
                table: "ProgramSwitchRequests",
                column: "RejectedById");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramSwitchRequests_Status",
                table: "ProgramSwitchRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramSwitchRequests_StudentId",
                table: "ProgramSwitchRequests",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramSwitchRequests_StudentId_Status",
                table: "ProgramSwitchRequests",
                columns: new[] { "StudentId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ProgramSwitchRequests_ToProgramId",
                table: "ProgramSwitchRequests",
                column: "ToProgramId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProgramSwitchRequests");
        }
    }
}
