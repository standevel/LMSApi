using LMS.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Data;

public sealed class LmsDbContext(DbContextOptions<LmsDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<AppRole> Roles => Set<AppRole>();
    public DbSet<AppPermission> Permissions => Set<AppPermission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserPermission> UserPermissions => Set<UserPermission>();
    public DbSet<AcademicSession> AcademicSessions => Set<AcademicSession>();
    public DbSet<Faculty> Faculties => Set<Faculty>();
    public DbSet<AcademicProgram> Programs => Set<AcademicProgram>();
    public DbSet<AcademicLevel> Levels => Set<AcademicLevel>();
    public DbSet<ProgramEnrollment> Enrollments => Set<ProgramEnrollment>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<CourseOffering> CourseOfferings => Set<CourseOffering>();
    public DbSet<Curriculum> Curricula => Set<Curriculum>();
    public DbSet<CurriculumCourse> CurriculumCourses => Set<CurriculumCourse>();
    public DbSet<LevelSemesterConfig> LevelSemesterConfigs => Set<LevelSemesterConfig>();
    public DbSet<CoursePrerequisite> CoursePrerequisites => Set<CoursePrerequisite>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<DocumentType> DocumentTypes => Set<DocumentType>();
    public DbSet<DocumentRecord> DocumentRecords => Set<DocumentRecord>();
    public DbSet<AdmissionApplication> AdmissionApplications => Set<AdmissionApplication>();
    public DbSet<SponsorOrganization> SponsorOrganizations => Set<SponsorOrganization>();
    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<LetterTemplate> LetterTemplates => Set<LetterTemplate>();


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.EntraObjectId).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Username).HasMaxLength(64);
            entity.Property(x => x.PasswordHash).HasMaxLength(1024);
            entity.Property(x => x.Email).HasMaxLength(256);
            entity.Property(x => x.DisplayName).HasMaxLength(256);
            entity.HasIndex(x => x.EntraObjectId).IsUnique();
            entity.HasIndex(x => x.Username).IsUnique().HasFilter("[Username] IS NOT NULL");
            entity.HasIndex(x => x.Email);
        });

        modelBuilder.Entity<AppRole>(entity =>
        {
            entity.ToTable("Roles");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(500);
            entity.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<AppPermission>(entity =>
        {
            entity.ToTable("Permissions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(500);
            entity.HasIndex(x => x.Code).IsUnique();
        });

        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.ToTable("UserRoles");
            entity.HasKey(x => new { x.UserId, x.RoleId });
            entity.HasOne(x => x.User).WithMany(x => x.UserRoles).HasForeignKey(x => x.UserId);
            entity.HasOne(x => x.Role).WithMany(x => x.UserRoles).HasForeignKey(x => x.RoleId);
        });

        modelBuilder.Entity<RolePermission>(entity =>
        {
            entity.ToTable("RolePermissions");
            entity.HasKey(x => new { x.RoleId, x.PermissionId });
            entity.HasOne(x => x.Role).WithMany(x => x.RolePermissions).HasForeignKey(x => x.RoleId);
            entity.HasOne(x => x.Permission).WithMany(x => x.RolePermissions).HasForeignKey(x => x.PermissionId);
        });

        modelBuilder.Entity<UserPermission>(entity =>
        {
            entity.ToTable("UserPermissions");
            entity.HasKey(x => new { x.UserId, x.PermissionId });
            entity.Property(x => x.Effect).HasConversion<string>().HasMaxLength(10).IsRequired();
            entity.Property(x => x.Reason).HasMaxLength(500);
            entity.HasOne(x => x.User).WithMany(x => x.UserPermissions).HasForeignKey(x => x.UserId);
            entity.HasOne(x => x.Permission).WithMany(x => x.UserPermissions).HasForeignKey(x => x.PermissionId);
        });

        modelBuilder.Entity<AcademicSession>(entity =>
        {
            entity.ToTable("AcademicSessions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            entity.Property(x => x.StartDate).IsRequired();
            entity.Property(x => x.EndDate).IsRequired();
            entity.Property(x => x.IsActive).IsRequired();
            entity.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<AcademicProgram>(entity =>
        {
            entity.ToTable("Programs");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Code).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.Property(x => x.Type).HasConversion<int>().IsRequired();
            entity.Property(x => x.DurationYears).IsRequired();
            entity.Property(x => x.MinJambScore).IsRequired();
            entity.Property(x => x.MaxAdmissions).IsRequired();
            entity.Property(x => x.RequiredJambSubjectsJson).HasColumnType("nvarchar(max)");
            entity.Property(x => x.RequiredOLevelSubjectsJson).HasColumnType("nvarchar(max)");
            entity.HasIndex(x => x.Code).IsUnique();

            entity.HasOne(x => x.Faculty)
                .WithMany(x => x.Programs)
                .HasForeignKey(x => x.FacultyId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Faculty>(entity =>
        {
            entity.ToTable("Faculties");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Label).HasMaxLength(100).IsRequired();
            entity.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<AcademicLevel>(entity =>
        {
            entity.ToTable("Levels");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            entity.HasOne(x => x.Program).WithMany(x => x.Levels).HasForeignKey(x => x.ProgramId);
            entity.HasIndex(x => new { x.ProgramId, x.Name }).IsUnique();
        });

        modelBuilder.Entity<ProgramEnrollment>(entity =>
        {
            entity.ToTable("Enrollments");
            entity.HasKey(x => x.Id);
            entity.HasOne(x => x.Program).WithMany(x => x.Enrollments).HasForeignKey(x => x.ProgramId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Level).WithMany(x => x.Enrollments).HasForeignKey(x => x.LevelId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.AcademicSession).WithMany().HasForeignKey(x => x.AcademicSessionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Curriculum).WithMany().HasForeignKey(x => x.CurriculumId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.UserId, x.AcademicSessionId }).IsUnique();
        });

        modelBuilder.Entity<Course>(entity =>
        {
            entity.ToTable("Courses");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Title).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.Property(x => x.CreditUnits).IsRequired();

            entity.HasIndex(x => x.Code).IsUnique();
        });

        modelBuilder.Entity<CourseOffering>(entity =>
        {
            entity.ToTable("CourseOfferings");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Semester).HasConversion<int>().IsRequired();

            entity.HasOne(x => x.Course)
                .WithMany(x => x.Offerings)
                .HasForeignKey(x => x.CourseId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Program)
                .WithMany()
                .HasForeignKey(x => x.ProgramId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Level)
                .WithMany()
                .HasForeignKey(x => x.LevelId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.AcademicSession)
                .WithMany()
                .HasForeignKey(x => x.AcademicSessionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Lecturer)
                .WithMany()
                .HasForeignKey(x => x.LecturerId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(x => new { x.CourseId, x.ProgramId, x.LevelId, x.AcademicSessionId, x.Semester }).IsUnique();
        });

        modelBuilder.Entity<Curriculum>(entity =>
        {
            entity.ToTable("Curricula");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.MinCreditUnitsForGraduation).IsRequired();
            entity.Property(x => x.Status).HasConversion<int>().IsRequired();
            entity.HasOne(x => x.Program).WithMany().HasForeignKey(x => x.ProgramId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.AdmissionSession).WithMany().HasForeignKey(x => x.AdmissionSessionId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LevelSemesterConfig>(entity =>
        {
            entity.ToTable("LevelSemesterConfigs");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Semester).HasConversion<int>().IsRequired();
            entity.HasOne(x => x.Level).WithMany(x => x.Semesters).HasForeignKey(x => x.LevelId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.LevelId, x.Semester }).IsUnique();
        });

        modelBuilder.Entity<CoursePrerequisite>(entity =>
        {
            entity.ToTable("CoursePrerequisites");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Type).HasConversion<int>().IsRequired();
            entity.HasOne(x => x.Course).WithMany().HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.PrerequisiteCourse).WithMany().HasForeignKey(x => x.PrerequisiteCourseId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.CourseId, x.PrerequisiteCourseId }).IsUnique();
        });

        modelBuilder.Entity<CurriculumCourse>(entity =>
        {
            entity.ToTable("CurriculumCourses");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Semester).HasConversion<int>().IsRequired();
            entity.Property(x => x.Category).HasConversion<int>().IsRequired();
            entity.HasOne(x => x.Curriculum).WithMany(x => x.Courses).HasForeignKey(x => x.CurriculumId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Level).WithMany().HasForeignKey(x => x.LevelId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Course).WithMany().HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.CurriculumId, x.LevelId, x.CourseId, x.Semester }).IsUnique();
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("AuditLogs");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Action).HasMaxLength(50).IsRequired();
            entity.Property(x => x.EntityName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.EntityId).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Changes).HasColumnType("nvarchar(max)");
            entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(x => x.Timestamp);
            entity.HasIndex(x => x.EntityName);
            entity.HasIndex(x => x.EntityId);
        });

        modelBuilder.Entity<DocumentType>(entity =>
        {
            entity.ToTable("DocumentTypes");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Code).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Category).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.HasIndex(x => x.Code).IsUnique();
        });

        modelBuilder.Entity<DocumentRecord>(entity =>
        {
            entity.ToTable("DocumentRecords");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FileName).HasMaxLength(500).IsRequired();
            entity.Property(x => x.FileUrl).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(x => x.RejectionReason).HasMaxLength(500);
            entity.HasOne(x => x.DocumentType).WithMany().HasForeignKey(x => x.DocumentTypeId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Faculty).WithMany().HasForeignKey(x => x.FacultyId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<AdmissionApplication>(entity =>
        {
            entity.ToTable("AdmissionApplications");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.StudentEmail).HasMaxLength(256).IsRequired();
            entity.Property(x => x.JambRegNumber).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();

            entity.HasOne(x => x.AcademicSession).WithMany().HasForeignKey(x => x.AcademicSessionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Faculty).WithMany().HasForeignKey(x => x.FacultyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.AcademicProgram).WithMany().HasForeignKey(x => x.AcademicProgramId).OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Documents).WithMany().UsingEntity("AdmissionApplicationDocuments");

            entity.HasIndex(x => new { x.StudentEmail, x.AcademicSessionId });
            entity.HasIndex(x => new { x.JambRegNumber, x.AcademicSessionId });
        });

        modelBuilder.Entity<SponsorOrganization>(entity =>
        {
            entity.ToTable("SponsorOrganizations");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Code).HasMaxLength(50).IsRequired();
            entity.HasIndex(x => x.Code).IsUnique();
        });

        modelBuilder.Entity<Subject>(entity =>
        {
            entity.ToTable("Subjects");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(130).IsRequired();
            entity.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<LetterTemplate>(entity =>
        {
            entity.ToTable("LetterTemplates");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.TemplateType).HasMaxLength(50).IsRequired();
            entity.Property(x => x.LogoBase64).HasColumnType("nvarchar(max)");
            entity.Property(x => x.SignatureBase64).HasColumnType("nvarchar(max)");
            entity.Property(x => x.SectionsJson).HasColumnType("nvarchar(max)");
            entity.HasIndex(x => x.TemplateType);
        });
    }
}

