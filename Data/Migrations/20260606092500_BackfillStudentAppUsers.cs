using LMS.Api.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LMS.Api.Data.Migrations
{
    [DbContext(typeof(LmsDbContext))]
    [Migration("20260606092500_BackfillStudentAppUsers")]
    public partial class BackfillStudentAppUsers : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO [Users] ([Id], [EntraObjectId], [Username], [PasswordHash], [Email], [DisplayName], [IsActive], [CreatedUtc], [UpdatedUtc])
                SELECT
                    s.[Id],
                    COALESCE(NULLIF(s.[EntraObjectId], ''), CONCAT('student:', CONVERT(nvarchar(36), s.[Id]))),
                    s.[OfficialEmail],
                    NULL,
                    s.[OfficialEmail],
                    LTRIM(RTRIM(CONCAT(s.[FirstName], ' ', s.[LastName]))),
                    CAST(1 AS bit),
                    SYSUTCDATETIME(),
                    SYSUTCDATETIME()
                FROM [Students] s
                WHERE s.[OfficialEmail] IS NOT NULL
                  AND NOT EXISTS (
                      SELECT 1
                      FROM [Users] u
                      WHERE u.[Id] = s.[Id]
                         OR u.[Email] = s.[OfficialEmail]
                         OR u.[Username] = s.[OfficialEmail]
                         OR u.[EntraObjectId] = COALESCE(NULLIF(s.[EntraObjectId], ''), CONCAT('student:', CONVERT(nvarchar(36), s.[Id])))
                  );
                """);

            migrationBuilder.Sql("""
                UPDATE u
                SET
                    u.[DisplayName] = LTRIM(RTRIM(CONCAT(s.[FirstName], ' ', s.[LastName]))),
                    u.[IsActive] = CAST(1 AS bit),
                    u.[UpdatedUtc] = SYSUTCDATETIME()
                FROM [Users] u
                INNER JOIN [Students] s
                    ON u.[Id] = s.[Id]
                    OR u.[Email] = s.[OfficialEmail]
                    OR u.[Username] = s.[OfficialEmail]
                    OR u.[EntraObjectId] = s.[EntraObjectId]
                WHERE s.[OfficialEmail] IS NOT NULL;
                """);

            migrationBuilder.Sql("""
                INSERT INTO [UserRoles] ([UserId], [RoleId], [AssignedUtc])
                SELECT DISTINCT u.[Id], r.[Id], SYSUTCDATETIME()
                FROM [Students] s
                INNER JOIN [Users] u
                    ON u.[Id] = s.[Id]
                    OR u.[Email] = s.[OfficialEmail]
                    OR u.[Username] = s.[OfficialEmail]
                    OR u.[EntraObjectId] = COALESCE(NULLIF(s.[EntraObjectId], ''), CONCAT('student:', CONVERT(nvarchar(36), s.[Id])))
                INNER JOIN [Roles] r ON r.[Name] = 'Student'
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM [UserRoles] ur
                    WHERE ur.[UserId] = u.[Id]
                      AND ur.[RoleId] = r.[Id]
                );
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
