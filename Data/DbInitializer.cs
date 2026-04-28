using LMS.Api.Data.Entities;
using LMS.Api.Data.Enums;
using LMS.Api.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LMS.Api.Data;

public interface IDbInitializer
{
    Task InitializeAsync(CancellationToken ct = default);
}

public sealed class DbInitializer(LmsDbContext dbContext, ILogger<DbInitializer> logger) : IDbInitializer
{
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await dbContext.Database.MigrateAsync(ct);
        await SeedRolesAsync(ct);
        await SeedPermissionsAsync(ct);
        await SeedRolePermissionsAsync(ct);
        await SeedSessionsAsync(ct);
        await SeedFacultiesAsync(ct);
        await SeedDepartmentsAsync(ct);
        await SeedProgramsAsync(ct);
        await SeedLecturersAsync(ct);
        await SeedDocumentTypesAsync(ct);
        await SeedSponsorsAsync(ct);
        await SeedSubjectsAsync(ct);
       
        logger.LogInformation("Database initialization completed successfully.");
    }

    private async Task SeedRolesAsync(CancellationToken ct)
    {
        var roleNames = new[]
        {
            LmsRoles.SuperAdmin,
            LmsRoles.Admin,
            LmsRoles.ViceChancellor,
            LmsRoles.Dean,
            LmsRoles.Lecturer,
            LmsRoles.Student,
            LmsRoles.Registrar,
            LmsRoles.Finance,
            LmsRoles.Alumni,
            LmsRoles.Guest,
            LmsRoles.HOD
        };

        var existing = await dbContext.Roles.Select(x => x.Name).ToListAsync(ct);
        var missing = roleNames.Except(existing, StringComparer.OrdinalIgnoreCase).ToList();
        if (missing.Count == 0)
        {
            return;
        }

        foreach (var roleName in missing)
        {
            dbContext.Roles.Add(new AppRole { Name = roleName, Description = $"{roleName} role" });
        }

        await dbContext.SaveChangesAsync(ct);
    }

    private async Task SeedPermissionsAsync(CancellationToken ct)
    {
        var existing = await dbContext.Permissions.Select(x => x.Code).ToListAsync(ct);
        var missing = LmsPermissions.All.Except(existing, StringComparer.OrdinalIgnoreCase).ToList();
        if (missing.Count == 0)
        {
            return;
        }

        foreach (var permissionCode in missing)
        {
            dbContext.Permissions.Add(new AppPermission { Code = permissionCode, Description = permissionCode });
        }

        await dbContext.SaveChangesAsync(ct);
    }

    private async Task SeedRolePermissionsAsync(CancellationToken ct)
    {
        var roles = await dbContext.Roles.AsNoTracking().ToDictionaryAsync(x => x.Name, StringComparer.OrdinalIgnoreCase, ct);
        var permissions = await dbContext.Permissions.AsNoTracking().ToDictionaryAsync(x => x.Code, StringComparer.OrdinalIgnoreCase, ct);

        var map = new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [LmsRoles.SuperAdmin] = LmsPermissions.All,
            [LmsRoles.Admin] =
            [
                LmsPermissions.UsersManage,
                LmsPermissions.CoursesManage,
                LmsPermissions.CoursesTeach,
                LmsPermissions.GradesSubmit,
                LmsPermissions.RecordsManage,
                LmsPermissions.ReportsView,
                LmsPermissions.EnrollmentsManage,
                LmsPermissions.ProfileView
            ],
            [LmsRoles.ViceChancellor] = [LmsPermissions.UsersManage, LmsPermissions.RolesManage, LmsPermissions.PermissionsManage, LmsPermissions.CoursesManage, LmsPermissions.ReportsView],
            [LmsRoles.Dean] = [LmsPermissions.CoursesManage, LmsPermissions.CoursesTeach, LmsPermissions.ReportsView, LmsPermissions.EnrollmentsManage],
            [LmsRoles.Lecturer] = [LmsPermissions.CoursesTeach, LmsPermissions.GradesSubmit, LmsPermissions.ProfileView],
            [LmsRoles.Student] = [LmsPermissions.ProfileView],
            [LmsRoles.Registrar] = [LmsPermissions.RecordsManage, LmsPermissions.EnrollmentsManage, LmsPermissions.UsersManage]
        };

        var existingPairs = await dbContext.RolePermissions
            .AsNoTracking()
            .Select(x => new { x.RoleId, x.PermissionId })
            .ToListAsync(ct);

        var existingSet = existingPairs.ToHashSet();
        var rowsToAdd = new List<RolePermission>();

        foreach (var roleMap in map)
        {
            if (!roles.TryGetValue(roleMap.Key, out var role))
            {
                continue;
            }

            foreach (var permissionCode in roleMap.Value)
            {
                if (!permissions.TryGetValue(permissionCode, out var permission))
                {
                    continue;
                }

                var pair = new { RoleId = role.Id, PermissionId = permission.Id };
                if (existingSet.Contains(pair))
                {
                    continue;
                }

                rowsToAdd.Add(new RolePermission
                {
                    RoleId = role.Id,
                    PermissionId = permission.Id,
                    AssignedUtc = DateTime.UtcNow
                });
            }
        }

        if (roles.TryGetValue(LmsRoles.Admin, out var adminRole)
            && permissions.TryGetValue(LmsPermissions.AccessManage, out var accessManagePermission))
        {
            var adminAccessPermission = await dbContext.RolePermissions
                .FirstOrDefaultAsync(x => x.RoleId == adminRole.Id && x.PermissionId == accessManagePermission.Id, ct);

            if (adminAccessPermission is not null)
            {
                dbContext.RolePermissions.Remove(adminAccessPermission);
            }
        }

        if (rowsToAdd.Count == 0 && dbContext.ChangeTracker.Entries<RolePermission>().All(x => x.State == EntityState.Unchanged))
        {
            return;
        }

        dbContext.RolePermissions.AddRange(rowsToAdd);
        await dbContext.SaveChangesAsync(ct);
    }

    private async Task SeedSessionsAsync(CancellationToken ct)
    {
        if (await dbContext.AcademicSessions.AnyAsync(ct))
        {
            logger.LogInformation("Sessions already seeded. Skipping.");
            return;
        }

        logger.LogInformation("Seeding Academic Sessions...");
        dbContext.AcademicSessions.AddRange(
            new AcademicSession
            {
                Name = "2024/2025",
                StartDate = new DateTime(2024, 9, 1),
                EndDate = new DateTime(2025, 8, 31),
                IsActive = true,
                IsAdmissionOpen = true
            },
            new AcademicSession
            {
                Name = "2025/2026",
                StartDate = new DateTime(2025, 9, 1),
                EndDate = new DateTime(2026, 8, 31),
                IsActive = false,
                IsAdmissionOpen = true
            }
        );
        await dbContext.SaveChangesAsync(ct);
    }

    private async Task SeedFacultiesAsync(CancellationToken ct)
    {
        logger.LogInformation("Seeding Faculties...");
        
        // Clear existing data in correct order to handle foreign key constraints
        var existingAdmissionApps = await dbContext.AdmissionApplications.ToListAsync(ct);
        if (existingAdmissionApps.Any())
        {
            logger.LogInformation("Removing {count} existing admission applications", existingAdmissionApps.Count);
            dbContext.AdmissionApplications.RemoveRange(existingAdmissionApps);
            await dbContext.SaveChangesAsync(ct);
        }

        var existingPrograms = await dbContext.Programs.Include(p => p.Levels).ToListAsync(ct);
        if (existingPrograms.Any())
        {
            logger.LogInformation("Removing {count} existing programs and their levels", existingPrograms.Count);
            var levels = existingPrograms.SelectMany(p => p.Levels).ToList();
            if (levels.Any())
            {
                dbContext.Levels.RemoveRange(levels);
            }
            dbContext.Programs.RemoveRange(existingPrograms);
            await dbContext.SaveChangesAsync(ct);
        }

        var existingDepartments = await dbContext.Departments.ToListAsync(ct);
        if (existingDepartments.Any())
        {
            logger.LogInformation("Removing {count} existing departments", existingDepartments.Count);
            dbContext.Departments.RemoveRange(existingDepartments);
            await dbContext.SaveChangesAsync(ct);
        }

        var existingFaculties = await dbContext.Faculties.ToListAsync(ct);
        if (existingFaculties.Any())
        {
            logger.LogInformation("Removing {count} existing faculties", existingFaculties.Count);
            dbContext.Faculties.RemoveRange(existingFaculties);
            await dbContext.SaveChangesAsync(ct);
        }

        // Create the correct faculties
        var faculties = new List<Faculty>
        {
            new Faculty { Name = "Arts", Label = "College" },
            new Faculty { Name = "Engineering", Label = "College" },
            new Faculty { Name = "Management and Social Sciences", Label = "College" },
            new Faculty { Name = "Science and Computing", Label = "College" },
            new Faculty { Name = "Allied Health", Label = "College" },
            new Faculty { Name = "Agriculture and Natural Sciences", Label = "College" }
        };
        
        dbContext.Faculties.AddRange(faculties);
        await dbContext.SaveChangesAsync(ct);
        
        logger.LogInformation("Created {count} faculties", faculties.Count);
    }

    private async Task SeedDepartmentsAsync(CancellationToken ct)
    {
        if (await dbContext.Departments.AnyAsync(ct))
        {
            logger.LogInformation("Departments already seeded. Skipping.");
            return;
        }

        logger.LogInformation("Seeding Departments...");

        // Get the correct faculties
        var faculties = await dbContext.Faculties.ToListAsync(ct);
        var arts = faculties.FirstOrDefault(f => f.Name == "Arts");
        var engineering = faculties.FirstOrDefault(f => f.Name == "Engineering");
        var mgmtSocial = faculties.FirstOrDefault(f => f.Name == "Management and Social Sciences");
        var scienceComputing = faculties.FirstOrDefault(f => f.Name == "Science and Computing");

        if (arts == null || engineering == null || mgmtSocial == null || scienceComputing == null)
        {
            logger.LogError("Required faculties not found for department seeding. Skipping.");
            return;
        }

        var departments = new List<Department>
        {
            // Arts
            new() { Name = "Film & Media Arts", Code = "FMA", FacultyId = arts.Id },
            new() { Name = "Fine Arts & Design", Code = "FAD", FacultyId = arts.Id },
            new() { Name = "Theatre Arts", Code = "TA", FacultyId = arts.Id },

            // Engineering
            new() { Name = "Electrical Engineering", Code = "EE", FacultyId = engineering.Id },
            new() { Name = "Mechanical Engineering", Code = "ME", FacultyId = engineering.Id },
            new() { Name = "Computer Engineering", Code = "CEN", FacultyId = engineering.Id },

            // Management and Social Sciences
            new() { Name = "Accounting & Data Analytics", Code = "ADA", FacultyId = mgmtSocial.Id },
            new() { Name = "Business Management", Code = "BM", FacultyId = mgmtSocial.Id },
            new() { Name = "Economics", Code = "EC", FacultyId = mgmtSocial.Id },
            new() { Name = "Finance & Financial Technology", Code = "FIN", FacultyId = mgmtSocial.Id },

            // Science and Computing
            new() { Name = "Computer Science", Code = "CS", FacultyId = scienceComputing.Id },
            new() { Name = "Software Engineering", Code = "SE", FacultyId = scienceComputing.Id },
            new() { Name = "Cyber Security", Code = "CY", FacultyId = scienceComputing.Id },
            new() { Name = "Information & Communications Technology", Code = "ICT", FacultyId = scienceComputing.Id },
            new() { Name = "Mathematics & Data Science", Code = "MDS", FacultyId = scienceComputing.Id }
        };

        dbContext.Departments.AddRange(departments);
        await dbContext.SaveChangesAsync(ct);
        
        logger.LogInformation("Created {count} departments mapped to correct faculties", departments.Count);
    }

    private async Task SeedProgramsAsync(CancellationToken ct)
    {
        if (await dbContext.Programs.AnyAsync(ct))
        {
            logger.LogInformation("Programs already seeded. Skipping.");
            return;
        }

        logger.LogInformation("Seeding Academic Programs and Levels...");

        // Get the correct faculties
        var faculties = await dbContext.Faculties.ToListAsync(ct);
        var arts = faculties.FirstOrDefault(f => f.Name == "Arts");
        var engineering = faculties.FirstOrDefault(f => f.Name == "Engineering");
        var mgmtSocial = faculties.FirstOrDefault(f => f.Name == "Management and Social Sciences");
        var scienceComputing = faculties.FirstOrDefault(f => f.Name == "Science and Computing");

        if (arts == null || engineering == null || mgmtSocial == null || scienceComputing == null)
        {
            logger.LogError("Required faculties not found for program seeding. Skipping.");
            return;
        }

        // Get departments
        var csDept = await dbContext.Departments.FirstOrDefaultAsync(d => d.Code == "CS", ct);
        var seDept = await dbContext.Departments.FirstOrDefaultAsync(d => d.Code == "SE", ct);
        var cyDept = await dbContext.Departments.FirstOrDefaultAsync(d => d.Code == "CY", ct);
        var ictDept = await dbContext.Departments.FirstOrDefaultAsync(d => d.Code == "ICT", ct);
        var mdsDept = await dbContext.Departments.FirstOrDefaultAsync(d => d.Code == "MDS", ct);
        var eeDept = await dbContext.Departments.FirstOrDefaultAsync(d => d.Code == "EE", ct);
        var meDept = await dbContext.Departments.FirstOrDefaultAsync(d => d.Code == "ME", ct);
        var cenDept = await dbContext.Departments.FirstOrDefaultAsync(d => d.Code == "CEN", ct);
        var fmaDept = await dbContext.Departments.FirstOrDefaultAsync(d => d.Code == "FMA", ct);
        var fadDept = await dbContext.Departments.FirstOrDefaultAsync(d => d.Code == "FAD", ct);
        var taDept = await dbContext.Departments.FirstOrDefaultAsync(d => d.Code == "TA", ct);
        var adaDept = await dbContext.Departments.FirstOrDefaultAsync(d => d.Code == "ADA", ct);
        var bmDept = await dbContext.Departments.FirstOrDefaultAsync(d => d.Code == "BM", ct);
        var ecDept = await dbContext.Departments.FirstOrDefaultAsync(d => d.Code == "EC", ct);
        var finDept = await dbContext.Departments.FirstOrDefaultAsync(d => d.Code == "FIN", ct);

        var programs = new List<AcademicProgram>
        {
            // Science and Computing
            new() { Name = "B.Sc. Computer Science", Code = "BCS", FacultyId = scienceComputing.Id, DepartmentId = csDept?.Id, Type = ProgramType.Undergraduate, DurationYears = 4 },
            new() { Name = "B.Sc. Software Engineering", Code = "BSE", FacultyId = scienceComputing.Id, DepartmentId = seDept?.Id, Type = ProgramType.Undergraduate, DurationYears = 4 },
            new() { Name = "B.Sc. Cyber Security", Code = "BCY", FacultyId = scienceComputing.Id, DepartmentId = cyDept?.Id, Type = ProgramType.Undergraduate, DurationYears = 4 },
            new() { Name = "B.Sc. Information & Communications Technology", Code = "BICT", FacultyId = scienceComputing.Id, DepartmentId = ictDept?.Id, Type = ProgramType.Undergraduate, DurationYears = 4 },
            new() { Name = "B.Sc. Mathematics & Data Science", Code = "BMDS", FacultyId = scienceComputing.Id, DepartmentId = mdsDept?.Id, Type = ProgramType.Undergraduate, DurationYears = 4 },

            // Engineering
            new() { Name = "B.Eng. Electrical Engineering", Code = "BEE", FacultyId = engineering.Id, DepartmentId = eeDept?.Id, Type = ProgramType.Undergraduate, DurationYears = 5 },
            new() { Name = "B.Eng. Mechanical Engineering", Code = "BME", FacultyId = engineering.Id, DepartmentId = meDept?.Id, Type = ProgramType.Undergraduate, DurationYears = 5 },
            new() { Name = "B.Eng. Computer Engineering", Code = "BCEN", FacultyId = engineering.Id, DepartmentId = cenDept?.Id, Type = ProgramType.Undergraduate, DurationYears = 5 },

            // Arts
            new() { Name = "B.A. Film & Media Studies", Code = "BFMS", FacultyId = arts.Id, DepartmentId = fmaDept?.Id, Type = ProgramType.Undergraduate, DurationYears = 4 },
            new() { Name = "B.A. Fine Arts & Design", Code = "BFAD", FacultyId = arts.Id, DepartmentId = fadDept?.Id, Type = ProgramType.Undergraduate, DurationYears = 4 },
            new() { Name = "B.A. Theatre Arts", Code = "BTA", FacultyId = arts.Id, DepartmentId = taDept?.Id, Type = ProgramType.Undergraduate, DurationYears = 4 },

            // Management and Social Sciences
            new() { Name = "B.Sc. Accounting & Data Analytics", Code = "BADA", FacultyId = mgmtSocial.Id, DepartmentId = adaDept?.Id, Type = ProgramType.Undergraduate, DurationYears = 4 },
            new() { Name = "B.Sc. Business Management", Code = "BBM", FacultyId = mgmtSocial.Id, DepartmentId = bmDept?.Id, Type = ProgramType.Undergraduate, DurationYears = 4 },
            new() { Name = "B.Sc. Economics", Code = "BEC", FacultyId = mgmtSocial.Id, DepartmentId = ecDept?.Id, Type = ProgramType.Undergraduate, DurationYears = 4 },
            new() { Name = "B.Sc. Finance & Financial Technology", Code = "BFIN", FacultyId = mgmtSocial.Id, DepartmentId = finDept?.Id, Type = ProgramType.Undergraduate, DurationYears = 4 }
        };

        dbContext.Programs.AddRange(programs);
        await dbContext.SaveChangesAsync(ct);

        foreach (var p in programs)
        {
            for (int i = 1; i <= p.DurationYears; i++)
            {
                dbContext.Levels.Add(new AcademicLevel
                {
                    ProgramId = p.Id,
                    Name = $"{i}00 Level",
                    Order = i
                });
            }
        }
        await dbContext.SaveChangesAsync(ct);
    }

    private async Task SeedLecturersAsync(CancellationToken ct)
    {
        var role = await dbContext.Roles.FirstOrDefaultAsync(x => x.Name == LmsRoles.Lecturer, ct);
        if (role == null)
        {
            logger.LogWarning("Lecturer role not found. Skipping lecturer seeding.");
            return;
        }

        var lecturers = new[]
        {
            new { Name = "Dr. John Doe", Email = "john.doe@wigweuniversity.edu.ng", Oid = "seed-oid-1" },
            new { Name = "Prof. Jane Smith", Email = "jane.smith@wigweuniversity.edu.ng", Oid = "seed-oid-2" }
        };

        foreach (var l in lecturers)
        {
            if (await dbContext.Users.AnyAsync(x => x.Email == l.Email || x.EntraObjectId == l.Oid, ct))
            {
                logger.LogInformation("Lecturer {Email} already exists. Skipping.", l.Email);
                continue;
            }

            logger.LogInformation("Seeding Lecturer: {Email}...", l.Email);
            var user = new AppUser
            {
                DisplayName = l.Name,
                Email = l.Email,
                EntraObjectId = l.Oid,
                Username = l.Email,
                IsActive = true
            };
            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync(ct);
            dbContext.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
        }
        await dbContext.SaveChangesAsync(ct);
    }
    private async Task SeedDocumentTypesAsync(CancellationToken ct)
    {
        if (await dbContext.DocumentTypes.AnyAsync(ct))
        {
            logger.LogInformation("Document types already seeded. Skipping.");
            return;
        }

        logger.LogInformation("Seeding Document Types...");
        dbContext.DocumentTypes.AddRange(
            new DocumentType { Name = "JAMB Result", Code = "JAMB_RESULT", Category = DocumentCategory.Admission, IsCompulsory = true },
            new DocumentType { Name = "O-Level Result", Code = "OLEVEL_RESULT", Category = DocumentCategory.Admission, IsCompulsory = true },
            new DocumentType { Name = "Birth Certificate", Code = "BIRTH_CERT", Category = DocumentCategory.Admission, IsCompulsory = false },
            new DocumentType { Name = "State of Origin", Code = "STATE_ORIGIN", Category = DocumentCategory.Admission, IsCompulsory = false },
            new DocumentType { Name = "Sponsorship Document", Code = "SPONSOR_DOCUMENT", Category = DocumentCategory.Admission, IsCompulsory = false }
        );
        await dbContext.SaveChangesAsync(ct);
    }

    private async Task SeedSponsorsAsync(CancellationToken ct)
    {
        if (await dbContext.SponsorOrganizations.AnyAsync(ct))
        {
            logger.LogInformation("Sponsors already seeded. Skipping.");
            return;
        }

        logger.LogInformation("Seeding Sponsor Organizations...");
        dbContext.SponsorOrganizations.AddRange(
            new SponsorOrganization { Name = "Wigwe Foundation", Code = "SP-WIGWE" },
            new SponsorOrganization { Name = "Nigeria LNG Ltd (NLNG)", Code = "SP-NLNG" },
            new SponsorOrganization { Name = "Petroleum Technology Development Fund (PTDF)", Code = "SP-PTDF" },
            new SponsorOrganization { Name = "TETFund (Tertiary Education Trust Fund)", Code = "SP-TETF" },
            new SponsorOrganization { Name = "Shell Petroleum Development Company", Code = "SP-SHELL" },
            new SponsorOrganization { Name = "Dangote Foundation", Code = "SP-DANGOTE" },
            new SponsorOrganization { Name = "Chevron Nigeria", Code = "SP-CHEVRON" },
            new SponsorOrganization { Name = "Access Bank Educational Fund", Code = "SP-ACCESS" },
            new SponsorOrganization { Name = "Zenith Bank Scholarship", Code = "SP-ZENITH" },
            new SponsorOrganization { Name = "Agip Energy Scholarship", Code = "SP-AGIP" },
            new SponsorOrganization { Name = "TotalEnergies Nigeria", Code = "SP-TOTAL" }
        );
        await dbContext.SaveChangesAsync(ct);
    }

    private async Task SeedSubjectsAsync(CancellationToken ct)
    {
        if (await dbContext.Subjects.AnyAsync(ct))
        {
            logger.LogInformation("Subjects already seeded. Skipping.");
            return;
        }

        logger.LogInformation("Seeding Subjects...");
        var subjects = new[]
        {
            "English Language", "Mathematics", "Further Mathematics", "Biology", "Chemistry",
            "Physics", "Agricultural Science", "Economics", "Government", "Geography",
            "Literature in English", "Christian Religious Studies", "Islamic Studies",
            "History", "Civic Education", "Fine Arts", "Music", "Technical Drawing",
            "Food and Nutrition", "Home Economics", "Commerce", "Financial Accounting",
            "Business Studies", "French", "Yoruba", "Igbo", "Hausa", "Arabic",
            "Basic Technology", "Computer Studies", "Health Science", "Physical Education",
            "Data Processing", "Office Practice", "Marketing", "Insurance",
            "Catering Craft Practice", "Typewriting", "Animal Husbandry", "Fisheries",
            "Forestry", "Tourism"
        };

        foreach (var s in subjects)
        {
            dbContext.Subjects.Add(new Subject { Name = s });
        }
        await dbContext.SaveChangesAsync(ct);
    }
}
