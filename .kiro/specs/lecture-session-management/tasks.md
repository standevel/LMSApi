# Implementation Plan: Lecture Session Management

## Overview

This implementation plan breaks down the Lecture Session Management feature into discrete coding tasks. The feature enables admins to view and manage all lecture sessions while allowing lecturers to manage their assigned sessions, including uploading materials, taking attendance, and tracking completion.

The implementation follows this sequence:

1. Create new database entities and update existing ones
2. Implement backend service layer with business logic
3. Create API endpoints with authorization
4. Build Angular frontend components
5. Integrate and test the complete feature

## Tasks

- [x] 1. Set up database entities and migrations
  - [x] 1.1 Create SessionMaterial entity
    - Create `SessionMaterial.cs` in `Data/Entities` folder
    - Define properties: Id, LectureSessionId, FileName, FileUrl, FileSizeBytes, ContentType, UploadedAt, UploadedBy
    - Add navigation properties to LectureSession and AppUser
    - _Requirements: 6.5, 6.6_

  - [x] 1.2 Create SessionAttendance entity
    - Create `SessionAttendance.cs` in `Data/Entities` folder
    - Define properties: Id, LectureSessionId, StudentId, IsPresent, RecordedAt, RecordedBy, ModifiedAt, ModifiedBy
    - Add navigation properties to LectureSession, Student, RecordedByUser, ModifiedByUser
    - _Requirements: 7.4, 7.5, 7.6_

  - [x] 1.3 Update LectureSession entity
    - Add `IsCompleted` boolean property with default value false
    - Add navigation properties for Materials and Attendance collections
    - _Requirements: 8.2, 8.3_

  - [x] 1.4 Configure entity relationships in DbContext
    - Add DbSet properties for SessionMaterial and SessionAttendance
    - Configure one-to-many relationships in OnModelCreating
    - Add unique constraint on (LectureSessionId, StudentId) for SessionAttendance
    - Add indexes on SessionDate, CourseOfferingId, IsCompleted for LectureSession
    - Add composite index on (LectureSessionId, StudentId) for SessionAttendance

  - [x] 1.5 Create and apply database migration
    - Generate migration for new entities and updated LectureSession
    - Review migration SQL for correctness
    - Apply migration to database

- [x] 2. Implement file storage service
  - [x] 2.1 Create IFileStorageService interface
    - Define UploadFileAsync method accepting IFormFile, containerName, fileName
    - Define DeleteFileAsync method accepting fileUrl
    - _Requirements: 6.2, 6.7_

  - [x] 2.2 Implement FileStorageService for Azure Blob Storage
    - Implement UploadFileAsync to upload files to blob storage
    - Implement DeleteFileAsync to remove files from blob storage
    - Use container name "session-materials"
    - Return blob URL after upload
    - _Requirements: 6.2, 6.5_

  - [x] 2.3 Register FileStorageService in dependency injection
    - Add service registration in Program.cs
    - Configure Azure Blob Storage connection string

