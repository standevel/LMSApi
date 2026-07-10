using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LMS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class SharedOfferingsRefactor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Step 1: Migrate ProgramId+LevelId into CourseOfferingPrograms ──────────
            // Create the join table first (while old columns still exist)
            migrationBuilder.CreateTable(
                name: "CourseOfferingPrograms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CourseOfferingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProgramId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LevelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseOfferingPrograms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CourseOfferingPrograms_CourseOfferings_CourseOfferingId",
                        column: x => x.CourseOfferingId,
                        principalTable: "CourseOfferings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CourseOfferingPrograms_Levels_LevelId",
                        column: x => x.LevelId,
                        principalTable: "Levels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CourseOfferingPrograms_Programs_ProgramId",
                        column: x => x.ProgramId,
                        principalTable: "Programs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CourseOfferingLecturers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CourseOfferingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LecturerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Role = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseOfferingLecturers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CourseOfferingLecturers_CourseOfferings_CourseOfferingId",
                        column: x => x.CourseOfferingId,
                        principalTable: "CourseOfferings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CourseOfferingLecturers_Users_LecturerId",
                        column: x => x.LecturerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // ── Step 2: Migrate existing flat data into join tables ───────────────────
            // Each old row had (CourseId, ProgramId, LevelId, LecturerId, Semester, SessionId).
            // Migrate ProgramId+LevelId into CourseOfferingPrograms (one row per old offering).
            migrationBuilder.Sql(@"
                INSERT INTO CourseOfferingPrograms (Id, CourseOfferingId, ProgramId, LevelId)
                SELECT NEWID(), Id, ProgramId, LevelId
                FROM CourseOfferings
                WHERE ProgramId != '00000000-0000-0000-0000-000000000000'
                  AND LevelId  != '00000000-0000-0000-0000-000000000000';
            ");

            // Migrate LecturerId into CourseOfferingLecturers as Main (Role = 1)
            migrationBuilder.Sql(@"
                INSERT INTO CourseOfferingLecturers (Id, CourseOfferingId, LecturerId, Role)
                SELECT NEWID(), Id, LecturerId, 1
                FROM CourseOfferings
                WHERE LecturerId IS NOT NULL;
            ");

            // ── Step 3: Deduplicate CourseOfferings ──────────────────────────────────
            // Keep the earliest row per (CourseId, AcademicSessionId, Semester).
            // Re-point CourseOfferingPrograms/Lecturers to the survivor, then delete dupes.
            migrationBuilder.Sql(@"
                -- Find the survivor (earliest Id) per unique key
                WITH Survivors AS (
                    SELECT MIN(Id) AS SurvivorId, CourseId, AcademicSessionId, Semester
                    FROM CourseOfferings
                    GROUP BY CourseId, AcademicSessionId, Semester
                ),
                Duplicates AS (
                    SELECT co.Id AS DupId, s.SurvivorId
                    FROM CourseOfferings co
                    JOIN Survivors s
                        ON co.CourseId = s.CourseId
                       AND co.AcademicSessionId = s.AcademicSessionId
                       AND co.Semester = s.Semester
                    WHERE co.Id <> s.SurvivorId
                )
                -- Re-point join rows to survivor
                UPDATE CourseOfferingPrograms
                SET CourseOfferingId = d.SurvivorId
                FROM CourseOfferingPrograms cop
                JOIN Duplicates d ON cop.CourseOfferingId = d.DupId;
            ");

            migrationBuilder.Sql(@"
                WITH Survivors AS (
                    SELECT MIN(Id) AS SurvivorId, CourseId, AcademicSessionId, Semester
                    FROM CourseOfferings
                    GROUP BY CourseId, AcademicSessionId, Semester
                ),
                Duplicates AS (
                    SELECT co.Id AS DupId, s.SurvivorId
                    FROM CourseOfferings co
                    JOIN Survivors s
                        ON co.CourseId = s.CourseId
                       AND co.AcademicSessionId = s.AcademicSessionId
                       AND co.Semester = s.Semester
                    WHERE co.Id <> s.SurvivorId
                )
                UPDATE CourseOfferingLecturers
                SET CourseOfferingId = d.SurvivorId
                FROM CourseOfferingLecturers col
                JOIN Duplicates d ON col.CourseOfferingId = d.DupId;
            ");

            // Remove duplicate CourseOfferingPrograms rows (same offering+program+level)
            migrationBuilder.Sql(@"
                WITH Ranked AS (
                    SELECT Id,
                           ROW_NUMBER() OVER (
                               PARTITION BY CourseOfferingId, ProgramId, LevelId
                               ORDER BY Id
                           ) AS rn
                    FROM CourseOfferingPrograms
                )
                DELETE FROM CourseOfferingPrograms WHERE Id IN (
                    SELECT Id FROM Ranked WHERE rn > 1
                );
            ");

            // Remove duplicate CourseOfferingLecturers rows (same offering+lecturer)
            migrationBuilder.Sql(@"
                WITH Ranked AS (
                    SELECT Id,
                           ROW_NUMBER() OVER (
                               PARTITION BY CourseOfferingId, LecturerId
                               ORDER BY Id
                           ) AS rn
                    FROM CourseOfferingLecturers
                )
                DELETE FROM CourseOfferingLecturers WHERE Id IN (
                    SELECT Id FROM Ranked WHERE rn > 1
                );
            ");

            // Delete other tables that reference duplicate offerings
            migrationBuilder.Sql(@"
                WITH Survivors AS (
                    SELECT MIN(Id) AS SurvivorId, CourseId, AcademicSessionId, Semester
                    FROM CourseOfferings
                    GROUP BY CourseId, AcademicSessionId, Semester
                )
                DELETE FROM CourseOfferings
                WHERE Id NOT IN (SELECT SurvivorId FROM Survivors);
            ");

            // ── Step 4: Drop old FK constraints and indexes ──────────────────────────
            migrationBuilder.DropForeignKey(
                name: "FK_CourseOfferings_Levels_LevelId",
                table: "CourseOfferings");

            migrationBuilder.DropForeignKey(
                name: "FK_CourseOfferings_Programs_ProgramId",
                table: "CourseOfferings");

            migrationBuilder.DropForeignKey(
                name: "FK_CourseOfferings_Users_LecturerId",
                table: "CourseOfferings");

            migrationBuilder.DropIndex(
                name: "IX_CourseOfferings_CourseId_ProgramId_LevelId_AcademicSessionId_Semester",
                table: "CourseOfferings");

            migrationBuilder.DropIndex(
                name: "IX_CourseOfferings_LecturerId",
                table: "CourseOfferings");

            migrationBuilder.DropIndex(
                name: "IX_CourseOfferings_LevelId",
                table: "CourseOfferings");

            migrationBuilder.DropIndex(
                name: "IX_CourseOfferings_ProgramId",
                table: "CourseOfferings");

            // ── Step 5: Drop old flat columns ────────────────────────────────────────
            migrationBuilder.DropColumn(
                name: "CoLecturersJson",
                table: "CourseOfferings");

            migrationBuilder.DropColumn(
                name: "LecturerId",
                table: "CourseOfferings");

            migrationBuilder.DropColumn(
                name: "LevelId",
                table: "CourseOfferings");

            migrationBuilder.DropColumn(
                name: "ProgramId",
                table: "CourseOfferings");

            // ── Step 6: Add new unique index on CourseOfferings ──────────────────────
            migrationBuilder.CreateIndex(
                name: "IX_CourseOfferings_CourseId_AcademicSessionId_Semester",
                table: "CourseOfferings",
                columns: new[] { "CourseId", "AcademicSessionId", "Semester" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CourseOfferingLecturers_CourseOfferingId_LecturerId",
                table: "CourseOfferingLecturers",
                columns: new[] { "CourseOfferingId", "LecturerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CourseOfferingLecturers_LecturerId",
                table: "CourseOfferingLecturers",
                column: "LecturerId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseOfferingPrograms_CourseOfferingId_ProgramId_LevelId",
                table: "CourseOfferingPrograms",
                columns: new[] { "CourseOfferingId", "ProgramId", "LevelId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CourseOfferingPrograms_LevelId",
                table: "CourseOfferingPrograms",
                column: "LevelId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseOfferingPrograms_ProgramId",
                table: "CourseOfferingPrograms",
                column: "ProgramId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CourseOfferingLecturers");

            migrationBuilder.DropTable(
                name: "CourseOfferingPrograms");

            migrationBuilder.DropIndex(
                name: "IX_CourseOfferings_CourseId_AcademicSessionId_Semester",
                table: "CourseOfferings");

            migrationBuilder.AddColumn<string>(
                name: "CoLecturersJson",
                table: "CourseOfferings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LecturerId",
                table: "CourseOfferings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LevelId",
                table: "CourseOfferings",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "ProgramId",
                table: "CourseOfferings",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_CourseOfferings_CourseId_ProgramId_LevelId_AcademicSessionId_Semester",
                table: "CourseOfferings",
                columns: new[] { "CourseId", "ProgramId", "LevelId", "AcademicSessionId", "Semester" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CourseOfferings_LecturerId",
                table: "CourseOfferings",
                column: "LecturerId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseOfferings_LevelId",
                table: "CourseOfferings",
                column: "LevelId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseOfferings_ProgramId",
                table: "CourseOfferings",
                column: "ProgramId");

            migrationBuilder.AddForeignKey(
                name: "FK_CourseOfferings_Levels_LevelId",
                table: "CourseOfferings",
                column: "LevelId",
                principalTable: "Levels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CourseOfferings_Programs_ProgramId",
                table: "CourseOfferings",
                column: "ProgramId",
                principalTable: "Programs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CourseOfferings_Users_LecturerId",
                table: "CourseOfferings",
                column: "LecturerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
