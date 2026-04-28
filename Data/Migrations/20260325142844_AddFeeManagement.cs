using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LMS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFeeManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FeeTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Scope = table.Column<int>(type: "int", nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FacultyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProgramId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LateFeeType = table.Column<int>(type: "int", nullable: false),
                    LateFeeAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeeTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FeeTemplates_AcademicSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "AcademicSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FeeTemplates_Faculties_FacultyId",
                        column: x => x.FacultyId,
                        principalTable: "Faculties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FeeTemplates_Programs_ProgramId",
                        column: x => x.ProgramId,
                        principalTable: "Programs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StudentFeeRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AmountPaid = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LateFeeApplied = table.Column<bool>(type: "bit", nullable: false),
                    LateFeeTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentFeeRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentFeeRecords_AcademicSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "AcademicSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentFeeRecords_Users_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FeeAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FeeTemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Scope = table.Column<int>(type: "int", nullable: false),
                    FacultyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProgramId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AmountOverride = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    DueDateOverride = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeeAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FeeAssignments_AcademicSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "AcademicSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FeeAssignments_Faculties_FacultyId",
                        column: x => x.FacultyId,
                        principalTable: "Faculties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FeeAssignments_FeeTemplates_FeeTemplateId",
                        column: x => x.FeeTemplateId,
                        principalTable: "FeeTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FeeAssignments_Programs_ProgramId",
                        column: x => x.ProgramId,
                        principalTable: "Programs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FeeAssignments_Users_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FeeLineItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FeeTemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsOptional = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeeLineItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FeeLineItems_FeeTemplates_FeeTemplateId",
                        column: x => x.FeeTemplateId,
                        principalTable: "FeeTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FeePayments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentFeeRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PaymentMethod = table.Column<int>(type: "int", nullable: false),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReceiptUrl = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    GatewayReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    GatewayCheckoutUrl = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RejectionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PaidAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConfirmedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConfirmedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeePayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FeePayments_StudentFeeRecords_StudentFeeRecordId",
                        column: x => x.StudentFeeRecordId,
                        principalTable: "StudentFeeRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LateFeeApplications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentFeeRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FeeTemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AmountCharged = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    FeeType = table.Column<int>(type: "int", nullable: false),
                    BaseRateUsed = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    EffectiveDueDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AppliedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AppliedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LateFeeApplications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LateFeeApplications_FeeTemplates_FeeTemplateId",
                        column: x => x.FeeTemplateId,
                        principalTable: "FeeTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LateFeeApplications_StudentFeeRecords_StudentFeeRecordId",
                        column: x => x.StudentFeeRecordId,
                        principalTable: "StudentFeeRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FeeAssignments_FacultyId",
                table: "FeeAssignments",
                column: "FacultyId");

            migrationBuilder.CreateIndex(
                name: "IX_FeeAssignments_FeeTemplateId",
                table: "FeeAssignments",
                column: "FeeTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_FeeAssignments_ProgramId",
                table: "FeeAssignments",
                column: "ProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_FeeAssignments_SessionId",
                table: "FeeAssignments",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_FeeAssignments_StudentId",
                table: "FeeAssignments",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_FeeLineItems_FeeTemplateId",
                table: "FeeLineItems",
                column: "FeeTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_FeePayments_GatewayReference",
                table: "FeePayments",
                column: "GatewayReference");

            migrationBuilder.CreateIndex(
                name: "IX_FeePayments_Status",
                table: "FeePayments",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_FeePayments_StudentFeeRecordId",
                table: "FeePayments",
                column: "StudentFeeRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_FeeTemplates_FacultyId",
                table: "FeeTemplates",
                column: "FacultyId");

            migrationBuilder.CreateIndex(
                name: "IX_FeeTemplates_ProgramId",
                table: "FeeTemplates",
                column: "ProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_FeeTemplates_SessionId",
                table: "FeeTemplates",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_LateFeeApplications_FeeTemplateId",
                table: "LateFeeApplications",
                column: "FeeTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_LateFeeApplications_StudentFeeRecordId_FeeTemplateId",
                table: "LateFeeApplications",
                columns: new[] { "StudentFeeRecordId", "FeeTemplateId" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentFeeRecords_SessionId",
                table: "StudentFeeRecords",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentFeeRecords_Status",
                table: "StudentFeeRecords",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_StudentFeeRecords_StudentId_SessionId",
                table: "StudentFeeRecords",
                columns: new[] { "StudentId", "SessionId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FeeAssignments");

            migrationBuilder.DropTable(
                name: "FeeLineItems");

            migrationBuilder.DropTable(
                name: "FeePayments");

            migrationBuilder.DropTable(
                name: "LateFeeApplications");

            migrationBuilder.DropTable(
                name: "FeeTemplates");

            migrationBuilder.DropTable(
                name: "StudentFeeRecords");
        }
    }
}
