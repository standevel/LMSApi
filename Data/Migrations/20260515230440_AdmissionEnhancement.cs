using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LMS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AdmissionEnhancement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "IsExchangeProgram",
                table: "AdmissionApplications",
                newName: "HomeInstitutionVerified");

            migrationBuilder.AddColumn<string>(
                name: "CurrencyCode",
                table: "FeeLineItems",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "ExchangeRate",
                table: "FeeLineItems",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RateDate",
                table: "FeeLineItems",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "FinancialProofAmount",
                table: "AdmissionApplications",
                type: "decimal(18,2)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ALevelPoints",
                table: "AdmissionApplications",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CGPAScaleMax",
                table: "AdmissionApplications",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CGPAScaleMin",
                table: "AdmissionApplications",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CGPAScaleName",
                table: "AdmissionApplications",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ConvertedCGPA",
                table: "AdmissionApplications",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CountryName",
                table: "AdmissionApplications",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CountryOfOrigin",
                table: "AdmissionApplications",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeansCertificateDocumentId",
                table: "AdmissionApplications",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DirectEntryGrade",
                table: "AdmissionApplications",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DirectEntryInstitution",
                table: "AdmissionApplications",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DirectEntryPoints",
                table: "AdmissionApplications",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DirectEntryQualification",
                table: "AdmissionApplications",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "DirectEntrySubject1",
                table: "AdmissionApplications",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DirectEntrySubject2",
                table: "AdmissionApplications",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DirectEntrySubject3",
                table: "AdmissionApplications",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DirectEntryYear",
                table: "AdmissionApplications",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExchangeDurationMonths",
                table: "AdmissionApplications",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExchangeEndDate",
                table: "AdmissionApplications",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ExchangePartnerAgreementId",
                table: "AdmissionApplications",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExchangeProgramType",
                table: "AdmissionApplications",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExchangeStartDate",
                table: "AdmissionApplications",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExchangeStatus",
                table: "AdmissionApplications",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "FinancialProofDocumentId",
                table: "AdmissionApplications",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "HomeInstitutionApprovalDocumentId",
                table: "AdmissionApplications",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HomeInstitutionCountry",
                table: "AdmissionApplications",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HomeInstitutionName",
                table: "AdmissionApplications",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HomeInstitutionStanding",
                table: "AdmissionApplications",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "HomeInstitutionTranscriptDocumentId",
                table: "AdmissionApplications",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "HomeInstitutionVerifiedAt",
                table: "AdmissionApplications",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HomeInstitutionVerifiedBy",
                table: "AdmissionApplications",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ImmigrationStatus",
                table: "AdmissionApplications",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IntendedSemester",
                table: "AdmissionApplications",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Region",
                table: "AdmissionApplications",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TransferLevelSuggestion",
                table: "AdmissionApplications",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TransferableCredits",
                table: "AdmissionApplications",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VisaExpiryDate",
                table: "AdmissionApplications",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VisaStatus",
                table: "AdmissionApplications",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VisaType",
                table: "AdmissionApplications",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Countries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Region = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CallingCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Countries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CourseEquivalencies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceInstitution = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    SourceCourseCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SourceCourseName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SourceCredits = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    TargetCourseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TargetCredits = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    MappingNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseEquivalencies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CourseEquivalencies_Courses_TargetCourseId",
                        column: x => x.TargetCourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "CredentialEvaluations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Evaluator = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EvaluationReportId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EvaluationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EquivalencyDegree = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EquivalencyMajor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    EquivalencyGPA = table.Column<decimal>(type: "decimal(4,2)", nullable: true),
                    EquivalencyScale = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ReportDocumentUrl = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ReportDocumentFileName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CredentialEvaluations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CredentialEvaluations_AdmissionApplications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "AdmissionApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CreditTransferRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProgramId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceCountryCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreditsPerYear = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    MaxTransferablePercentage = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    MaxTransferableCredits = table.Column<int>(type: "int", nullable: false),
                    MinCGPA = table.Column<decimal>(type: "decimal(4,2)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreditTransferRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CreditTransferRules_Programs_ProgramId",
                        column: x => x.ProgramId,
                        principalTable: "Programs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GPAScaleConversions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CountryCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    ScaleName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ScaleMax = table.Column<decimal>(type: "decimal(4,2)", nullable: false),
                    ScaleMin = table.Column<decimal>(type: "decimal(4,2)", nullable: false),
                    EquivalentCGPA = table.Column<decimal>(type: "decimal(4,2)", nullable: false),
                    MinPassingScore = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GPAScaleConversions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GradingScales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CountryCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    QualificationType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    GradesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GradingScales", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProgramCreditMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProgramId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreditsPerLevel = table.Column<int>(type: "int", nullable: false),
                    MaxTransferablePercentage = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    MaxTransferableCredits = table.Column<int>(type: "int", nullable: false),
                    MinCreditsAtLMS = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProgramCreditMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProgramCreditMappings_Programs_ProgramId",
                        column: x => x.ProgramId,
                        principalTable: "Programs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProgramPrerequisites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProgramId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequiredSubjectCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RequiredSubjectName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    MinGrade = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    IsCore = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProgramPrerequisites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProgramPrerequisites_Programs_ProgramId",
                        column: x => x.ProgramId,
                        principalTable: "Programs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DirectEntryGradeConfigurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GradingScaleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QualificationType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    GradesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DirectEntryGradeConfigurations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DirectEntryGradeConfigurations_GradingScales_GradingScaleId",
                        column: x => x.GradingScaleId,
                        principalTable: "GradingScales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Countries_Code",
                table: "Countries",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CourseEquivalencies_TargetCourseId",
                table: "CourseEquivalencies",
                column: "TargetCourseId");

            migrationBuilder.CreateIndex(
                name: "IX_CredentialEvaluations_ApplicationId",
                table: "CredentialEvaluations",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditTransferRules_ProgramId",
                table: "CreditTransferRules",
                column: "ProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_DirectEntryGradeConfigurations_GradingScaleId",
                table: "DirectEntryGradeConfigurations",
                column: "GradingScaleId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramCreditMappings_ProgramId",
                table: "ProgramCreditMappings",
                column: "ProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramPrerequisites_ProgramId",
                table: "ProgramPrerequisites",
                column: "ProgramId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Countries");

            migrationBuilder.DropTable(
                name: "CourseEquivalencies");

            migrationBuilder.DropTable(
                name: "CredentialEvaluations");

            migrationBuilder.DropTable(
                name: "CreditTransferRules");

            migrationBuilder.DropTable(
                name: "DirectEntryGradeConfigurations");

            migrationBuilder.DropTable(
                name: "GPAScaleConversions");

            migrationBuilder.DropTable(
                name: "ProgramCreditMappings");

            migrationBuilder.DropTable(
                name: "ProgramPrerequisites");

            migrationBuilder.DropTable(
                name: "GradingScales");

            migrationBuilder.DropColumn(
                name: "CurrencyCode",
                table: "FeeLineItems");

            migrationBuilder.DropColumn(
                name: "ExchangeRate",
                table: "FeeLineItems");

            migrationBuilder.DropColumn(
                name: "RateDate",
                table: "FeeLineItems");

            migrationBuilder.DropColumn(
                name: "ALevelPoints",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "CGPAScaleMax",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "CGPAScaleMin",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "CGPAScaleName",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "ConvertedCGPA",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "CountryName",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "CountryOfOrigin",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "DeansCertificateDocumentId",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "DirectEntryGrade",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "DirectEntryInstitution",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "DirectEntryPoints",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "DirectEntryQualification",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "DirectEntrySubject1",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "DirectEntrySubject2",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "DirectEntrySubject3",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "DirectEntryYear",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "ExchangeDurationMonths",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "ExchangeEndDate",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "ExchangePartnerAgreementId",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "ExchangeProgramType",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "ExchangeStartDate",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "ExchangeStatus",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "FinancialProofDocumentId",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "HomeInstitutionApprovalDocumentId",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "HomeInstitutionCountry",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "HomeInstitutionName",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "HomeInstitutionStanding",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "HomeInstitutionTranscriptDocumentId",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "HomeInstitutionVerifiedAt",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "HomeInstitutionVerifiedBy",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "ImmigrationStatus",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "IntendedSemester",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "Region",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "TransferLevelSuggestion",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "TransferableCredits",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "VisaExpiryDate",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "VisaStatus",
                table: "AdmissionApplications");

            migrationBuilder.DropColumn(
                name: "VisaType",
                table: "AdmissionApplications");

            migrationBuilder.RenameColumn(
                name: "HomeInstitutionVerified",
                table: "AdmissionApplications",
                newName: "IsExchangeProgram");

            migrationBuilder.AlterColumn<string>(
                name: "FinancialProofAmount",
                table: "AdmissionApplications",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldMaxLength: 50,
                oldNullable: true);
        }
    }
}
