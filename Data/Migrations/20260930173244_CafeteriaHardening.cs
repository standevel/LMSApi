using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LMS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class CafeteriaHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProfileCompletionRequirements_AcademicSessions_AcademicSessionId",
                table: "ProfileCompletionRequirements");

            migrationBuilder.DropForeignKey(
                name: "FK_ProfileCompletionRequirements_Faculties_FacultyId",
                table: "ProfileCompletionRequirements");

            migrationBuilder.DropForeignKey(
                name: "FK_ProfileCompletionRequirements_Levels_AcademicLevelId",
                table: "ProfileCompletionRequirements");

            migrationBuilder.DropForeignKey(
                name: "FK_ProfileCompletionRequirements_Programs_AcademicProgramId",
                table: "ProfileCompletionRequirements");

            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[CafeteriaWalletAccounts]', N'U') IS NULL
BEGIN
    CREATE TABLE [CafeteriaWalletAccounts] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NULL,
        [StudentId] uniqueidentifier NULL,
        [Username] nvarchar(256) NOT NULL,
        [Balance] decimal(18,2) NOT NULL DEFAULT 0,
        [CreatedAt] datetime2 NOT NULL DEFAULT GETUTCDATE(),
        [UpdatedAt] datetime2 NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT [PK_CafeteriaWalletAccounts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CafeteriaWalletAccounts_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_CafeteriaWalletAccounts_Students_StudentId] FOREIGN KEY ([StudentId]) REFERENCES [Students] ([Id]) ON DELETE SET NULL
    );
    CREATE UNIQUE INDEX [IX_CafeteriaWalletAccounts_Username] ON [CafeteriaWalletAccounts] ([Username]);
    CREATE INDEX [IX_CafeteriaWalletAccounts_UserId] ON [CafeteriaWalletAccounts] ([UserId]);
    CREATE INDEX [IX_CafeteriaWalletAccounts_StudentId] ON [CafeteriaWalletAccounts] ([StudentId]);
END

IF OBJECT_ID(N'[CafeteriaWalletTransactions]', N'U') IS NULL
BEGIN
    CREATE TABLE [CafeteriaWalletTransactions] (
        [Id] uniqueidentifier NOT NULL,
        [WalletAccountId] uniqueidentifier NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [TransactionType] nvarchar(50) NOT NULL,
        [Gateway] nvarchar(50) NOT NULL,
        [Reference] nvarchar(200) NOT NULL,
        [Status] nvarchar(50) NOT NULL,
        [Description] nvarchar(500) NOT NULL,
        [BalanceAfter] decimal(18,2) NOT NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT GETUTCDATE(),
        [VerifiedAt] datetime2 NULL,
        CONSTRAINT [PK_CafeteriaWalletTransactions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CafeteriaWalletTransactions_CafeteriaWalletAccounts_WalletAccountId] FOREIGN KEY ([WalletAccountId]) REFERENCES [CafeteriaWalletAccounts] ([Id]) ON DELETE CASCADE
    );
    CREATE UNIQUE INDEX [IX_CafeteriaWalletTransactions_Reference] ON [CafeteriaWalletTransactions] ([Reference]);
    CREATE INDEX [IX_CafeteriaWalletTransactions_WalletAccountId] ON [CafeteriaWalletTransactions] ([WalletAccountId]);
    CREATE INDEX [IX_CafeteriaWalletTransactions_Status] ON [CafeteriaWalletTransactions] ([Status]);
END
");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyToken",
                table: "CafeteriaWalletAccounts",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiresAt",
                table: "CafeteriaVendorOrders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ProfileCompletionRequirements_AcademicSessions_AcademicSessionId",
                table: "ProfileCompletionRequirements",
                column: "AcademicSessionId",
                principalTable: "AcademicSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProfileCompletionRequirements_Faculties_FacultyId",
                table: "ProfileCompletionRequirements",
                column: "FacultyId",
                principalTable: "Faculties",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProfileCompletionRequirements_Levels_AcademicLevelId",
                table: "ProfileCompletionRequirements",
                column: "AcademicLevelId",
                principalTable: "Levels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProfileCompletionRequirements_Programs_AcademicProgramId",
                table: "ProfileCompletionRequirements",
                column: "AcademicProgramId",
                principalTable: "Programs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProfileCompletionRequirements_AcademicSessions_AcademicSessionId",
                table: "ProfileCompletionRequirements");

            migrationBuilder.DropForeignKey(
                name: "FK_ProfileCompletionRequirements_Faculties_FacultyId",
                table: "ProfileCompletionRequirements");

            migrationBuilder.DropForeignKey(
                name: "FK_ProfileCompletionRequirements_Levels_AcademicLevelId",
                table: "ProfileCompletionRequirements");

            migrationBuilder.DropForeignKey(
                name: "FK_ProfileCompletionRequirements_Programs_AcademicProgramId",
                table: "ProfileCompletionRequirements");

            migrationBuilder.DropColumn(
                name: "ConcurrencyToken",
                table: "CafeteriaWalletAccounts");

            migrationBuilder.DropColumn(
                name: "ExpiresAt",
                table: "CafeteriaVendorOrders");

            migrationBuilder.AddForeignKey(
                name: "FK_ProfileCompletionRequirements_AcademicSessions_AcademicSessionId",
                table: "ProfileCompletionRequirements",
                column: "AcademicSessionId",
                principalTable: "AcademicSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ProfileCompletionRequirements_Faculties_FacultyId",
                table: "ProfileCompletionRequirements",
                column: "FacultyId",
                principalTable: "Faculties",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ProfileCompletionRequirements_Levels_AcademicLevelId",
                table: "ProfileCompletionRequirements",
                column: "AcademicLevelId",
                principalTable: "Levels",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ProfileCompletionRequirements_Programs_AcademicProgramId",
                table: "ProfileCompletionRequirements",
                column: "AcademicProgramId",
                principalTable: "Programs",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