- [x] 3. Implement session management service layer
  - [x] 3.1 Create DTOs and request/response models
    - Create `SessionFilterRequest` record with filtering properties
    - Create `SessionListItem` record for list view
    - Create `SessionDetailsResponse` record with full session data
    - Create `UpdateSessionRequest` record for session editing
    - Create `SaveAttendanceRequest` and `AttendanceRecord` records
    - Create `UpdateNotesRequest` and `ToggleCompletionRequest` records
    - Create `AttendanceStatistics`, `LecturerInfo`, `MaterialInfo`, `EnrolledStudent` records
    - Create `PagedResult<T>` record for pagination
    - _Requirements: 1.2, 3.2, 4.1, 6.6, 7.3, 11.1-11.5_

  - [x] 3.2 Create ISessionManagementService interface
    - Define GetSessionsAsync method with filters, userId, isAdmin parameters
    - Define GetSessionDetailsAsync method
    - Define UpdateSessionAsync method (admin only)
    - Define DeleteSessionAsync method (admin only)
    - Define UploadMaterialAsync method
    - Define DeleteMaterialAsync method
    - Define SaveAttendanceAsync method
    - Define UpdateNotesAsync method
    - Define ToggleCompletionAsync method
    - Define GetEnrolledStudentsAsync method
    - Define GetAttendanceStatisticsAsync method
    - _Requirements: 1.1, 2.1, 3.1, 4.1, 5.1, 6.1, 6.7, 7.1, 8.1, 9.1_

  - [x] 3.3 Implement GetSessionsAsync with filtering and authorization
    - Query LectureSessions with eager loading of related entities
    - Apply role-based filtering (admin sees all, lecturer sees assigned only)
    - Apply CourseOfferingId, AcademicSessionId, LecturerId, date range, IsCompleted filters
    - Sort by SessionDate and StartTime ascending
    - Implement pagination
    - Map to SessionListItem records
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 2.1-2.6, 12.1, 12.2_

  - [x] 3.4 Implement GetSessionDetailsAsync with authorization
    - Query session by ID with all related entities
    - Check authorization (admin or assigned lecturer)
    - Map to SessionDetailsResponse with materials, attendance stats, lecturers
    - Calculate attendance statistics if attendance exists
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 12.1, 12.2_

  - [x] 3.5 Implement UpdateSessionAsync with validation and conflict detection
    - Verify admin authorization
    - Validate end time is after start time
    - Validate session date falls within academic session period
    - Check for lecturer conflicts (same lecturer, overlapping time)
    - Check for venue conflicts (same venue, overlapping time)
    - Update session properties and lecturer assignments
    - Return updated session or conflict warnings
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 4.6, 4.7, 12.3, 12.6_

  - [x] 3.6 Implement DeleteSessionAsync with cascade
    - Verify admin authorization
    - Delete associated SessionMaterial records and files from blob storage
    - Delete associated SessionAttendance records
    - Delete LectureSession record
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 12.4, 12.6_

  - [x] 3.7 Implement UploadMaterialAsync with validation
    - Verify lecturer is assigned to session
    - Validate file type (PDF, DOC, DOCX, PPT, PPTX)
    - Validate file size (max 50MB)
    - Upload file to blob storage using FileStorageService
    - Create SessionMaterial record with metadata
    - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5, 6.6, 12.5, 12.6_

  - [x] 3.8 Implement DeleteMaterialAsync with authorization
    - Verify lecturer is assigned to session or is admin
    - Delete file from blob storage
    - Delete SessionMaterial record
    - _Requirements: 6.7, 12.5, 12.6_

  - [x] 3.9 Implement SaveAttendanceAsync
    - Verify lecturer is assigned to session
    - Get enrolled students for course offering
    - Validate all student IDs are enrolled
    - Create or update SessionAttendance records
    - Set RecordedAt/RecordedBy for new records
    - Set ModifiedAt/ModifiedBy for updated records
    - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5, 7.6, 7.7, 12.5, 12.6_

  - [x] 3.10 Implement UpdateNotesAsync
    - Verify lecturer is assigned to session
    - Validate notes length (max 2000 characters)
    - Update session Notes field
    - _Requirements: 9.1, 9.2, 9.3, 9.4, 9.5, 12.5, 12.6_

  - [x] 3.11 Implement ToggleCompletionAsync
    - Verify lecturer is assigned to session
    - Update IsCompleted field
    - _Requirements: 8.1, 8.2, 8.3, 8.4, 8.5, 12.5, 12.6_

  - [x] 3.12 Implement GetEnrolledStudentsAsync
    - Query ProgramEnrollment for course offering
    - Return list of students with ID, name, email
    - Sort by student name
    - _Requirements: 7.2, 7.3_

  - [x] 3.13 Implement GetAttendanceStatisticsAsync
    - Query SessionAttendance for session
    - Count total students, present count, absent count
    - Calculate attendance percentage
    - Return AttendanceStatistics record
    - _Requirements: 11.1, 11.2, 11.3, 11.4, 11.5_

  - [ ]\* 3.14 Write unit tests for SessionManagementService
    - Test authorization logic (admin vs lecturer access)
    - Test file validation (type and size)
    - Test date range validation
    - Test notes length validation
    - Test attendance statistics calculation
    - Test filtering logic
    - Test conflict detection

