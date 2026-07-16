using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LMS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProgramSpecialization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ParentProgramId",
                table: "Programs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SpecializationStartYear",
                table: "Programs",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MajorDeclarationRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParentProgramId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeclaredProgramId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ApprovedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MajorDeclarationRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MajorDeclarationRequests_Programs_DeclaredProgramId",
                        column: x => x.DeclaredProgramId,
                        principalTable: "Programs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MajorDeclarationRequests_Programs_ParentProgramId",
                        column: x => x.ParentProgramId,
                        principalTable: "Programs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MajorDeclarationRequests_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MajorDeclarationRequests_Users_ApprovedById",
                        column: x => x.ApprovedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Programs_ParentProgramId",
                table: "Programs",
                column: "ParentProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_MajorDeclarationRequests_ApprovedById",
                table: "MajorDeclarationRequests",
                column: "ApprovedById");

            migrationBuilder.CreateIndex(
                name: "IX_MajorDeclarationRequests_DeclaredProgramId",
                table: "MajorDeclarationRequests",
                column: "DeclaredProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_MajorDeclarationRequests_ParentProgramId",
                table: "MajorDeclarationRequests",
                column: "ParentProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_MajorDeclarationRequests_Status",
                table: "MajorDeclarationRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_MajorDeclarationRequests_StudentId",
                table: "MajorDeclarationRequests",
                column: "StudentId");

            migrationBuilder.AddForeignKey(
                name: "FK_Programs_Programs_ParentProgramId",
                table: "Programs",
                column: "ParentProgramId",
                principalTable: "Programs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Programs_Programs_ParentProgramId",
                table: "Programs");

            migrationBuilder.DropTable(
                name: "MajorDeclarationRequests");

            migrationBuilder.DropIndex(
                name: "IX_Programs_ParentProgramId",
                table: "Programs");

            migrationBuilder.DropColumn(
                name: "ParentProgramId",
                table: "Programs");

            migrationBuilder.DropColumn(
                name: "SpecializationStartYear",
                table: "Programs");
        }
    }
}
