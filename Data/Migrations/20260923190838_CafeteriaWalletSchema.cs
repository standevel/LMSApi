using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LMS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class CafeteriaWalletSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CafeteriaMenuItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VendorId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ImageUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    FeedingTimeId = table.Column<int>(type: "int", nullable: false),
                    FeedingTimeName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsAvailable = table.Column<bool>(type: "bit", nullable: false),
                    AvailableDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CafeteriaMenuItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CafeteriaMenuItemFavorites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Username = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    MenuItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SavedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CafeteriaMenuItemFavorites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CafeteriaMenuItemFavorites_CafeteriaMenuItems_MenuItemId",
                        column: x => x.MenuItemId,
                        principalTable: "CafeteriaMenuItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CafeteriaMenuItemFavorites_DeletedAt",
                table: "CafeteriaMenuItemFavorites",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CafeteriaMenuItemFavorites_MenuItemId",
                table: "CafeteriaMenuItemFavorites",
                column: "MenuItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CafeteriaMenuItemFavorites_Username_MenuItemId",
                table: "CafeteriaMenuItemFavorites",
                columns: new[] { "Username", "MenuItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CafeteriaMenuItems_VendorId_IsAvailable",
                table: "CafeteriaMenuItems",
                columns: new[] { "VendorId", "IsAvailable" });

            migrationBuilder.CreateTable(
                name: "CafeteriaVendorOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StudentUsername = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StudentName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MatricNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    VendorId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    VendorName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MenuItemId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MenuItemName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ImageUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsScholarshipCovered = table.Column<bool>(type: "bit", nullable: false),
                    Station = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    QrToken = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClaimedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CafeteriaVendorOrders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SystemCafeteriaConfigurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EnableSelfServiceTopUp = table.Column<bool>(type: "bit", nullable: false),
                    AllowStudentSelfTopUp = table.Column<bool>(type: "bit", nullable: false),
                    AllowParentTopUp = table.Column<bool>(type: "bit", nullable: false),
                    MaxSingleTopUpAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DailySpendLimit = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemCafeteriaConfigurations", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CafeteriaVendorOrders");

            migrationBuilder.DropTable(
                name: "SystemCafeteriaConfigurations");

            migrationBuilder.DropTable(
                name: "CafeteriaMenuItemFavorites");

            migrationBuilder.DropTable(
                name: "CafeteriaMenuItems");
        }
    }
}