- [x] 4. Checkpoint - Ensure service layer tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 5. Create API endpoints with authorization
  - [x] 5.1 Create GetSessionsEndpoint
    - Define GET route `/api/lecture-sessions`
    - Accept SessionFilterRequest as query parameters
    - Get current user ID and role from authentication
    - Call SessionManagementService.GetSessionsAsync
    - Return PagedResult<SessionListItem>
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 2.1-2.6_

  - [x] 5.2 Create GetSessionDetailsEndpoint
    - Define GET route `/api/lecture-sessions/{id}`
    - Get current user ID and role from authentication
    - Call SessionManagementService.GetSessionDetailsAsync
    - Return SessionDetailsResponse or 404/403
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5_

  - [x] 5.3 Create UpdateSessionEndpoint (admin only)
    - Define PUT route `/api/lecture-sessions/{id}`
    - Require admin role authorization
    - Accept UpdateSessionRequest body
    - Call SessionManagementService.UpdateSessionAsync
    - Return updated session or validation errors
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 4.6, 4.7, 12.3, 12.6_

  - [x] 5.4 Create DeleteSessionEndpoint (admin only)
    - Define DELETE route `/api/lecture-sessions/{id}`
    - Require admin role authorization
    - Call SessionManagementService.DeleteSessionAsync
    - Return 204 No Content or error
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 12.4, 12.6_

  - [x] 5.5 Create UploadMaterialEndpoint
    - Define POST route `/api/lecture-sessions/{id}/materials`
    - Accept multipart/form-data with file
    - Get current user ID from authentication
    - Call SessionManagementService.UploadMaterialAsync
    - Return SessionMaterial or validation errors
    - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5, 6.6, 12.5, 12.6_

  - [x] 5.6 Create DeleteMaterialEndpoint
    - Define DELETE route `/api/lecture-sessions/materials/{materialId}`
    - Get current user ID from authentication
    - Call SessionManagementService.DeleteMaterialAsync
    - Return 204 No Content or error
    - _Requirements: 6.7, 12.5, 12.6_

  - [x] 5.7 Create SaveAttendanceEndpoint
    - Define POST route `/api/lecture-sessions/{id}/attendance`
    - Accept SaveAttendanceRequest body
    - Get current user ID from authentication
    - Call SessionManagementService.SaveAttendanceAsync
    - Return AttendanceStatistics or error
    - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5, 7.6, 7.7, 12.5, 12.6_

  - [x] 5.8 Create UpdateSessionNotesEndpoint
    - Define PATCH route `/api/lecture-sessions/{id}/notes`
    - Accept UpdateNotesRequest body
    - Get current user ID from authentication
    - Call SessionManagementService.UpdateNotesAsync
    - Return updated session or error
    - _Requirements: 9.1, 9.2, 9.3, 9.4, 9.5, 12.5, 12.6_

  - [x] 5.9 Create MarkSessionCompletedEndpoint
    - Define PATCH route `/api/lecture-sessions/{id}/completion`
    - Accept ToggleCompletionRequest body
    - Get current user ID from authentication
    - Call SessionManagementService.ToggleCompletionAsync
    - Return updated session or error
    - _Requirements: 8.1, 8.2, 8.3, 8.4, 8.5, 12.5, 12.6_

  - [x] 5.10 Register all endpoints in Program.cs
    - Map all lecture session management endpoints
    - Ensure proper route grouping

  - [ ]\* 5.11 Write integration tests for API endpoints
    - Test request/response serialization
    - Test authentication and authorization
    - Test file upload handling
    - Test error response formatting
    - Test end-to-end workflows

- [x] 6. Checkpoint - Ensure API tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 7. Create Angular service for session management
  - [x] 7.1 Create SessionManagementService
    - Create `session-management.service.ts` in appropriate services folder
    - Define TypeScript interfaces matching backend DTOs
    - Implement getSessions method with filters
    - Implement getSessionDetails method
    - Implement updateSession method
    - Implement deleteSession method
    - Implement uploadMaterial method
    - Implement deleteMaterial method
    - Implement saveAttendance method
    - Implement updateNotes method
    - Implement toggleCompletion method
    - Implement getEnrolledStudents method
    - Use HttpClient for API calls
    - Handle errors and return observables
    - _Requirements: 1.1, 3.1, 4.1, 5.1, 6.1, 6.7, 7.1, 8.1, 9.1_

