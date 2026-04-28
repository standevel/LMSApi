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
    public DbSet<Department> Departments => Set<Department>();
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
    public DbSet<Student> Students => Set<Student>();
    public DbSet<SponsorOrganization> SponsorOrganizations => Set<SponsorOrganization>();
    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<LetterTemplate> LetterTemplates => Set<LetterTemplate>();

    // Fee Management
    public DbSet<FeeCategory> FeeCategories => Set<FeeCategory>();
    public DbSet<FeeTemplate> FeeTemplates => Set<FeeTemplate>();
    public DbSet<FeeLineItem> FeeLineItems => Set<FeeLineItem>();
    public DbSet<FeeAssignment> FeeAssignments => Set<FeeAssignment>();
    public DbSet<StudentFeeRecord> StudentFeeRecords => Set<StudentFeeRecord>();
    public DbSet<LateFeeApplication> LateFeeApplications => Set<LateFeeApplication>();
    public DbSet<FeePayment> FeePayments => Set<FeePayment>();

    // Timetable Management
    public DbSet<LectureTimetableSlot> LectureTimetableSlots => Set<LectureTimetableSlot>();
    public DbSet<LectureSession> LectureSessions => Set<LectureSession>();
    public DbSet<LectureSessionLecturer> LectureSessionLecturers => Set<LectureSessionLecturer>();
    public DbSet<SessionMaterial> SessionMaterials => Set<SessionMaterial>();
    public DbSet<SessionExternalLink> SessionExternalLinks => Set<SessionExternalLink>();
    public DbSet<SessionAttendance> SessionAttendances => Set<SessionAttendance>();
    
    // Course Materials
    public DbSet<CourseMaterial> CourseMaterials => Set<CourseMaterial>();
    
    // Gradebook Management
    public DbSet<SystemGradingConfiguration> SystemGradingConfigurations => Set<SystemGradingConfiguration>();
    public DbSet<AssessmentCategory> AssessmentCategories => Set<AssessmentCategory>();
    public DbSet<Assessment> Assessments => Set<Assessment>();
    public DbSet<Grade> Grades => Set<Grade>();
    public DbSet<GradeApproval> GradeApprovals => Set<GradeApproval>();
    public DbSet<GradePublication> GradePublications => Set<GradePublication>();


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
            entity.Property(x => x.ExpiresUtc);
            entity.HasOne(x => x.User).WithMany(x => x.UserPermissions).HasForeignKey(x => x.UserId);
            entity.HasOne(x => x.Permission).WithMany(x => x.UserPermissions).HasForeignKey(x => x.PermissionId);
            entity.HasIndex(x => x.ExpiresUtc);
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

            entity.HasOne(x => x.Department)
                .WithMany(x => x.Programs)
                .HasForeignKey(x => x.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Department>(entity =>
        {
            entity.ToTable("Departments");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Code).HasMaxLength(20).IsRequired();
            entity.HasIndex(x => x.Name).IsUnique();
            entity.HasIndex(x => x.Code).IsUnique();

            entity.HasOne(x => x.Faculty)
                .WithMany(x => x.Departments)
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
            entity.Property(x => x.OfferExpiresAt);
            entity.Property(x => x.OfferAcceptedAt);
            entity.Property(x => x.AccountCreatedAt);

            entity.HasOne(x => x.AcademicSession).WithMany().HasForeignKey(x => x.AcademicSessionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Faculty).WithMany().HasForeignKey(x => x.FacultyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.AcademicProgram).WithMany().HasForeignKey(x => x.AcademicProgramId).OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Documents).WithMany().UsingEntity("AdmissionApplicationDocuments");

            entity.HasIndex(x => new { x.StudentEmail, x.AcademicSessionId });
            entity.HasIndex(x => new { x.JambRegNumber, x.AcademicSessionId });
            entity.HasIndex(x => new { x.Status, x.OfferAcceptedAt }); // For Registrar pending accounts query
            entity.HasIndex(x => x.EntraObjectId).HasFilter("[EntraObjectId] IS NOT NULL"); // For idempotency checks
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

        modelBuilder.Entity<LectureTimetableSlot>(entity =>
        {
            entity.ToTable("LectureTimetableSlots");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.DayOfWeek).HasConversion<int>().IsRequired();
            entity.Property(x => x.StartTime).IsRequired();
            entity.Property(x => x.EndTime).IsRequired();
            entity.Property(x => x.DurationMinutes).IsRequired();
            entity.Property(x => x.CreatedBy).IsRequired();
            entity.Property(x => x.CreatedByUserId).IsRequired();
            entity.Property(x => x.UpdatedBy).IsRequired(false);
            entity.Property(x => x.UpdatedByUserId).IsRequired(false);

            entity.HasOne(x => x.CourseOffering).WithMany().HasForeignKey(x => x.CourseOfferingId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Lecturer).WithMany().HasForeignKey(x => x.LecturerId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.Venue).WithMany().HasForeignKey(x => x.VenueId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.UpdatedByUser).WithMany().HasForeignKey(x => x.UpdatedByUserId).OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => x.CourseOfferingId);
            entity.HasIndex(x => x.CreatedByUserId);
            entity.HasIndex(x => x.LecturerId);
            entity.HasIndex(x => x.UpdatedByUserId);
            entity.HasIndex(x => x.VenueId);
        });

        // --- Fee Management ---

        modelBuilder.Entity<FeeCategory>(entity =>
        {
            entity.ToTable("FeeCategories");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(500);
            entity.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<FeeTemplate>(entity =>
        {
            entity.ToTable("FeeTemplates");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.Property(x => x.Scope).HasConversion<int>().IsRequired();
            entity.Property(x => x.LateFeeType).HasConversion<int>().IsRequired();
            entity.Property(x => x.LateFeeAmount).HasColumnType("decimal(18,2)");
            entity.Ignore(x => x.HasLateFee); // computed property

            entity.HasOne(x => x.Category).WithMany(x => x.Templates).HasForeignKey(x => x.FeeCategoryId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Session).WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Faculty).WithMany().HasForeignKey(x => x.FacultyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Program).WithMany().HasForeignKey(x => x.ProgramId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<FeeLineItem>(entity =>
        {
            entity.ToTable("FeeLineItems");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(500);
            entity.Property(x => x.Amount).HasColumnType("decimal(18,2)").IsRequired();
            entity.HasOne(x => x.FeeTemplate).WithMany(x => x.LineItems).HasForeignKey(x => x.FeeTemplateId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<FeeAssignment>(entity =>
        {
            entity.ToTable("FeeAssignments");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Scope).HasConversion<int>().IsRequired();
            entity.Property(x => x.AmountOverride).HasColumnType("decimal(18,2)");

            entity.HasOne(x => x.FeeTemplate).WithMany(x => x.Assignments).HasForeignKey(x => x.FeeTemplateId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Faculty).WithMany().HasForeignKey(x => x.FacultyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Program).WithMany().HasForeignKey(x => x.ProgramId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Student).WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Session).WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<StudentFeeRecord>(entity =>
        {
            entity.ToTable("StudentFeeRecords");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TotalAmount).HasColumnType("decimal(18,2)").IsRequired();
            entity.Property(x => x.AmountPaid).HasColumnType("decimal(18,2)");
            entity.Property(x => x.LateFeeTotal).HasColumnType("decimal(18,2)");
            entity.Property(x => x.Status).HasConversion<int>().IsRequired();
            entity.Ignore(x => x.Balance); // computed property

            entity.HasOne(x => x.Student).WithMany(x => x.FeeRecords).HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Session).WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => new { x.StudentId, x.SessionId }).IsUnique();
            entity.HasIndex(x => x.Status);
        });

        modelBuilder.Entity<LateFeeApplication>(entity =>
        {
            entity.ToTable("LateFeeApplications");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.AmountCharged).HasColumnType("decimal(18,2)").IsRequired();
            entity.Property(x => x.BaseRateUsed).HasColumnType("decimal(18,2)").IsRequired();
            entity.Property(x => x.FeeType).HasConversion<int>().IsRequired();
            entity.Property(x => x.AppliedBy).HasMaxLength(256);

            entity.HasOne(x => x.StudentFeeRecord).WithMany(x => x.LateFeeApplications).HasForeignKey(x => x.StudentFeeRecordId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.FeeTemplate).WithMany().HasForeignKey(x => x.FeeTemplateId).OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => new { x.StudentFeeRecordId, x.FeeTemplateId });
        });

        modelBuilder.Entity<FeePayment>(entity =>
        {
            entity.ToTable("FeePayments");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Amount).HasColumnType("decimal(18,2)").IsRequired();
            entity.Property(x => x.PaymentMethod).HasConversion<int>().IsRequired();
            entity.Property(x => x.Status).HasConversion<int>().IsRequired();
            entity.Property(x => x.ReferenceNumber).HasMaxLength(200);
            entity.Property(x => x.ReceiptUrl).HasMaxLength(2000);
            entity.Property(x => x.GatewayReference).HasMaxLength(200);
            entity.Property(x => x.GatewayCheckoutUrl).HasMaxLength(2000);
            entity.Property(x => x.RejectionReason).HasMaxLength(500);
            entity.Property(x => x.ConfirmedBy).HasMaxLength(256);

            entity.HasOne(x => x.StudentFeeRecord).WithMany(x => x.Payments).HasForeignKey(x => x.StudentFeeRecordId).OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => x.GatewayReference);
            entity.HasIndex(x => x.Status);
        });

        modelBuilder.Entity<LectureSession>(entity =>
        {
            entity.ToTable("LectureSessions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.SessionDate).IsRequired();
            entity.Property(x => x.StartTime).IsRequired();
            entity.Property(x => x.EndTime).IsRequired();
            entity.Property(x => x.Notes).HasMaxLength(2000);
            entity.Property(x => x.IsManuallyCreated).IsRequired();
            entity.Property(x => x.IsCompleted).IsRequired();
            entity.Property(x => x.CreatedAt).IsRequired();

            entity.HasOne(x => x.CourseOffering).WithMany().HasForeignKey(x => x.CourseOfferingId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.TimetableSlot).WithMany().HasForeignKey(x => x.TimetableSlotId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.Venue).WithMany().HasForeignKey(x => x.VenueId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => x.SessionDate);
            entity.HasIndex(x => x.CourseOfferingId);
            entity.HasIndex(x => x.VenueId);
            entity.HasIndex(x => x.TimetableSlotId);
            entity.HasIndex(x => x.IsCompleted);
        });

        modelBuilder.Entity<LectureSessionLecturer>(entity =>
        {
            entity.ToTable("LectureSessionLecturers");
            entity.HasKey(x => new { x.LectureSessionId, x.LecturerId });

            entity.HasOne(x => x.LectureSession).WithMany(x => x.SessionLecturers).HasForeignKey(x => x.LectureSessionId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Lecturer).WithMany().HasForeignKey(x => x.LecturerId).OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => x.LecturerId);
        });

        modelBuilder.Entity<SessionMaterial>(entity =>
        {
            entity.ToTable("SessionMaterials");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FileName).HasMaxLength(500).IsRequired();
            entity.Property(x => x.FileUrl).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.FileSizeBytes).IsRequired();
            entity.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
            entity.Property(x => x.UploadedAt).IsRequired();

            entity.HasOne(x => x.LectureSession).WithMany(x => x.Materials).HasForeignKey(x => x.LectureSessionId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.UploadedByUser).WithMany().HasForeignKey(x => x.UploadedBy).OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => x.LectureSessionId);
            entity.HasIndex(x => x.UploadedBy);
        });

        modelBuilder.Entity<SessionAttendance>(entity =>
        {
            entity.ToTable("SessionAttendances");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.IsPresent).IsRequired();
            entity.Property(x => x.RecordedAt).IsRequired();

            entity.HasOne(x => x.LectureSession).WithMany(x => x.Attendance).HasForeignKey(x => x.LectureSessionId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Student).WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.RecordedByUser).WithMany().HasForeignKey(x => x.RecordedBy).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.ModifiedByUser).WithMany().HasForeignKey(x => x.ModifiedBy).OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => new { x.LectureSessionId, x.StudentId }).IsUnique();
            entity.HasIndex(x => x.StudentId);
        });

        modelBuilder.Entity<SessionExternalLink>(entity =>
        {
            entity.ToTable("SessionExternalLinks");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Url).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(500);
            entity.Property(x => x.CreatedAt).IsRequired();

            entity.HasOne(x => x.LectureSession).WithMany(x => x.ExternalLinks).HasForeignKey(x => x.LectureSessionId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => x.LectureSessionId);
            entity.HasIndex(x => x.CreatedBy);
        });

        modelBuilder.Entity<CourseMaterial>(entity =>
        {
            entity.ToTable("CourseMaterials");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(500);
            entity.Property(x => x.FileUrl).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.FileType).HasMaxLength(100);
            entity.Property(x => x.UploadedAt).IsRequired();

            entity.HasOne(x => x.CourseOffering).WithMany().HasForeignKey(x => x.CourseOfferingId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.UploadedBy).WithMany().HasForeignKey(x => x.UploadedById).OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => x.CourseOfferingId);
            entity.HasIndex(x => x.UploadedById);
        });

        // Gradebook Management
        modelBuilder.Entity<SystemGradingConfiguration>(entity =>
        {
            entity.ToTable("SystemGradingConfigurations");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.DefaultGradingStyle).IsRequired();
            entity.Property(x => x.DefaultExamPercentage).HasPrecision(5, 2);
            entity.Property(x => x.DefaultCA1Weight).HasPrecision(5, 2);
            entity.Property(x => x.DefaultCA2Weight).HasPrecision(5, 2);
            entity.Property(x => x.DefaultCA3Weight).HasPrecision(5, 2);
            entity.Property(x => x.DefaultExamWeight).HasPrecision(5, 2);
        });

        modelBuilder.Entity<AssessmentCategory>(entity =>
        {
            entity.ToTable("AssessmentCategories");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CategoryName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Weight).HasPrecision(5, 2);
            entity.Property(x => x.MaxMarks).HasPrecision(5, 2);
            
            entity.HasOne(x => x.CourseOffering).WithMany().HasForeignKey(x => x.CourseOfferingId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => x.CourseOfferingId);
        });

        modelBuilder.Entity<Assessment>(entity =>
        {
            entity.ToTable("Assessments");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(500);
            entity.Property(x => x.MaxMarks).HasPrecision(5, 2);
            
            entity.HasOne(x => x.CourseOffering).WithMany().HasForeignKey(x => x.CourseOfferingId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.AssessmentCategory).WithMany().HasForeignKey(x => x.AssessmentCategoryId).OnDelete(DeleteBehavior.Restrict);
            
            entity.HasIndex(x => x.CourseOfferingId);
            entity.HasIndex(x => x.AssessmentCategoryId);
        });

        modelBuilder.Entity<Grade>(entity =>
        {
            entity.ToTable("Grades");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.MarksObtained).HasPrecision(5, 2);
            entity.Property(x => x.Remarks).HasMaxLength(500);
            
            entity.HasOne(x => x.Assessment).WithMany(x => x.Grades).HasForeignKey(x => x.AssessmentId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Student).WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.UpdatedBy).WithMany().HasForeignKey(x => x.UpdatedById).OnDelete(DeleteBehavior.Restrict);
            
            entity.HasIndex(x => new { x.AssessmentId, x.StudentId }).IsUnique();
            entity.HasIndex(x => x.StudentId);
        });

        modelBuilder.Entity<GradeApproval>(entity =>
        {
            entity.ToTable("GradeApprovals");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Comments).HasMaxLength(500);
            
            entity.HasOne(x => x.CourseOffering).WithMany().HasForeignKey(x => x.CourseOfferingId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.ApprovedBy).WithMany().HasForeignKey(x => x.ApprovedById).OnDelete(DeleteBehavior.Restrict);
            
            entity.HasIndex(x => new { x.CourseOfferingId, x.Level });
            entity.HasIndex(x => x.Status);
        });

        modelBuilder.Entity<GradePublication>(entity =>
        {
            entity.ToTable("GradePublications");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.PublicationNotes).HasMaxLength(500);
            
            entity.HasOne(x => x.CourseOffering).WithMany().HasForeignKey(x => x.CourseOfferingId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.PublishedBy).WithMany().HasForeignKey(x => x.PublishedById).OnDelete(DeleteBehavior.Restrict);
            
            entity.HasIndex(x => x.CourseOfferingId).IsUnique();
            entity.HasIndex(x => x.IsVisibleToStudents);
        });

        modelBuilder.Entity<Student>(entity =>
        {
            entity.ToTable("Students");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.EntraObjectId).HasMaxLength(100).IsRequired();
            entity.Property(x => x.OfficialEmail).HasMaxLength(256).IsRequired();
            entity.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.LastName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.MiddleName).HasMaxLength(100);
            entity.Property(x => x.PersonalEmail).HasMaxLength(256).IsRequired();
            entity.Property(x => x.Phone).HasMaxLength(20).IsRequired();
            entity.Property(x => x.StudentNumber).HasMaxLength(50); // Nullable - assigned by Registrar after admission
            entity.Property(x => x.Status).HasConversion<int>().IsRequired();
            
            entity.HasOne(x => x.AdmissionApplication)
                .WithOne(x => x.Student)
                .HasForeignKey<Student>(x => x.AdmissionApplicationId)
                .OnDelete(DeleteBehavior.Restrict);
            
            entity.HasOne(x => x.AcademicSession)
                .WithMany()
                .HasForeignKey(x => x.AcademicSessionId)
                .OnDelete(DeleteBehavior.Restrict);
            
            entity.HasOne(x => x.Faculty)
                .WithMany()
                .HasForeignKey(x => x.FacultyId)
                .OnDelete(DeleteBehavior.SetNull);
            
            entity.HasOne(x => x.AcademicProgram)
                .WithMany()
                .HasForeignKey(x => x.AcademicProgramId)
                .OnDelete(DeleteBehavior.SetNull);
            
            entity.HasOne(x => x.Level)
                .WithMany()
                .HasForeignKey(x => x.LevelId)
                .OnDelete(DeleteBehavior.Restrict);
            
            entity.HasIndex(x => x.EntraObjectId).IsUnique();
            entity.HasIndex(x => x.OfficialEmail).IsUnique();
            entity.HasIndex(x => x.StudentNumber).IsUnique().HasFilter("[StudentNumber] IS NOT NULL"); // Unique only when assigned
            entity.HasIndex(x => x.AdmissionApplicationId).IsUnique();
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.LevelId);
        });
    }
}
