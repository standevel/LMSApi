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
    public DbSet<CourseEnrollment> CourseEnrollments => Set<CourseEnrollment>();
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

    // --- New entities for international/direct entry/transfer/exchange support ---
    public DbSet<Country> Countries => Set<Country>();
    public DbSet<CreditTransferRule> CreditTransferRules => Set<CreditTransferRule>();
    public DbSet<GPAScaleConversion> GPAScaleConversions => Set<GPAScaleConversion>();
    public DbSet<CourseEquivalency> CourseEquivalencies => Set<CourseEquivalency>();
    public DbSet<ProgramCreditMapping> ProgramCreditMappings => Set<ProgramCreditMapping>();
    public DbSet<ProgramPrerequisite> ProgramPrerequisites => Set<ProgramPrerequisite>();
    public DbSet<GradingScale> GradingScales => Set<GradingScale>();
    public DbSet<DirectEntryGradeConfiguration> DirectEntryGradeConfigurations => Set<DirectEntryGradeConfiguration>();
    public DbSet<CredentialEvaluation> CredentialEvaluations => Set<CredentialEvaluation>();

    // Fee Management
    public DbSet<FeeCategory> FeeCategories => Set<FeeCategory>();
    public DbSet<FeeTemplate> FeeTemplates => Set<FeeTemplate>();
    public DbSet<FeeLineItem> FeeLineItems => Set<FeeLineItem>();
    public DbSet<FeeAssignment> FeeAssignments => Set<FeeAssignment>();
    public DbSet<StudentFeeRecord> StudentFeeRecords => Set<StudentFeeRecord>();
    public DbSet<LateFeeApplication> LateFeeApplications => Set<LateFeeApplication>();
    public DbSet<FeePayment> FeePayments => Set<FeePayment>();
    public DbSet<Scholarship> Scholarships => Set<Scholarship>();
    public DbSet<StudentScholarship> StudentScholarships => Set<StudentScholarship>();

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
     public DbSet<SystemRegistrationConfiguration> SystemRegistrationConfigurations => Set<SystemRegistrationConfiguration>();
     public DbSet<SystemParentPortalConfiguration> SystemParentPortalConfigurations => Set<SystemParentPortalConfiguration>();
     public DbSet<AssessmentCategory> AssessmentCategories => Set<AssessmentCategory>();
     public DbSet<Assessment> Assessments => Set<Assessment>();
     public DbSet<Grade> Grades => Set<Grade>();
     public DbSet<GradeApproval> GradeApprovals => Set<GradeApproval>();
     public DbSet<GradePublication> GradePublications => Set<GradePublication>();

// Communication System
      public DbSet<Announcement> Announcements => Set<Announcement>();
      public DbSet<AnnouncementAttachment> AnnouncementAttachments => Set<AnnouncementAttachment>();
      public DbSet<DiscussionThread> DiscussionThreads => Set<DiscussionThread>();
      public DbSet<DiscussionPost> DiscussionPosts => Set<DiscussionPost>();
      public DbSet<DiscussionPostAttachment> DiscussionPostAttachments => Set<DiscussionPostAttachment>();
      public DbSet<Notification> Notifications => Set<Notification>();
      public DbSet<Message> Messages => Set<Message>();
      public DbSet<MessageAttachment> MessageAttachments => Set<MessageAttachment>();

      // Student Self-Service (Phase 4)
      public DbSet<Waitlist> Waitlists => Set<Waitlist>();
      public DbSet<CourseSwapRequest> CourseSwapRequests => Set<CourseSwapRequest>();
      public DbSet<ScheduleAdjustmentRequest> ScheduleAdjustmentRequests => Set<ScheduleAdjustmentRequest>();
      public DbSet<PrerequisiteOverride> PrerequisiteOverrides => Set<PrerequisiteOverride>();
      public DbSet<ProgramSwitchRequest> ProgramSwitchRequests => Set<ProgramSwitchRequest>();
      public DbSet<CourseAdviserAssignment> CourseAdviserAssignments => Set<CourseAdviserAssignment>();
      public DbSet<AdvisingNote> AdvisingNotes => Set<AdvisingNote>();
      public DbSet<RegistrationVerification> RegistrationVerifications => Set<RegistrationVerification>();

      // Assessment Engine (Phase 2)
      public DbSet<Quiz> Quizzes => Set<Quiz>();
      public DbSet<QuizQuestion> QuizQuestions => Set<QuizQuestion>();
      public DbSet<QuestionOption> QuestionOptions => Set<QuestionOption>();
      public DbSet<QuizAttempt> QuizAttempts => Set<QuizAttempt>();
      public DbSet<QuizAnswer> QuizAnswers => Set<QuizAnswer>();
      public DbSet<QuestionBank> QuestionBanks => Set<QuestionBank>();
      public DbSet<ExamProctoringSession> ExamProctoringSessions => Set<ExamProctoringSession>();
      public DbSet<ProctoringViolation> ProctoringViolations => Set<ProctoringViolation>();
      
      // Phase 1+2: Enhanced quiz management entities
      public DbSet<QuizSetting> QuizSettings => Set<QuizSetting>();
      public DbSet<QuestionBankItem> QuestionBankItems => Set<QuestionBankItem>();
      public DbSet<QuestionBankOption> QuestionBankOptions => Set<QuestionBankOption>();
      public DbSet<QuizTimeExtension> QuizTimeExtensions => Set<QuizTimeExtension>();
      public DbSet<QuizFeedback> QuizFeedbacks => Set<QuizFeedback>();
      public DbSet<CbtHall> CbtHalls => Set<CbtHall>();
      public DbSet<Assignment> Assignments => Set<Assignment>();
      public DbSet<AssignmentGroup> AssignmentGroups => Set<AssignmentGroup>();
      public DbSet<AssignmentSubmission> AssignmentSubmissions => Set<AssignmentSubmission>();
      public DbSet<SubmissionGrade> SubmissionGrades => Set<SubmissionGrade>();

     // Reporting & Analytics (Phase 3)
     public DbSet<AcademicStanding> AcademicStandings => Set<AcademicStanding>();
     public DbSet<DegreeAudit> DegreeAudits => Set<DegreeAudit>();
     public DbSet<DegreeAuditRequirement> DegreeAuditRequirements => Set<DegreeAuditRequirement>();
     public DbSet<DegreeRequirement> DegreeRequirements => Set<DegreeRequirement>();
     public DbSet<DegreeRequirementCourse> DegreeRequirementCourses => Set<DegreeRequirementCourse>();
