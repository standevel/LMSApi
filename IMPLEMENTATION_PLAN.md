# Implementation Plan for LMS API - Addressing Identified Gaps

This plan outlines the implementation of missing features identified in LMS_PROJECT_ANALYSIS.md, structured into five phases. Each phase follows existing project patterns: FastEndpoints for API endpoints, ErrorOr for service results, Repository pattern for data access, and EF Core for entity modeling.

## Phase 1 (Weeks 1-3): Online Learning Platform - Communication System

**Goal**: Implement real-time communication features including announcements, messaging, and notifications.

### Entities to Create
- `Announcement` (Data/Entities/Announcement.cs)
- `DiscussionThread` (Data/Entities/DiscussionThread.cs)
- `DiscussionPost` (Data/Entities/DiscussionPost.cs)
- `Notification` (Data/Entities/Notification.cs)
- `Message` (Data/Entities/Message.cs)

### Endpoints to Implement
- `POST /api/announcements` (Create announcement)
- `GET /api/announcements` (List announcements with filtering)
- `GET /api/announcements/{id}` (Get announcement by ID)
- `PUT /api/announcements/{id}` (Update announcement)
- `DELETE /api/announcements/{id}` (Delete announcement)
- `POST /api/discussions` (Create discussion thread)
- `GET /api/discussions` (List discussion threads)
- `GET /api/discussions/{id}` (Get thread with posts)
- `POST /api/discussions/{id}/posts` (Add post to thread)
- `PUT /api/discussions/posts/{id}` (Update post)
- `DELETE /api/discussions/posts/{id}` (Delete post)
- `POST /api/messages` (Send direct message)
- `GET /api/messages` (Get user's messages)
- `POST /api/notifications` (Create notification)
- `GET /api/notifications` (Get user's notifications)
- `PUT /api/notifications/{id}/read` (Mark notification as read)

### Services to Add
- `AnnouncementService` (Services/AnnouncementService.cs) - Implements IAnnouncementService
- `DiscussionService` (Services/DiscussionService.cs) - Implements IDiscussionService
- `NotificationService` (Services/NotificationService.cs) - Implements INotificationService (extends existing Brevo email service)
- `MessageService` (Services/MessageService.cs) - Implements IMessageService

### Contracts/DTOs to Create
- In Contracts/CommunicationContracts.cs:
  - `AnnouncementDto`, `CreateAnnouncementRequest`, `UpdateAnnouncementRequest`
  - `DiscussionThreadDto`, `DiscussionPostDto`, `CreateDiscussionRequest`, `UpdateDiscussionRequest`
  - `MessageDto`, `CreateMessageRequest`
  - `NotificationDto`, `CreateNotificationRequest`, `UpdateNotificationRequest`

### Dependencies on Existing Code
- Extends `BaseService` for audit logging (like CourseService)
- Uses `IUserRepository` for sender/recipient resolution
- Uses `LmsDbContext` for new DbSets
- Integrates with existing `BrevoEmailService` for email notifications
- Depends on `IFileStorageService` for attachments in announcements/posts

## Phase 2 (Weeks 4-6): Assessment Engine - Quiz/Exam System

**Goal**: Implement online quiz and exam functionality with auto-grading, question banks, and proctoring.

### Entities to Create
- `Quiz` (Data/Entities/Quiz.cs)
- `QuizQuestion` (Data/Entities/QuizQuestion.cs)
- `QuizAttempt` (Data/Entities/QuizAttempt.cs)
- `QuestionBank` (Data/Entities/QuestionBank.cs)
- `ExamProctoringSession` (Data/Entities/ExamProctoringSession.cs)
- `QuestionOption` (Data/Entities/QuestionOption.cs) (for multiple choice)

### Endpoints to Implement
- `POST /api/quizzes` (Create quiz)
- `GET /api/quizzes` (List quizzes with filtering)
- `GET /api/quizzes/{id}` (Get quiz with questions)
- `PUT /api/quizzes/{id}` (Update quiz)
- `DELETE /api/quizzes/{id}` (Delete quiz)
- `POST /api/quizzes/{id}/questions` (Add question to quiz)
- `PUT /api/quizzes/questions/{id}` (Update question)
- `DELETE /api/quizzes/questions/{id}` (Delete question)
- `POST /api/quizzes/{id}/attempts` (Start quiz attempt)
- `PUT /api/quizzes/attempts/{id}/answers` (Submit answers)
- `GET /api/quizzes/attempts/{id}` (Get attempt results)
- `POST /api/question-banks` (Create question bank)
- `GET /api/question-banks` (List question banks)
- `POST /api/question-banks/{id}/questions` (Add question to bank)
- `POST /api/exams/{id}/proctoring` (Start proctoring session)
- `PUT /api/exams/proctoring/{id}/heartbeat` (Update proctoring heartbeat)

### Services to Add
- `QuizService` (Services/QuizService.cs) - Implements IQuizService
- `QuestionBankService` (Services/QuestionBankService.cs) - Implements IQuestionBankService
- `ProctoringService` (Services/ProctoringService.cs) - Implements IProctoringService

### Contracts/DTOs to Create
- In Contracts/AssessmentContracts.cs:
  - `QuizDto`, `CreateQuizRequest`, `UpdateQuizRequest`
  - `QuizQuestionDto`, `CreateQuizQuestionRequest`, `UpdateQuizQuestionRequest`
  - `QuizAttemptDto`, `QuizAnswerDto`, `QuizResultDto`
  - `QuestionBankDto`, `CreateQuestionBankRequest`
  - `ExamProctoringSessionDto`, `ProctoringHeartbeatRequest`

### Dependencies on Existing Code
- Uses `ICourseRepository` to associate quizzes with course offerings
- Uses `IUserRepository` for student and lecturer associations
- Extends audit logging for quiz creation/attempts
- Leverages existing `Grade` entity for storing quiz scores (may need extension)
- Integrates with `LectureSession` for timed exams during class periods

## Phase 3 (Weeks 7-9): Reporting & Analytics - GPA, Transcripts, Dashboards

**Goal**: Build comprehensive reporting capabilities including GPA calculation, transcript generation, and analytical dashboards.

### Entities to Create
- `AcademicStanding` (Data/Entities/AcademicStanding.cs)
- `DegreeAudit` (Data/Entities/DegreeAudit.cs)
- `DegreeRequirement` (Data/Entities/DegreeRequirement.cs)
- `TranscriptRequest` (Data/Entities/TranscriptRequest.cs)
- `ReportCache` (Data/Entities/ReportCache.cs) (for dashboard performance)

### Endpoints to Implement
- `GET /api/reports/gpa/{studentId}` (Get student GPA)
- `GET /api/reports/transcript/{studentId}` (Generate transcript)
- `POST /api/reports/transcript-requests` (Request official transcript)
- `GET /api/reports/transcript-requests` (List transcript requests)
- `GET /api/reports/degree-audit/{studentId}` (Get degree audit)
- `GET /api/reports/enrollment-analytics` (Get enrollment trends)
- `GET /api/reports/graduation-rates` (Get graduation statistics)
- `GET /api/reports/dashboard/summary` (Get dashboard summary data)
- `GET /api/reports/dashboard/faculty/{facultyId}` (Get faculty dashboard)
- `GET /api/reports/dashboard/department/{deptId}` (Get department dashboard)

### Services to Add
- `GpaCalculationService` (Services/GpaCalculationService.cs) - Implements IGpaCalculationService
- `TranscriptGenerationService` (Services/TranscriptGenerationService.cs) - Implements ITranscriptGenerationService
- `DegreeAuditService` (Services/DegreeAuditService.cs) - Implements IDegreeAuditService
- `AnalyticsService` (Services/AnalyticsService.cs) - Implements IAnalyticsService
- `ReportSchedulerService` (Services/ReportSchedulerService.cs) - For scheduled report generation

### Contracts/DTOs to Create
- In Contracts/ReportingContracts.cs:
  - `GpaDto`, `TranscriptDto`, `DegreeAuditDto`
  - `TranscriptRequestDto`, `CreateTranscriptRequestDto`
  - `EnrollmentAnalyticsDto`, `GraduationRatesDto`
  - `DashboardSummaryDto`, `FacultyDashboardDto`, `DepartmentDashboardDto`

### Dependencies on Existing Code
- Heavily depends on existing enrollment, grade, and course completion data
- Uses `IUserRepository` for student information
- Uses `ICourseRepository` and `IEnrollmentRepository` (to be created/reused) for academic history
- Extends existing `Assessment` entity for grade storage
- Utilizes existing `AcademicProgram` and `AcademicLevel` for degree requirements
- Integrates with `LectureSession` and `CourseOffering` for attendance-based calculations
- Depends on `DateTime` utilities for academic term calculations

## Phase 4 (Weeks 10-12): Student Self-Service - Registration, Scheduling

**Goal**: Enable students to manage their own enrollment, schedule adjustments, and academic planning.

### Entities to Create
- `Waitlist` (Data/Entities/Waitlist.cs)
- `CourseSwapRequest` (Data/Entities/CourseSwapRequest.cs)
- `ScheduleAdjustment` (Data/Entities/ScheduleAdjustment.cs)
- `PrerequisiteOverride` (Data/Entities/PrerequisiteOverride.cs) (admin approval)

### Endpoints to Implement
- `POST /api/self-service/register` (Register for course)
- `DELETE /api/self-service/register/{enrollmentId}` (Drop course)
- `POST /api/self-service/waitlist/{courseOfferingId}` (Join waitlist)
- `DELETE /api/self-service/waitlist/{id}` (Leave waitlist)
- `POST /api/self-service/swap` (Request course swap)
- `GET /api/self-service/swap-requests` (List swap requests)
- `PUT /api/self-service/swap-requests/{id}/approve` (Approve swap)
- `POST /api/self-service/prerequisite-override` (Request prerequisite override)
- `GET /api/self-service/schedule` (Get student's current schedule)
- `POST /api/self-service/schedule/adjust` (Request schedule adjustment)
- `GET /api/self-service/registration-history` (Get past registrations)

### Services to Add
- `RegistrationService` (Services/RegistrationService.cs) - Implements IRegistrationService
- `WaitlistService` (Services/WaitlistService.cs) - Implements IWaitlistService
- `ScheduleService` (Services/ScheduleService.cs) - Implements IScheduleService
- `PrerequisiteValidationService` (Services/PrerequisiteValidationService.cs) - Implements IPrerequisiteValidationService

### Contracts/DTOs to Create
- In Contracts/SelfServiceContracts.cs:
  - `CourseRegistrationDto`, `CreateRegistrationRequest`
  - `WaitlistDto`, `JoinWaitlistRequest`
  - `CourseSwapRequestDto`, `CreateCourseSwapRequest`
  - `ScheduleDto`, `ScheduleAdjustmentRequest`
  - `PrerequisiteOverrideDto`, `CreatePrerequisiteOverrideRequest`

### Dependencies on Existing Code
- Builds upon existing `ProgramEnrollment` entity (extend for waitlist/overrides)
- Uses `ICourseRepository` for course offering availability
- Uses `IUserRepository` for student information
- Integrates with existing enrollment validation logic
- Depends on `AcademicSession` and `Semester` for term-based restrictions
- Integrates with `FeeService` for tuition calculation on registration changes
- Uses existing `NotificationService` for enrollment confirmations/waitlist alerts

## Phase 5 (Weeks 13-14): Advanced Features - Parent Portal, Integrations, Infrastructure

**Goal**: Implement parent portal access, system integrations, and infrastructure improvements.

### Entities to Create
- `ParentGuardian` (Data/Entities/ParentGuardian.cs)
- `ParentStudentLink` (Data/Entities/ParentStudentLink.cs)
- `FamilyCommunicationPreference` (Data/Entities/FamilyCommunicationPreference.cs)
- `WebhookSubscription` (Data/Entities/WebhookSubscription.cs)
- `WebhookDeliveryLog` (Data/Entities/WebhookDeliveryLog.cs)
- `BulkOperation` (Data/Entities/BulkOperation.cs)
- `ApiRateLimit` (Data/Entities/ApiRateLimit.cs)

### Endpoints to Implement
- `GET /api/parents/{parentId}/students` (Get linked students)
- `GET /api/parents/students/{studentId}/progress` (Get student progress)
- `GET /api/parents/students/{studentId}/grades` (Get student grades)
- `POST /api/parents/students/{studentId}/messages` (Send message to student)
- `POST /api/webhooks/subscriptions` (Create webhook subscription)
- `GET /api/webhooks/subscriptions` (List webhook subscriptions)
- `DELETE /api/webhooks/subscriptions/{id}` (Delete webhook subscription)
- `POST /api/webhooks/subscriptions/{id}/test` (Test webhook)
- `POST /api/bulk-operations/users` (Bulk import/export users)
- `POST /api/bulk-operations/enrollments` (Bulk import/export enrollments)
- `GET /api/rate-limit/status` (Get current rate limit status)

### Services to Add
- `ParentPortalService` (Services/ParentPortalService.cs) - Implements IParentPortalService
- `IntegrationService` (Services/IntegrationService.cs) - Implements IIntegrationService
- `WebhookService` (Services/WebhookService.cs) - Implements IWebhookService
- `BulkOperationService` (Services/BulkOperationService.cs) - Implements IBulkOperationService
- `RateLimitingService` (Services/RateLimitingService.cs) - Implements IRateLimitingService
- `CacheService` (Services/CacheService.cs) - Implements ICacheService (using MemoryCache/Redis)

### Contracts/DTOs to Create
- In Contracts/AdvancedContracts.cs:
  - `ParentGuardianDto`, `CreateParentGuardianRequest`
  - `ParentStudentLinkDto`, `CreateParentStudentLinkRequest`
  - `FamilyCommunicationPreferenceDto`
  - `WebhookSubscriptionDto`, `CreateWebhookSubscriptionRequest`
  - `WebhookDeliveryLogDto`
  - `BulkOperationDto`, `CreateBulkOperationRequest`
  - `ApiRateLimitDto`

### Dependencies on Existing Code
- Extends `IUserRepository` for parent/guardian user management
- Uses existing `NotificationService` for parent communications (email/SMS)
- Integrates with `BrevoEmailService` and potentially SMS services
- Depends on existing audit logging for webhook operations
- Uses existing `LmsDbContext` for new entities
- Builds upon existing authentication system for parent portal access (separate client/roles)
- Integrates with existing `CourseService`, `RegistrationService`, etc. for data access
- Requires configuration extensions in `appsettings.json` for webhook URLs, API keys, cache settings
- May require infrastructure changes for background processing (Hangfire/Quartz) - to be evaluated

## Cross-Phase Dependencies and Notes

1. **Authentication & Authorization**: All phases assume the existing authentication system (Entra ID) is in place. New endpoints will use `[Authorize]` attributes with appropriate roles (Student, Lecturer, Parent, Admin).

2. **Database Migrations**: Each phase will require EF Core migrations for new entities. Migration files will be created in `Data/Migrations/`.

3. **Audit Logging**: All services will extend `BaseService` to leverage existing audit logging via `IAuditService`.

4. **Error Handling**: Services will return `ErrorOr<T>` with domain errors defined in `LMS.Api.Common.Errors`.

5. **Mapping**: Entity-to-DTO mapping will use extension methods (like `ToDto()`) following existing patterns in `Common/Mapping/`.

6. **Validation**: Request validation will occur in FastEndpoints validators or service layer using FluentValidation or custom logic.

7. **Performance Considerations**: 
   - Phase 3 reporting services will implement caching for expensive calculations
   - Phase 5 will introduce caching layer for frequently accessed data
   - Database indexes will be added for new entities as needed

8. **Testing**: Each phase should include unit tests for services and integration tests for endpoints, following existing test patterns.

This implementation plan addresses all gaps identified in LMS_PROJECT_ANALYSIS.md while adhering to the established architectural patterns of the LMS API project.