- [x] 8. Build SessionManagementComponent
  - [x] 8.1 Create component structure
    - Generate SessionManagementComponent in appropriate folder
    - Create component HTML template
    - Create component TypeScript class
    - Create component CSS with Wigwe theme styling
    - _Requirements: 1.1, 2.1, 3.1_

  - [x] 8.2 Implement session list view
    - Define signals for sessions, loading, filters, alerts
    - Implement loadSessions method calling service
    - Render session list with cards or table
    - Display course code, name, date, time, venue, lecturers
    - Show session type badge (Automatic/Manual)
    - Show completion status indicator
    - Show material count and attendance indicator
    - Implement sorting by date ascending
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 10.1, 10.2, 10.3, 10.4_

  - [x] 8.3 Implement filter controls
    - Create filter panel with course offering, academic session, lecturer, date range, completion status filters
    - Implement applyFilters method
    - Update session list within 500ms after filter changes
    - Allow multiple filters simultaneously
    - Add clear filters button
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6_

  - [x] 8.4 Implement pagination
    - Add pagination controls at bottom
    - Handle page changes
    - Display total count and current page
    - _Requirements: 1.1, 2.6_

  - [x] 8.5 Implement session details modal
    - Create modal component or use existing modal service
    - Display full session details when user selects session
    - Show course name, code, date, time, venue, lecturers, session type, notes
    - Display list of materials with download links
    - Display attendance statistics if available
    - Show completion status
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5_

  - [x] 8.6 Implement edit session functionality (admin only)
    - Show edit button only for admin users
    - Create edit modal with form
    - Pre-populate form with current session data
    - Allow editing date, time, venue, lecturers, notes
    - Validate end time after start time
    - Display conflict warnings for lecturer/venue conflicts
    - Call updateSession service method
    - Show success/error alerts
    - Refresh session list after update
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 4.6, 4.7, 12.3_

  - [x] 8.7 Implement delete session functionality (admin only)
    - Show delete button only for admin users
    - Display confirmation dialog with warning about materials/attendance
    - Call deleteSession service method
    - Show success/error alerts
    - Refresh session list after deletion
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 12.4_

  - [x] 8.8 Implement materials upload
    - Show upload button for lecturers on assigned sessions
    - Create file input with drag-and-drop support
    - Validate file types (PDF, DOC, DOCX, PPT, PPTX) on client
    - Validate file size (max 50MB) on client
    - Show upload progress bar
    - Call uploadMaterial service method
    - Display uploaded materials list
    - Show success/error alerts
    - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5, 6.6, 12.5_

  - [x] 8.9 Implement material deletion
    - Show delete button next to each material for lecturers
    - Display confirmation dialog
    - Call deleteMaterial service method
    - Update materials list
    - Show success/error alerts
    - _Requirements: 6.7, 12.5_

  - [x] 8.10 Implement attendance taking
    - Show "Take Attendance" button for lecturers on assigned sessions
    - Create attendance modal
    - Fetch enrolled students when modal opens
    - Display student list with name, ID, and present/absent checkboxes
    - Add "Select All" / "Deselect All" buttons
    - Add search/filter for students
    - Call saveAttendance service method
    - Display attendance statistics after save
    - Show success/error alerts
    - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5, 7.6, 7.7, 11.1-11.5, 12.5_

  - [x] 8.11 Implement notes editing
    - Show notes section for lecturers on assigned sessions
    - Allow editing notes up to 2000 characters
    - Show character count
    - Call updateNotes service method
    - Show success/error alerts
    - _Requirements: 9.1, 9.2, 9.3, 9.4, 9.5, 12.5_

  - [x] 8.12 Implement completion toggle
    - Show "Mark as Completed" button for lecturers on assigned sessions
    - Toggle button text based on current status
    - Call toggleCompletion service method
    - Update UI to show completion status
    - Show success/error alerts
    - _Requirements: 8.1, 8.2, 8.3, 8.4, 8.5, 12.5_

  - [x] 8.13 Implement alert system
    - Create alert display area (top-right corner)
    - Show success alerts (green) for successful operations
    - Show error alerts (red) for failures
    - Show warning alerts (amber) for conflicts
    - Show info alerts (blue) for processing status
    - Auto-dismiss success/info alerts after 5 seconds
    - Allow manual dismissal of all alerts
    - Stack multiple alerts vertically
    - Add smooth animations
    - _Requirements: All requirements (error handling)_

  - [x] 8.14 Apply Wigwe theme styling
    - Use Wigwe color palette for all UI elements
    - Apply Wigwe typography
    - Style buttons with Wigwe button styles
    - Style forms with consistent input styling
    - Style cards with Wigwe card components
    - Style tables with Wigwe table styling
    - Style modals with Wigwe modal styling
    - Ensure responsive design for mobile devices
    - _Requirements: All requirements (UI/UX)_

  - [ ]\* 8.15 Write component tests
    - Test filter controls behavior
    - Test modal display and interaction
    - Test file upload UI
    - Test attendance checkbox handling
    - Test alert display and dismissal
    - Test role-based UI rendering