public DbSet<TranscriptRequest> TranscriptRequests => Set<TranscriptRequest>();
      public DbSet<ReportCache> ReportCaches => Set<ReportCache>();

      // ===== Phase 5: Advanced Features =====
      public DbSet<FamilyCommunicationPreference> FamilyCommunicationPreferences => Set<FamilyCommunicationPreference>();
      public DbSet<WebhookSubscription> WebhookSubscriptions => Set<WebhookSubscription>();
      public DbSet<WebhookDeliveryLog> WebhookDeliveryLogs => Set<WebhookDeliveryLog>();
      public DbSet<BulkOperation> BulkOperations => Set<BulkOperation>();
      public DbSet<ApiRateLimit> ApiRateLimits => Set<ApiRateLimit>();
      public DbSet<ParentGuardian> ParentGuardians => Set<ParentGuardian>();
      public DbSet<ParentStudentLink> ParentStudentLinks => Set<ParentStudentLink>();
      public DbSet<PushSubscription> PushSubscriptions => Set<PushSubscription>();
      public DbSet<ClassterResultUpload> ClassterResultUploads => Set<ClassterResultUpload>();
      public DbSet<ClassterResultUploadRow> ClassterResultUploadRows => Set<ClassterResultUploadRow>();

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

            entity.HasOne(x => x.Department)
                .WithMany()
                .HasForeignKey(x => x.DepartmentId)
                .OnDelete(DeleteBehavior.SetNull);
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

            entity.HasOne(x => x.Department)
                .WithMany(x => x.Programs)
                .HasForeignKey(x => x.DepartmentId)
                .OnDelete(DeleteBehavior.Cascade);
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

            entity.HasOne(x => x.Head)
                .WithMany()
                .HasForeignKey(x => x.HeadId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Faculty>(entity =>
        {
            entity.ToTable("Faculties");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Label).HasMaxLength(100).IsRequired();
            entity.HasIndex(x => x.Name).IsUnique();

            entity.HasOne(x => x.Dean)
                .WithMany()
                .HasForeignKey(x => x.DeanId)
                .OnDelete(DeleteBehavior.SetNull);
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

        modelBuilder.Entity<CourseEnrollment>(entity =>
        {
            entity.ToTable("CourseEnrollments");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
            entity.HasIndex(x => new { x.StudentId, x.CourseOfferingId }).IsUnique();
            entity.HasIndex(x => new { x.StudentId, x.Status });
            entity.HasOne(x => x.Student).WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.CourseOffering).WithMany().HasForeignKey(x => x.CourseOfferingId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.UpdatedBy).WithMany().HasForeignKey(x => x.UpdatedById).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Course>(entity =>
        {
            entity.ToTable("Courses");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Title).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.Property(x => x.CreditUnits).IsRequired();
            entity.Property(x => x.Semester).HasConversion<int>();
            entity.Property(x => x.LectureHours).HasColumnType("int");
            entity.Property(x => x.PracticalHours).HasColumnType("int");

            entity.HasIndex(x => new { x.ProgramId, x.Code }).IsUnique();
            entity.HasOne(x => x.Program)
                .WithMany(x => x.Courses)
                .HasForeignKey(x => x.ProgramId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Level)
                .WithMany()
                .HasForeignKey(x => x.LevelId)
                .OnDelete(DeleteBehavior.Restrict);
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

            entity.HasOne(x => x.Curriculum)
                .WithMany()
                .HasForeignKey(x => x.CurriculumId);

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
            entity.Property(x => x.HttpMethod).HasMaxLength(10);
            entity.Property(x => x.Path).HasMaxLength(1024);
            entity.Property(x => x.QueryString).HasMaxLength(2048);
            entity.Property(x => x.IpAddress).HasMaxLength(64);
            entity.Property(x => x.UserAgent).HasMaxLength(512);
            entity.Property(x => x.CorrelationId).HasMaxLength(128);
            entity.Property(x => x.RequestContentType).HasMaxLength(256);
            entity.Property(x => x.RequestBodyJson).HasColumnType("nvarchar(max)");
            entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(x => x.Timestamp);
            entity.HasIndex(x => x.EntityName);
            entity.HasIndex(x => x.EntityId);
            entity.HasIndex(x => x.CorrelationId);
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
            entity.Property(x => x.ApplicantType).HasConversion<int>().IsRequired();
            entity.Property(x => x.PreviousCGPA).HasPrecision(18, 2);
            entity.Property(x => x.EnglishProficiencyType).HasConversion<int>();
            entity.Property(x => x.EnglishProficiencyScore).HasMaxLength(20);
            entity.Property(x => x.Nationality).HasMaxLength(100);
            entity.Property(x => x.PassportNumber).HasMaxLength(50);
            entity.Property(x => x.DateOfBirth);
            entity.Property(x => x.EmergencyContactName).HasMaxLength(200);
            entity.Property(x => x.EmergencyContactPhone).HasMaxLength(30);
            entity.Property(x => x.EmergencyContactEmail).HasMaxLength(256);
            entity.Property(x => x.PreviousInstitutionName).HasMaxLength(200);
            entity.Property(x => x.PreviousInstitutionCountry).HasMaxLength(100);
            entity.Property(x => x.VisaRequired);
            entity.Property(x => x.VisaApplicationNumber).HasMaxLength(100);
            entity.Property(x => x.FinancialProofProvided);
            entity.Property(x => x.FinancialProofAmount).HasColumnType("decimal(18,2)");
            entity.Property(x => x.FinancialProofCurrency).HasMaxLength(10);
            entity.Property(x => x.ConvertedCGPA).HasColumnType("decimal(18,2)");
            entity.Property(x => x.CGPAScaleMax).HasColumnType("decimal(4,2)");
            entity.Property(x => x.CGPAScaleMin).HasColumnType("decimal(4,2)");
            entity.Property(x => x.TransferableCredits).HasColumnType("decimal(18,2)");
            entity.Property(x => x.DirectEntryPoints).HasColumnType("decimal(18,2)");
            
            entity.HasOne(x => x.AcademicSession).WithMany().HasForeignKey(x => x.AcademicSessionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Faculty).WithMany().HasForeignKey(x => x.FacultyId).OnDelete(DeleteBehavior.Restrict);
 
            entity.HasOne(x => x.AcademicProgram).WithMany().HasForeignKey(x => x.AcademicProgramId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.StartingLevel).WithMany().HasForeignKey(x => x.StartingLevelId).OnDelete(DeleteBehavior.Restrict);

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
            entity.Property(x => x.ExchangeRate).HasColumnType("decimal(18,6)");
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
            entity.Property(x => x.ScholarshipDiscount).HasColumnType("decimal(18,2)");
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

        modelBuilder.Entity<Scholarship>(entity =>
        {
            entity.ToTable("Scholarships");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.Property(x => x.Type).HasConversion<int>().IsRequired();
            entity.Property(x => x.CoverageFlags).HasConversion<int>().IsRequired();
            entity.Property(x => x.PercentageCovered).HasColumnType("decimal(5,2)").IsRequired();

            entity.HasOne(x => x.SponsorOrganization).WithMany().HasForeignKey(x => x.SponsorOrganizationId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<StudentScholarship>(entity =>
        {
            entity.ToTable("StudentScholarships");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CalculatedAmount).HasColumnType("decimal(18,2)").IsRequired();

            entity.HasOne(x => x.Student).WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Scholarship).WithMany(x => x.StudentScholarships).HasForeignKey(x => x.ScholarshipId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Session).WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => new { x.StudentId, x.ScholarshipId, x.SessionId }).IsUnique();
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
            entity.Property(x => x.OnlineMeetingId).HasMaxLength(500);
            entity.Property(x => x.OnlineMeetingJoinUrl).HasMaxLength(2000);

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

        modelBuilder.Entity<SystemGradingConfiguration>(entity =>
        {
            entity.ToTable("SystemGradingConfigurations");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.DefaultGradingStyle).HasConversion<string>().IsRequired();
            entity.Property(x => x.DefaultExamPercentage).HasPrecision(5, 2);
            entity.Property(x => x.DefaultCA1Weight).HasPrecision(5, 2);
            entity.Property(x => x.DefaultCA2Weight).HasPrecision(5, 2);
            entity.Property(x => x.DefaultCA3Weight).HasPrecision(5, 2);
            entity.Property(x => x.DefaultExamWeight).HasPrecision(5, 2);
            entity.Property(x => x.GpaScale).HasPrecision(3, 2);
        });

        modelBuilder.Entity<SystemRegistrationConfiguration>(entity =>
        {
            entity.ToTable("SystemRegistrationConfigurations");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Strategy).HasMaxLength(20).IsRequired();
            entity.Property(x => x.EnforceMinCredits).IsRequired();
        });

        modelBuilder.Entity<SystemParentPortalConfiguration>(entity =>
        {
            entity.ToTable("SystemParentPortalConfigurations");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.AutoCreateGuardianAccountsOnStudentCreation).IsRequired();
            entity.Property(x => x.SendGuardianInvitationEmail).IsRequired();
            entity.Property(x => x.DefaultRelationship).HasMaxLength(100).IsRequired();
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

        modelBuilder.Entity<ClassterResultUpload>(entity =>
        {
            entity.ToTable("ClassterResultUploads");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FileName).HasMaxLength(260).IsRequired();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.Property(x => x.TotalRows).IsRequired();
            entity.Property(x => x.ProcessedRows).IsRequired();
            entity.Property(x => x.SuccessfulRows).IsRequired();
            entity.Property(x => x.FailedRows).IsRequired();
            entity.Property(x => x.CreatedAt).IsRequired();
            entity.Property(x => x.UpdatedAt).IsRequired();
            entity.HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.UploadId).IsUnique();
            entity.HasIndex(x => x.AcademicSessionId);
            entity.HasIndex(x => x.CourseId);
            entity.HasIndex(x => x.CreatedById);
        });

        modelBuilder.Entity<ClassterResultUploadRow>(entity =>
        {
            entity.ToTable("ClassterResultUploadRows");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ExternalStudentId).HasMaxLength(128);
            entity.Property(x => x.StudentName).HasMaxLength(256);
            entity.Property(x => x.AssessmentType).HasMaxLength(100);
            entity.Property(x => x.MarksObtained).HasPrecision(9, 4);
            entity.Property(x => x.Fingerprint).HasMaxLength(64);
            entity.Property(x => x.MappingStatus).HasMaxLength(50).IsRequired();
            entity.Property(x => x.MappingReason).HasMaxLength(512);
            entity.Property(x => x.RawPayload).HasColumnType("nvarchar(max)");
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.Property(x => x.UpdatedAtUtc).IsRequired();
            entity.HasOne(x => x.Upload).WithMany(x => x.Rows).HasForeignKey(x => x.UploadId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => x.UploadId);
            entity.HasIndex(x => new { x.UploadId, x.Fingerprint }).IsUnique();
            entity.HasIndex(x => new { x.UploadId, x.RowNumber }).IsUnique();
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
            entity.Property(x => x.EntraObjectId).HasMaxLength(100);
            entity.Property(x => x.OfficialEmail).HasMaxLength(256).IsRequired();
            entity.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.LastName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.MiddleName).HasMaxLength(100);
            entity.Property(x => x.PersonalEmail).HasMaxLength(256).IsRequired();
            entity.Property(x => x.Phone).HasMaxLength(255).IsRequired();
            entity.Property(x => x.EmergencyContactPhone).HasMaxLength(255);
            entity.Property(x => x.EmergencyContactEmail).HasMaxLength(256);
            entity.Property(x => x.EmergencyContactName).HasMaxLength(200);
            entity.Property(x => x.StudentNumber).HasMaxLength(50); // Nullable - assigned by Registrar after admission
            entity.Property(x => x.JambRegistrationNumber).HasMaxLength(50);
            entity.Property(x => x.JambScore).HasColumnType("int");
            entity.Property(x => x.Status).HasConversion<int>().IsRequired();
            
            entity.HasOne(x => x.AdmissionApplication)
                .WithOne(x => x.Student)
                .HasForeignKey<Student>(x => x.AdmissionApplicationId)
                .IsRequired(false)
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
            
            entity.HasIndex(x => x.EntraObjectId).IsUnique().HasFilter("[EntraObjectId] IS NOT NULL");
            entity.HasIndex(x => x.OfficialEmail).IsUnique();
            entity.HasIndex(x => x.StudentNumber).IsUnique().HasFilter("[StudentNumber] IS NOT NULL");
            entity.HasIndex(x => x.AdmissionApplicationId).IsUnique().HasFilter("[AdmissionApplicationId] IS NOT NULL");
            entity.HasIndex(x => x.EmergencyContactEmail).HasFilter("[EmergencyContactEmail] IS NOT NULL");
            entity.HasIndex(x => x.EmergencyContactName).HasFilter("[EmergencyContactName] IS NOT NULL");
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.LevelId);
        });

        modelBuilder.Entity<CourseAdviserAssignment>(entity =>
        {
            entity.ToTable("CourseAdviserAssignments");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Status).HasMaxLength(32).IsRequired();
            entity.Property(x => x.Source).HasMaxLength(32).IsRequired();
            entity.Property(x => x.Note).HasMaxLength(500);

            entity.HasOne(x => x.Student)
                .WithMany()
                .HasForeignKey(x => x.StudentId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Adviser)
                .WithMany()
                .HasForeignKey(x => x.AdviserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.AssignedBy)
                .WithMany()
                .HasForeignKey(x => x.AssignedById)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => new { x.StudentId, x.Status })
                .IsUnique()
                .HasFilter("[Status] = 'Active'");
            entity.HasIndex(x => new { x.AdviserId, x.Status });
            entity.HasIndex(x => x.Source);
        });

        modelBuilder.Entity<AdvisingNote>(entity =>
        {
            entity.ToTable("AdvisingNotes");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Body).HasMaxLength(4000).IsRequired();

            entity.HasOne(x => x.Student)
                .WithMany()
                .HasForeignKey(x => x.StudentId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Adviser)
                .WithMany()
                .HasForeignKey(x => x.AdviserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => x.StudentId);
            entity.HasIndex(x => x.AdviserId);
            entity.HasIndex(x => x.FollowUpDateUtc);
        });

        modelBuilder.Entity<RegistrationVerification>(entity =>
        {
            entity.ToTable("RegistrationVerifications");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Status).HasMaxLength(32).IsRequired();
            entity.Property(x => x.Remarks).HasMaxLength(1000);
            entity.Property(x => x.UnlockReason).HasMaxLength(1000);

            entity.HasOne(x => x.Student)
                .WithMany()
                .HasForeignKey(x => x.StudentId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.AcademicSession)
                .WithMany()
                .HasForeignKey(x => x.AcademicSessionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.VerifiedByAdviser)
                .WithMany()
                .HasForeignKey(x => x.VerifiedByAdviserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.UnlockedBy)
                .WithMany()
                .HasForeignKey(x => x.UnlockedById)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasIndex(x => new { x.StudentId, x.AcademicSessionId, x.Status })
                .IsUnique()
                .HasFilter("[Status] = 'Verified'");
            entity.HasIndex(x => x.VerifiedByAdviserId);
        });


        // --- New entity configurations ---
        modelBuilder.Entity<Country>(entity =>
        {
            entity.ToTable("Countries");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).HasMaxLength(10).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.DisplayName).HasMaxLength(200);
            entity.Property(x => x.Region).HasConversion<string>().HasMaxLength(50);
            entity.Property(x => x.CallingCode).HasMaxLength(10);
            entity.Property(x => x.IsActive).IsRequired();
            entity.Property(x => x.DisplayOrder).IsRequired();
            entity.HasIndex(x => x.Code).IsUnique();
        });

        modelBuilder.Entity<CreditTransferRule>(entity =>
        {
            entity.ToTable("CreditTransferRules");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.SourceCountryCode).HasMaxLength(10);
            entity.Property(x => x.CreditsPerYear).HasColumnType("decimal(5,2)").IsRequired();
            entity.Property(x => x.MaxTransferablePercentage).HasColumnType("decimal(5,2)").IsRequired();
            entity.Property(x => x.MaxTransferableCredits).IsRequired();
            entity.Property(x => x.MinCGPA).HasColumnType("decimal(4,2)").IsRequired();
            entity.Property(x => x.IsActive).IsRequired();
            entity.Property(x => x.CreatedAt).IsRequired();

            entity.HasOne(x => x.Program)
                .WithMany()
                .HasForeignKey(x => x.ProgramId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GPAScaleConversion>(entity =>
        {
            entity.ToTable("GPAScaleConversions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CountryCode).HasMaxLength(10).IsRequired();
            entity.Property(x => x.ScaleName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.ScaleMax).HasColumnType("decimal(4,2)").IsRequired();
            entity.Property(x => x.ScaleMin).HasColumnType("decimal(4,2)").IsRequired();
            entity.Property(x => x.EquivalentCGPA).HasColumnType("decimal(4,2)").IsRequired();
            entity.Property(x => x.MinPassingScore).HasColumnType("decimal(5,2)").IsRequired();
            entity.Property(x => x.IsActive).IsRequired();
            entity.Property(x => x.CreatedAt).IsRequired();
        });

        modelBuilder.Entity<CourseEquivalency>(entity =>
        {
            entity.ToTable("CourseEquivalencies");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.SourceInstitution).HasMaxLength(300).IsRequired();
            entity.Property(x => x.SourceCourseCode).HasMaxLength(20).IsRequired();
            entity.Property(x => x.SourceCourseName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.SourceCredits).HasColumnType("decimal(5,2)").IsRequired();
            entity.Property(x => x.TargetCredits).HasColumnType("decimal(5,2)").IsRequired();
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.Property(x => x.MappingNotes).HasMaxLength(2000);
            entity.Property(x => x.IsActive).IsRequired();
            entity.Property(x => x.CreatedAt).IsRequired();

            entity.HasOne(x => x.TargetCourse)
                .WithMany()
                .HasForeignKey(x => x.TargetCourseId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ProgramCreditMapping>(entity =>
        {
            entity.ToTable("ProgramCreditMappings");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CreditsPerLevel).IsRequired();
            entity.Property(x => x.MaxTransferablePercentage).HasColumnType("decimal(5,2)").IsRequired();
            entity.Property(x => x.MaxTransferableCredits).IsRequired();
            entity.Property(x => x.MinCreditsAtLMS).IsRequired();
            entity.Property(x => x.IsActive).IsRequired();
            entity.Property(x => x.CreatedAt).IsRequired();

            entity.HasOne(x => x.Program)
                .WithMany()
                .HasForeignKey(x => x.ProgramId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProgramPrerequisite>(entity =>
        {
            entity.ToTable("ProgramPrerequisites");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.RequiredSubjectCode).HasMaxLength(20).IsRequired();
            entity.Property(x => x.RequiredSubjectName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.MinGrade).HasMaxLength(10).IsRequired();
            entity.Property(x => x.IsCore).IsRequired();
            entity.Property(x => x.IsActive).IsRequired();
            entity.Property(x => x.CreatedAt).IsRequired();

            entity.HasOne(x => x.Program)
                .WithMany()
                .HasForeignKey(x => x.ProgramId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GradingScale>(entity =>
        {
            entity.ToTable("GradingScales");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.CountryCode).HasMaxLength(10);
            entity.Property(x => x.QualificationType).HasMaxLength(50);
            entity.Property(x => x.GradesJson).HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(x => x.IsActive).IsRequired();
            entity.Property(x => x.CreatedAt).IsRequired();
        });

        modelBuilder.Entity<DirectEntryGradeConfiguration>(entity =>
        {
            entity.ToTable("DirectEntryGradeConfigurations");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.QualificationType).HasMaxLength(50).IsRequired();
            entity.Property(x => x.GradesJson).HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(x => x.IsActive).IsRequired();
            entity.Property(x => x.CreatedAt).IsRequired();

            entity.HasOne(x => x.GradingScale)
                .WithMany(x => x.DirectEntryConfigurations)
                .HasForeignKey(x => x.GradingScaleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CredentialEvaluation>(entity =>
        {
            entity.ToTable("CredentialEvaluations");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Evaluator).HasMaxLength(50).IsRequired();
            entity.Property(x => x.EvaluationReportId).HasMaxLength(200).IsRequired();
            entity.Property(x => x.EvaluationDate).IsRequired();
            entity.Property(x => x.EquivalencyDegree).HasMaxLength(100);
            entity.Property(x => x.EquivalencyMajor).HasMaxLength(200);
            entity.Property(x => x.EquivalencyGPA).HasColumnType("decimal(4,2)");
            entity.Property(x => x.EquivalencyScale).HasMaxLength(20);
            entity.Property(x => x.Notes).HasMaxLength(2000);
            entity.Property(x => x.ReportDocumentUrl).HasMaxLength(2000);
            entity.Property(x => x.ReportDocumentFileName).HasMaxLength(500);
            entity.Property(x => x.CreatedAt).IsRequired();

            entity.HasOne(x => x.Application)
                .WithMany(x => x.CredentialEvaluations)
                .HasForeignKey(x => x.ApplicationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ===== Reporting & Analytics (Phase 3) =====

        modelBuilder.Entity<AcademicStanding>(entity =>
        {
            entity.ToTable("AcademicStandings");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.StandingType).HasConversion<int>().IsRequired();
            entity.Property(x => x.CumulativeGpa).HasColumnType("decimal(4,2)").IsRequired();
            entity.Property(x => x.TotalCreditsAttempted).IsRequired();
            entity.Property(x => x.TotalCreditsEarned).IsRequired();
            entity.Property(x => x.Remarks).HasMaxLength(500);
            entity.Property(x => x.EffectiveDate).IsRequired();
            entity.Property(x => x.ExpiryDate);

            entity.HasOne(x => x.Student)
                .WithMany()
                .HasForeignKey(x => x.StudentId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.AcademicSession)
                .WithMany()
                .HasForeignKey(x => x.AcademicSessionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Creator)
                .WithMany()
                .HasForeignKey(x => x.CreatedById)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(x => new { x.StudentId, x.AcademicSessionId });
            entity.HasIndex(x => x.StandingType);
        });

        modelBuilder.Entity<DegreeAudit>(entity =>
        {
            entity.ToTable("DegreeAudits");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Status).HasConversion<int>().IsRequired();
            entity.Property(x => x.TotalCreditsRequired).IsRequired();
            entity.Property(x => x.TotalCreditsEarned).IsRequired();
            entity.Property(x => x.TotalCreditsInProgress).IsRequired();
            entity.Property(x => x.CumulativeGpa).HasColumnType("decimal(4,2)").IsRequired();
            entity.Property(x => x.Summary).HasMaxLength(2000);
            entity.Property(x => x.GeneratedAt).IsRequired();
            entity.Property(x => x.CompletedAt);

            entity.HasOne(x => x.Student)
                .WithMany()
                .HasForeignKey(x => x.StudentId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Program)
                .WithMany()
                .HasForeignKey(x => x.ProgramId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Template)
                .WithMany()
                .HasForeignKey(x => x.DegreeAuditTemplateId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.Creator)
                .WithMany()
                .HasForeignKey(x => x.CreatedById)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(x => new { x.StudentId, x.ProgramId });
            entity.HasIndex(x => x.Status);
        });

        modelBuilder.Entity<DegreeAuditRequirement>(entity =>
        {
            entity.ToTable("DegreeAuditRequirements");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Category).HasConversion<int>().IsRequired();
            entity.Property(x => x.RequirementName).HasMaxLength(200);
            entity.Property(x => x.CreditsRequired).IsRequired();
            entity.Property(x => x.CreditsEarned).IsRequired();
            entity.Property(x => x.IsCompleted).IsRequired();
            entity.Property(x => x.Remarks).HasMaxLength(500);

            entity.HasOne(x => x.DegreeAudit)
                .WithMany(x => x.Requirements)
                .HasForeignKey(x => x.DegreeAuditId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DegreeRequirement>(entity =>
        {
            entity.ToTable("DegreeRequirements");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Type).HasConversion<int>().IsRequired();
            entity.Property(x => x.CreditHoursRequired).IsRequired();
            entity.Property(x => x.MinGpaRequired).HasColumnType("decimal(4,2)");
            entity.Property(x => x.Description).HasMaxLength(2000);
            entity.Property(x => x.DisplayOrder).IsRequired();
            entity.Property(x => x.IsActive).IsRequired();
            entity.Property(x => x.CreatedAt).IsRequired();
            entity.Property(x => x.UpdatedAt);

            entity.HasOne(x => x.Program)
                .WithMany()
                .HasForeignKey(x => x.ProgramId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => new { x.ProgramId, x.Type, x.DisplayOrder });
        });

        modelBuilder.Entity<DegreeRequirementCourse>(entity =>
        {
            entity.ToTable("DegreeRequirementCourses");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.IsRequired).IsRequired();
            entity.Property(x => x.MinGrade).IsRequired();
            entity.Property(x => x.Remarks).HasMaxLength(500);

            entity.HasOne(x => x.DegreeRequirement)
                .WithMany(x => x.RequirementCourses)
                .HasForeignKey(x => x.DegreeRequirementId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Course)
                .WithMany()
                .HasForeignKey(x => x.CourseId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => new { x.DegreeRequirementId, x.CourseId }).IsUnique();
        });

        modelBuilder.Entity<TranscriptRequest>(entity =>
        {
            entity.ToTable("TranscriptRequests");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Status).HasConversion<int>().IsRequired();
            entity.Property(x => x.IsOfficial).IsRequired();
            entity.Property(x => x.DeliveryEmail).HasMaxLength(500);
            entity.Property(x => x.DeliveryMethod).HasMaxLength(50);
            entity.Property(x => x.Remarks).HasMaxLength(1000);
            entity.Property(x => x.FeeAmount).HasColumnType("decimal(18,2)");
            entity.Property(x => x.FeePaid).IsRequired();
            entity.Property(x => x.CompletedAt);
            entity.Property(x => x.DocumentUrl).HasMaxLength(2000);
            entity.Property(x => x.CreatedAt).IsRequired();
            entity.Property(x => x.UpdatedAt);

            entity.HasOne(x => x.Student)
                .WithMany()
                .HasForeignKey(x => x.StudentId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Creator)
                .WithMany()
                .HasForeignKey(x => x.CreatedById)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.Processor)
                .WithMany()
                .HasForeignKey(x => x.ProcessedBy)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.CreatedAt);
        });

        modelBuilder.Entity<ReportCache>(entity =>
        {
            entity.ToTable("ReportCaches");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ReportType).HasConversion<int>().IsRequired();
            entity.Property(x => x.CacheKey).HasMaxLength(500).IsRequired();
            entity.Property(x => x.CachedData).HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(x => x.CreatedAt).IsRequired();
            entity.Property(x => x.ExpiresAt).IsRequired();

            entity.HasIndex(x => x.CacheKey).IsUnique();
            entity.HasIndex(x => x.ReportType);
            entity.HasIndex(x => x.ExpiresAt);
        });

        // ===== Assessment Engine (Phase 2) =====

        modelBuilder.Entity<Quiz>(entity =>
        {
            entity.ToTable("Quizzes");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.Property(x => x.TimeLimitMinutes);
            entity.Property(x => x.PassThreshold).HasColumnType("decimal(5,2)");
            entity.Property(x => x.TargetProgramIdsJson).HasColumnType("nvarchar(max)").IsRequired();
            
            entity.HasOne(x => x.CourseOffering)
                .WithMany()
                .HasForeignKey(x => x.CourseOfferingId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasIndex(x => x.CourseOfferingId);
        });

        modelBuilder.Entity<QuizQuestion>(entity =>
        {
            entity.ToTable("QuizQuestions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.QuestionText).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.OrderIndex).IsRequired();
            entity.Property(x => x.QuestionType).HasMaxLength(50).IsRequired();
            
            entity.HasOne(x => x.Quiz)
                .WithMany(x => x.Questions)
                .HasForeignKey(x => x.QuizId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasIndex(x => x.QuizId);
        });

        modelBuilder.Entity<QuestionOption>(entity =>
        {
            entity.ToTable("QuestionOptions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.OptionText).HasMaxLength(1000).IsRequired();
            entity.Property(x => x.IsCorrectAnswer).IsRequired();
            
            entity.HasOne(x => x.QuizQuestion)
                .WithMany(x => x.Options)
                .HasForeignKey(x => x.QuizQuestionId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasIndex(x => x.QuizQuestionId);
        });

        modelBuilder.Entity<QuizAttempt>(entity =>
        {
            entity.ToTable("QuizAttempts");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.StartTime).IsRequired();
            entity.Property(x => x.TotalScore).HasColumnType("decimal(5,2)").IsRequired();
            
            entity.HasOne(x => x.Student)
                .WithMany()
                .HasForeignKey(x => x.StudentId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Quiz)
                .WithMany(x => x.Attempts)
                .HasForeignKey(x => x.QuizId)
                .OnDelete(DeleteBehavior.Restrict);
            
            entity.HasIndex(x => x.StudentId);
            entity.HasIndex(x => x.QuizId);
        });

        modelBuilder.Entity<QuizAnswer>(entity =>
        {
            entity.ToTable("QuizAnswers");
            entity.HasKey(x => x.Id);
            
            entity.HasOne(x => x.Attempt)
                .WithMany(x => x.Answers)
                .HasForeignKey(x => x.AttemptId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasOne(x => x.Question)
                .WithMany()
                .HasForeignKey(x => x.QuestionId)
                .OnDelete(DeleteBehavior.NoAction);
            
            entity.HasOne(x => x.SelectedOption)
                .WithMany()
                .HasForeignKey(x => x.SelectedOptionId)
                .OnDelete(DeleteBehavior.NoAction);
            
            entity.HasIndex(x => x.AttemptId);
            entity.HasIndex(x => x.QuestionId);
        });

        modelBuilder.Entity<QuestionBank>(entity =>
        {
            entity.ToTable("QuestionBanks");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(1000);
            
            entity.HasOne(x => x.CourseOffering)
                .WithMany()
                .HasForeignKey(x => x.CourseOfferingId)
                .OnDelete(DeleteBehavior.SetNull);
            
            entity.HasIndex(x => x.CourseOfferingId);
        });

        modelBuilder.Entity<ExamProctoringSession>(entity =>
        {
            entity.ToTable("ExamProctoringSessions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.Property(x => x.StartTimeUtc).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(50).IsRequired();
            entity.Property(x => x.ViolationCount).IsRequired();
            entity.Property(x => x.IntegrityScore).HasPrecision(18, 2);
            
            entity.HasOne(x => x.Student)
                .WithMany()
                .HasForeignKey(x => x.StudentId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Quiz)
                .WithMany()
                .HasForeignKey(x => x.QuizId)
                .OnDelete(DeleteBehavior.Restrict);
            
            entity.HasIndex(x => x.StudentId);
            entity.HasIndex(x => x.QuizId);
            entity.HasIndex(x => x.Status);
        });

        modelBuilder.Entity<ProctoringViolation>(entity =>
        {
            entity.ToTable("ProctoringViolations");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ViolationType).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Details).HasMaxLength(1000);
            entity.Property(x => x.ScreenshotUrl).HasMaxLength(1000);
            entity.Property(x => x.OccurredAtUtc).IsRequired();
            entity.Property(x => x.Severity).IsRequired();

            entity.HasOne(x => x.Session)
                .WithMany(x => x.Violations)
                .HasForeignKey(x => x.SessionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => x.SessionId);
            entity.HasIndex(x => x.OccurredAtUtc);
            entity.HasIndex(x => x.ViolationType);
        });

        // ===== Phase 1+2: Enhanced Quiz Management Entities =====

        modelBuilder.Entity<QuizSetting>(entity =>
        {
            entity.ToTable("QuizSettings");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
            entity.Property(x => x.FeedbackVisibility).HasMaxLength(20).IsRequired();
            entity.Property(x => x.PassThreshold).HasColumnType("decimal(5,2)");
            entity.Property(x => x.RestrictToAllowedIps).HasDefaultValue(false);
            entity.Property(x => x.AllowedIpRangesJson).HasMaxLength(4000);
            entity.Property(x => x.AllowedCbtHallIdsJson).HasMaxLength(4000);

            entity.HasOne(x => x.Quiz)
                .WithOne(x => x.Setting)
                .HasForeignKey<QuizSetting>(x => x.QuizId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.PoolQuestionBank)
                .WithMany()
                .HasForeignKey(x => x.PoolQuestionBankId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(x => x.QuizId).IsUnique();
        });

        modelBuilder.Entity<CbtHall>(entity =>
        {
            entity.ToTable("CbtHalls");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(150).IsRequired();
            entity.Property(x => x.Code).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.Property(x => x.IpRangesJson).HasMaxLength(4000).IsRequired();
            entity.Property(x => x.IsActive).HasDefaultValue(true);
            entity.HasIndex(x => x.Code).IsUnique();
            entity.HasIndex(x => x.IsActive);
        });

        modelBuilder.Entity<QuestionBankItem>(entity =>
        {
            entity.ToTable("QuestionBankItems");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.QuestionText).HasMaxLength(5000).IsRequired();
            entity.Property(x => x.QuestionType).HasMaxLength(50);
            entity.Property(x => x.Difficulty).HasMaxLength(20);
            entity.Property(x => x.Category).HasMaxLength(100);
            entity.Property(x => x.Tags).HasMaxLength(2000);
            entity.Property(x => x.Explanation).HasMaxLength(5000);
            entity.Property(x => x.Feedback).HasMaxLength(5000);
            entity.Property(x => x.AverageScore).HasColumnType("decimal(5,2)");

            entity.HasOne(x => x.QuestionBank)
                .WithMany(x => x.Items)
                .HasForeignKey(x => x.QuestionBankId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => x.QuestionBankId);
        });

        modelBuilder.Entity<QuestionBankOption>(entity =>
        {
            entity.ToTable("QuestionBankOptions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.OptionText).HasMaxLength(1000).IsRequired();

            entity.HasOne(x => x.QuestionBankItem)
                .WithMany(x => x.Options)
                .HasForeignKey(x => x.QuestionBankItemId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => x.QuestionBankItemId);
        });

        modelBuilder.Entity<QuizTimeExtension>(entity =>
        {
            entity.ToTable("QuizTimeExtensions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
            entity.Property(x => x.ApprovedBy).HasMaxLength(256).IsRequired();
            entity.Property(x => x.Reason).HasMaxLength(1000);
            entity.Property(x => x.DocumentationUrl).HasMaxLength(2000);

            entity.HasOne(x => x.Quiz)
                .WithMany(x => x.TimeExtensions)
                .HasForeignKey(x => x.QuizId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Student)
                .WithMany()
                .HasForeignKey(x => x.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => new { x.QuizId, x.StudentId }).IsUnique();
        });

        modelBuilder.Entity<QuizFeedback>(entity =>
        {
            entity.ToTable("QuizFeedbacks");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FeedbackText).HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(x => x.FeedbackType).HasMaxLength(50).IsRequired();
            entity.Property(x => x.GradingNotes).HasMaxLength(2000);
            entity.Property(x => x.ManualOverrideScore).HasColumnType("decimal(5,2)");

            entity.HasOne(x => x.Quiz)
                .WithMany(x => x.Feedbacks)
                .HasForeignKey(x => x.QuizId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.Question)
                .WithMany()
                .HasForeignKey(x => x.QuestionId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.Student)
                .WithMany()
                .HasForeignKey(x => x.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => x.QuizId);
            entity.HasIndex(x => x.StudentId);
        });

        // ===== Student Self-Service (Phase 4) =====

        modelBuilder.Entity<Waitlist>(entity =>
        {
            entity.ToTable("Waitlists");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Status).HasMaxLength(50).IsRequired();
            entity.Property(x => x.WaitlistRank).IsRequired();

            entity.HasOne(x => x.Student)
                .WithMany()
                .HasForeignKey(x => x.StudentId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.CourseOffering)
                .WithMany()
                .HasForeignKey(x => x.CourseOfferingId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => new { x.StudentId, x.CourseOfferingId, x.Status });
            entity.HasIndex(x => x.CourseOfferingId);
        });

        modelBuilder.Entity<CourseSwapRequest>(entity =>
        {
            entity.ToTable("CourseSwapRequests");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Status).HasMaxLength(50).IsRequired();

            entity.HasOne(x => x.Student)
                .WithMany()
                .HasForeignKey(x => x.StudentId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.CourseOfferingToDrop)
                .WithMany()
                .HasForeignKey(x => x.CourseOfferingToDropId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.CourseOfferingToAdd)
                .WithMany()
                .HasForeignKey(x => x.CourseOfferingToAddId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => x.StudentId);
            entity.HasIndex(x => x.Status);
        });

        modelBuilder.Entity<ScheduleAdjustmentRequest>(entity =>
        {
            entity.ToTable("ScheduleAdjustmentRequests");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Reason).HasMaxLength(500).IsRequired();
            entity.Property(x => x.DesiredSlotDetails).HasMaxLength(1000).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(50).IsRequired();

            entity.HasOne(x => x.Student)
                .WithMany()
                .HasForeignKey(x => x.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => x.StudentId);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.RequestedDate);
        });

        modelBuilder.Entity<PrerequisiteOverride>(entity =>
        {
            entity.ToTable("PrerequisiteOverrides");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Reason).HasMaxLength(500).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(50).IsRequired();

            entity.HasOne(x => x.Student)
                .WithMany()
                .HasForeignKey(x => x.StudentId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.CourseOffering)
                .WithMany()
                .HasForeignKey(x => x.CourseOfferingId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.ApprovedBy)
                .WithMany()
                .HasForeignKey(x => x.ApprovedById)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(x => x.StudentId);
            entity.HasIndex(x => x.CourseOfferingId);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.RequestedAtUtc);
        });

        modelBuilder.Entity<ProgramSwitchRequest>(entity =>
        {
            entity.ToTable("ProgramSwitchRequests");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
            entity.Property(x => x.Status).HasConversion<int>().IsRequired();
            entity.Property(x => x.JambDocumentUrl).HasMaxLength(500);
            entity.Property(x => x.JambDocumentFileName).HasMaxLength(255);
            entity.Property(x => x.HoDNotes).HasMaxLength(1000);
            entity.Property(x => x.DeanNotes).HasMaxLength(1000);
            entity.Property(x => x.AdminNotes).HasMaxLength(1000);
            entity.Property(x => x.RejectionReason).HasMaxLength(1000);

            entity.HasOne(x => x.Student)
                .WithMany()
                .HasForeignKey(x => x.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.FromProgram)
                .WithMany()
                .HasForeignKey(x => x.FromProgramId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ToProgram)
                .WithMany()
                .HasForeignKey(x => x.ToProgramId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.HoDReviewedBy)
                .WithMany()
                .HasForeignKey(x => x.HoDReviewedById)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.DeanReviewedBy)
                .WithMany()
                .HasForeignKey(x => x.DeanReviewedById)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.AdminCompletedBy)
                .WithMany()
                .HasForeignKey(x => x.AdminCompletedById)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.RejectedBy)
                .WithMany()
                .HasForeignKey(x => x.RejectedById)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasIndex(x => x.StudentId);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => new { x.StudentId, x.Status });
        });

        // ===== Phase 5: Advanced Features =====

        modelBuilder.Entity<FamilyCommunicationPreference>(entity =>
        {
            entity.ToTable("FamilyCommunicationPreferences");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.EmailNotifications).IsRequired();
            entity.Property(x => x.SmsNotifications).IsRequired();
            entity.Property(x => x.AllowMessageSending).IsRequired();
            entity.Property(x => x.ReceiveAcademicUpdates).IsRequired();
            entity.Property(x => x.ReceiveAttendanceAlerts).IsRequired();
            entity.Property(x => x.ReceiveGradeUpdates).IsRequired();

            entity.HasOne(x => x.ParentGuardian)
                .WithMany()
                .HasForeignKey(x => x.ParentGuardianId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => x.ParentGuardianId).IsUnique();
        });

        modelBuilder.Entity<WebhookSubscription>(entity =>
        {
            entity.ToTable("WebhookSubscriptions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Url).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.Secret).HasMaxLength(500).IsRequired();
            entity.Property(x => x.EventTypes).HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(x => x.IsActive).IsRequired();
            entity.Property(x => x.RetryAttempts).IsRequired();
            entity.Property(x => x.TimeoutSeconds).IsRequired();

            entity.HasOne(x => x.CreatedBy)
                .WithMany()
                .HasForeignKey(x => x.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => x.IsActive);
            entity.HasIndex(x => x.CreatedById);
        });

        modelBuilder.Entity<WebhookDeliveryLog>(entity =>
        {
            entity.ToTable("WebhookDeliveryLogs");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.EventType).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Payload).HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(x => x.SentAtUtc).IsRequired();
            entity.Property(x => x.StatusCode).IsRequired();
            entity.Property(x => x.ResponseBody).HasColumnType("nvarchar(max)");
            entity.Property(x => x.ErrorMessage).HasMaxLength(2000);
            entity.Property(x => x.IsSuccess).IsRequired();
            entity.Property(x => x.AttemptNumber).IsRequired();

            entity.HasOne(x => x.WebhookSubscription)
                .WithMany(x => x.DeliveryLogs)
                .HasForeignKey(x => x.WebhookSubscriptionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => x.WebhookSubscriptionId);
            entity.HasIndex(x => x.SentAtUtc);
            entity.HasIndex(x => x.EventType);
        });

        modelBuilder.Entity<Message>(entity =>
        {
            entity.ToTable("Messages");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Content).HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(x => x.SentAt).IsRequired();
            entity.Property(x => x.IsRead).IsRequired();
            entity.Property(x => x.IsActive).IsRequired();

            // RecipientId uses Cascade (messages are soft-deleted when recipient is deleted)
            entity.HasOne(x => x.Recipient)
                .WithMany()
                .HasForeignKey(x => x.RecipientId)
                .OnDelete(DeleteBehavior.Cascade);

            // SenderId uses Restrict to avoid multiple cascade paths (SQL Server limitation)
            entity.HasOne(x => x.Sender)
                .WithMany()
                .HasForeignKey(x => x.SenderId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => x.SenderId);
            entity.HasIndex(x => x.RecipientId);
            entity.HasIndex(x => x.SentAt);
        });

        modelBuilder.Entity<MessageAttachment>(entity =>
        {
            entity.ToTable("MessageAttachments");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FileName).HasMaxLength(500).IsRequired();
            entity.Property(x => x.FileUrl).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.ContentType).HasMaxLength(200).IsRequired();
            entity.Property(x => x.FileSizeBytes).IsRequired();

            entity.HasOne(x => x.Message)
                .WithMany(x => x.Attachments)
                .HasForeignKey(x => x.MessageId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => x.MessageId);
        });

        modelBuilder.Entity<BulkOperation>(entity =>
        {
            entity.ToTable("BulkOperations");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.OperationType).HasMaxLength(100).IsRequired();
            entity.Property(x => x.FileName).HasMaxLength(500).IsRequired();
            entity.Property(x => x.FileUrl).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.ResultData).HasColumnType("nvarchar(max)");
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(50).IsRequired();
            entity.Property(x => x.ErrorMessage).HasMaxLength(2000);

            entity.HasOne(x => x.CreatedBy)
                .WithMany()
                .HasForeignKey(x => x.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.CreatedById);
            entity.HasIndex(x => x.CreatedAt);
        });

        modelBuilder.Entity<ApiRateLimit>(entity =>
        {
            entity.ToTable("ApiRateLimits");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ClientId).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Endpoint).HasMaxLength(500).IsRequired();
            entity.Property(x => x.Method).HasMaxLength(10).IsRequired();
            entity.Property(x => x.WindowStartUtc).IsRequired();
            entity.Property(x => x.RequestCount).IsRequired();
            entity.Property(x => x.Limit).IsRequired();
            entity.Property(x => x.Remaining).IsRequired();

            entity.HasIndex(x => new { x.ClientId, x.Endpoint, x.Method, x.WindowStartUtc }).IsUnique();
            entity.HasIndex(x => x.WindowStartUtc);
        });

        // ===== ParentGuardian and ParentStudentLink =====

        modelBuilder.Entity<ParentGuardian>(entity =>
        {
            entity.ToTable("ParentGuardians");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.LastName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.PhoneNumber).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(256).IsRequired();
            entity.Property(x => x.DateAddedUtc).IsRequired();

            entity.HasOne(x => x.AppUser)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => x.UserId).IsUnique();
            entity.HasIndex(x => x.Email);
        });

        modelBuilder.Entity<ParentStudentLink>(entity =>
        {
            entity.ToTable("ParentStudentLinks");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.RelationshipType).HasMaxLength(100).IsRequired();
            entity.Property(x => x.LinkedAtUtc).IsRequired();

            entity.HasOne(x => x.ParentGuardian)
                .WithMany()
                .HasForeignKey(x => x.ParentGuardianId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Student)
                .WithMany()
                .HasForeignKey(x => x.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => x.ParentGuardianId);
            entity.HasIndex(x => x.StudentId);
            entity.HasIndex(x => new { x.ParentGuardianId, x.StudentId }).IsUnique();
        });

        modelBuilder.Entity<PushSubscription>(entity =>
        {
            entity.ToTable("PushSubscriptions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Endpoint).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.P256dh).HasMaxLength(500).IsRequired();
            entity.Property(x => x.Auth).HasMaxLength(500).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();

            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => x.UserId);
            entity.HasIndex(x => x.Endpoint);
        });

        modelBuilder.Entity<Assignment>(entity =>
        {
            entity.ToTable("Assignments");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Description).HasColumnType("nvarchar(max)");
            entity.Property(x => x.MaxPoints).HasColumnType("decimal(5,2)").IsRequired();
            entity.Property(x => x.DueDate).IsRequired();
            entity.Property(x => x.CutoffDate);
            entity.Property(x => x.AllowedExtensions).HasMaxLength(500).IsRequired();
            entity.Property(x => x.MaxFileSizeMb).HasDefaultValue(50);
            entity.Property(x => x.IsGroupAssignment).HasDefaultValue(false);
            entity.Property(x => x.MaxGroupSize);
            entity.Property(x => x.ReleaseConditionsJson).HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(x => x.TargetProgramIdsJson).HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(x => x.CreatedAt).IsRequired();
            entity.Property(x => x.UpdatedAt).IsRequired();
            entity.Property(x => x.Version).IsConcurrencyToken();
            entity.Property(x => x.IsDeleted).IsRequired();
            entity.HasQueryFilter(x => !x.IsDeleted);

            entity.HasOne(x => x.Course)
                .WithMany()
                .HasForeignKey(x => x.CourseId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => x.CourseId);
            entity.HasIndex(x => x.DueDate);
        });

        modelBuilder.Entity<AssignmentSubmission>(entity =>
        {
            entity.ToTable("AssignmentSubmissions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(x => x.SubmissionMetadataJson).HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(x => x.DigitalReceipt).HasMaxLength(128);
            entity.Property(x => x.CreatedAt).IsRequired();
            entity.Property(x => x.UpdatedAt).IsRequired();
            entity.Property(x => x.Version).IsConcurrencyToken();
            entity.Property(x => x.IsDeleted).IsRequired();
            entity.HasQueryFilter(x => !x.IsDeleted);

            entity.HasOne(x => x.Assignment)
                .WithMany(x => x.Submissions)
                .HasForeignKey(x => x.AssignmentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => x.AssignmentId);
            entity.HasIndex(x => x.SubmitterId);
            entity.HasIndex(x => new { x.AssignmentId, x.SubmitterId }).IsUnique();
        });

        modelBuilder.Entity<SubmissionGrade>(entity =>
        {
            entity.ToTable("SubmissionGrades");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Score).HasColumnType("decimal(5,2)").IsRequired();
            entity.Property(x => x.FeedbackText).HasColumnType("nvarchar(max)");
            entity.Property(x => x.FeedbackMediaUrl).HasMaxLength(2000);
            entity.Property(x => x.RubricExecutionJson).HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(x => x.GradedAt).IsRequired();
            entity.Property(x => x.CreatedAt).IsRequired();
            entity.Property(x => x.UpdatedAt).IsRequired();
            entity.Property(x => x.Version).IsConcurrencyToken();
            entity.Property(x => x.IsDeleted).IsRequired();
            entity.HasQueryFilter(x => !x.IsDeleted);

            entity.HasOne(x => x.Submission)
                .WithOne(x => x.Grade)
                .HasForeignKey<SubmissionGrade>(x => x.SubmissionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => x.SubmissionId).IsUnique();
            entity.HasIndex(x => x.GraderId);
        });
        modelBuilder.Entity<AssignmentGroup>(entity =>
        {
            entity.ToTable("AssignmentGroups");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            entity.Property(x => x.MemberStudentIdsJson).HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(x => x.CreatedAt).IsRequired();
            entity.Property(x => x.UpdatedAt).IsRequired();
            entity.HasQueryFilter(x => !x.Assignment.IsDeleted);

            entity.HasOne(x => x.Assignment)
                .WithMany()
                .HasForeignKey(x => x.AssignmentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => x.AssignmentId);
        });
    }
}
