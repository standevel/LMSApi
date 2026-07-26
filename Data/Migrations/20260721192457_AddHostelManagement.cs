using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LMS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHostelManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Gender",
                table: "Students",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "HostelBlocks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    GenderType = table.Column<int>(type: "int", nullable: false),
                    CampusLocation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TotalFloors = table.Column<int>(type: "int", nullable: false),
                    WardenName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    WardenPhone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    WardenEmail = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HostelBlocks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HostelRooms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HostelBlockId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoomNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FloorLevel = table.Column<int>(type: "int", nullable: false),
                    RoomType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Capacity = table.Column<int>(type: "int", nullable: false),
                    SemesterFeeRate = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AmenitiesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HostelRooms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HostelRooms_HostelBlocks_HostelBlockId",
                        column: x => x.HostelBlockId,
                        principalTable: "HostelBlocks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HostelBeds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HostelRoomId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BedLabel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CurrentStudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HostelBeds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HostelBeds_HostelRooms_HostelRoomId",
                        column: x => x.HostelRoomId,
                        principalTable: "HostelRooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HostelBeds_Students_CurrentStudentId",
                        column: x => x.CurrentStudentId,
                        principalTable: "Students",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "HostelMaintenanceRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HostelBlockId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HostelRoomId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReportedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AssignedTo = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    ResolutionNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReportedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HostelMaintenanceRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HostelMaintenanceRequests_HostelBlocks_HostelBlockId",
                        column: x => x.HostelBlockId,
                        principalTable: "HostelBlocks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HostelMaintenanceRequests_HostelRooms_HostelRoomId",
                        column: x => x.HostelRoomId,
                        principalTable: "HostelRooms",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_HostelMaintenanceRequests_Users_ReportedByUserId",
                        column: x => x.ReportedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HostelAllocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcademicSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HostelBedId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PreferredBlockId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PreferredRoomType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SpecialNeeds = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ApplicationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AllocatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CheckedInAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CheckedOutAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FeeRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HostelAllocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HostelAllocations_AcademicSessions_AcademicSessionId",
                        column: x => x.AcademicSessionId,
                        principalTable: "AcademicSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HostelAllocations_HostelBeds_HostelBedId",
                        column: x => x.HostelBedId,
                        principalTable: "HostelBeds",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_HostelAllocations_HostelBlocks_PreferredBlockId",
                        column: x => x.PreferredBlockId,
                        principalTable: "HostelBlocks",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_HostelAllocations_StudentFeeRecords_FeeRecordId",
                        column: x => x.FeeRecordId,
                        principalTable: "StudentFeeRecords",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_HostelAllocations_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HostelExeats",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HostelAllocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DepartureTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpectedReturnTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ActualReturnTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Destination = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ApprovedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WardenRemarks = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ParentApproved = table.Column<bool>(type: "bit", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DecidedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HostelExeats", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HostelExeats_HostelAllocations_HostelAllocationId",
                        column: x => x.HostelAllocationId,
                        principalTable: "HostelAllocations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_HostelExeats_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HostelExeats_Users_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_HostelAllocations_AcademicSessionId",
                table: "HostelAllocations",
                column: "AcademicSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_HostelAllocations_FeeRecordId",
                table: "HostelAllocations",
                column: "FeeRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_HostelAllocations_HostelBedId",
                table: "HostelAllocations",
                column: "HostelBedId");

            migrationBuilder.CreateIndex(
                name: "IX_HostelAllocations_PreferredBlockId",
                table: "HostelAllocations",
                column: "PreferredBlockId");

            migrationBuilder.CreateIndex(
                name: "IX_HostelAllocations_StudentId",
                table: "HostelAllocations",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_HostelBeds_CurrentStudentId",
                table: "HostelBeds",
                column: "CurrentStudentId");

            migrationBuilder.CreateIndex(
                name: "IX_HostelBeds_HostelRoomId",
                table: "HostelBeds",
                column: "HostelRoomId");

            migrationBuilder.CreateIndex(
                name: "IX_HostelExeats_ApprovedByUserId",
                table: "HostelExeats",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_HostelExeats_HostelAllocationId",
                table: "HostelExeats",
                column: "HostelAllocationId");

            migrationBuilder.CreateIndex(
                name: "IX_HostelExeats_StudentId",
                table: "HostelExeats",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_HostelMaintenanceRequests_HostelBlockId",
                table: "HostelMaintenanceRequests",
                column: "HostelBlockId");

            migrationBuilder.CreateIndex(
                name: "IX_HostelMaintenanceRequests_HostelRoomId",
                table: "HostelMaintenanceRequests",
                column: "HostelRoomId");

            migrationBuilder.CreateIndex(
                name: "IX_HostelMaintenanceRequests_ReportedByUserId",
                table: "HostelMaintenanceRequests",
                column: "ReportedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_HostelRooms_HostelBlockId",
                table: "HostelRooms",
                column: "HostelBlockId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HostelExeats");

            migrationBuilder.DropTable(
                name: "HostelMaintenanceRequests");

            migrationBuilder.DropTable(
                name: "HostelAllocations");

            migrationBuilder.DropTable(
                name: "HostelBeds");

            migrationBuilder.DropTable(
                name: "HostelRooms");

            migrationBuilder.DropTable(
                name: "HostelBlocks");

            migrationBuilder.DropColumn(
                name: "Gender",
                table: "Students");
        }
    }
}