- [x] 9. Add routing and navigation
  - [x] 9.1 Add route for session management
    - Add route in app.routes.ts
    - Configure route guards for authentication
    - Add navigation link in appropriate menu

  - [x] 9.2 Test navigation flow
    - Verify route loads component correctly
    - Test authentication guard
    - Test role-based access

- [ ] 10. Final integration and testing
  - [ ] 10.1 Test complete session viewing workflow
    - Test admin viewing all sessions
    - Test lecturer viewing assigned sessions only
    - Test filtering by various criteria
    - Test pagination
    - Test session details display
    - _Requirements: 1.1-1.5, 2.1-2.6, 3.1-3.5, 12.1, 12.2_

  - [ ] 10.2 Test complete session editing workflow (admin)
    - Test editing session details
    - Test validation errors
    - Test conflict warnings
    - Test unauthorized access (lecturer attempting edit)
    - _Requirements: 4.1-4.7, 12.3, 12.6_

  - [ ] 10.3 Test complete session deletion workflow (admin)
    - Test deletion with confirmation
    - Test cascade deletion of materials and attendance
    - Test unauthorized access (lecturer attempting delete)
    - _Requirements: 5.1-5.5, 12.4, 12.6_

  - [ ] 10.4 Test complete materials upload workflow
    - Test file selection and validation
    - Test upload progress
    - Test material display
    - Test material deletion
    - Test unauthorized access (unassigned lecturer)
    - _Requirements: 6.1-6.7, 12.5, 12.6_

  - [ ] 10.5 Test complete attendance workflow
    - Test fetching enrolled students
    - Test marking attendance
    - Test updating existing attendance
    - Test attendance statistics calculation
    - Test unauthorized access (unassigned lecturer)
    - _Requirements: 7.1-7.7, 11.1-11.5, 12.5, 12.6_

  - [ ] 10.6 Test notes and completion workflows
    - Test adding and editing notes
    - Test notes length validation
    - Test marking session as completed
    - Test unmarking completed session
    - Test unauthorized access (unassigned lecturer)
    - _Requirements: 8.1-8.5, 9.1-9.5, 12.5, 12.6_

  - [ ] 10.7 Test error handling and alerts
    - Test all validation error scenarios
    - Test network error handling
    - Test alert display and dismissal
    - Test alert stacking and animations

  - [ ] 10.8 Test responsive design
    - Test on mobile devices
    - Test on tablets
    - Test on desktop
    - Verify touch-friendly controls
    - Verify collapsible filter panel on mobile

- [ ] 11. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Authorization checks are critical for security and must be implemented correctly
- File upload requires proper validation on both client and server
- Attendance statistics must be calculated accurately
- UI must follow Wigwe theme guidelines consistently
- Alert system provides user feedback for all operations
