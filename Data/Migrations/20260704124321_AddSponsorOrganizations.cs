using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LMS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSponsorOrganizations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='SponsorOrganizations' AND xtype='U')
                BEGIN
                    CREATE TABLE SponsorOrganizations (
                        Id uniqueidentifier NOT NULL DEFAULT NEWID(),
                        Name nvarchar(200) NOT NULL,
                        Code nvarchar(50) NOT NULL,
                        Email nvarchar(256) NULL,
                        Phone nvarchar(50) NULL,
                        IsActive bit NOT NULL DEFAULT 1,
                        CONSTRAINT PK_SponsorOrganizations PRIMARY KEY (Id),
                        CONSTRAINT UQ_SponsorOrganizations_Code UNIQUE (Code)
                    )
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
