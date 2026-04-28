# Design Document: Lecture Session Management

## Overview

The Lecture Session Management feature provides comprehensive viewing and management capabilities for lecture sessions in the Wigwe University LMS. It enables administrators to oversee all sessions across the institution while allowing lecturers to manage their assigned sessions, including uploading materials, taking attendance, and tracking session completion.

This feature builds upon the existing lecture session creation system by adding:

- Session viewing and filtering for admins and lecturers
- Session editing and deletion (admin only)
- Course materials upload and management
- Attendance tracking and statistics
- Session completion status tracking
- Session notes management

### Key Design Decisions

- **Role-Based Views**: Admins see all sessions; lecturers see only their assigned sessions
- **New Entities**: `SessionMaterial` and `SessionAttendance` entities to support materials and attendance tracking
- **IsCompleted Field**: Added to `LectureSession` entity to track session delivery status
- **Soft Permissions**: Lecturers can only manage sessions where they are assigned
- **Material Storage**: Files stored in blob storage with metadata in database
- **Attendance Model**: One record per student per session with present/absent status
- **Filtering Architecture**: Multi-criteria filtering with efficient database queries
- **Audit Trail**: All modifications tracked with user and timestamp

## Architecture

### Component Structure

```
┌─────────────────────────────────────────────────────────────┐
│                     Presentation Layer                       │
│  ┌────────────────────────────────────────────────────────┐ │
│  │  SessionManagementComponent (Angular)                  │ │
│  │  - Session list view                                   │ │
│  │  - Filter controls                                     │ │
│  │  - Session details modal                               │ │
│  │  - Edit session modal (admin)                          │ │
│  │  - Materials upload                                    │ │
│  │  - Attendance taking                                   │ │
│  │  - Notes editor                                        │ │
│  └────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
                            │
                            │ HTTP/REST
                            ▼
┌─────────────────────────────────────────────────────────────┐
│                      API Layer (C#)                          │
│  ┌────────────────────────────────────────────────────────┐ │
│  │  LectureSessionEndpoints                               │ │
│  │  - GetSessionsEndpoint                                 │ │
│  │  - GetSessionDetailsEndpoint                           │ │
│  │  - UpdateSessionEndpoint (admin)                       │ │
│  │  - DeleteSessionEndpoint (admin)                       │ │
│  │  - UploadMaterialEndpoint                              │ │
│  │  - DeleteMaterialEndpoint                              │ │
│  │  - SaveAttendanceEndpoint                              │ │
│  │  - UpdateSessionNotesEndpoint                          │ │
│  │  - MarkSessionCompletedEndpoint                        │ │
│  └────────────────────────────────────────────────────────┘ │
│                            │                                 │
│  ┌────────────────────────────────────────────────────────┐ │
│  │  SessionManagementService                              │ │
│  │  - GetSessionsAsync()                                  │ │
│  │  - GetSessionDetailsAsync()                            │ │
│  │  - UpdateSessionAsync()                                │ │
│  │  - DeleteSessionAsync()                                │ │
│  │  - UploadMaterialAsync()                               │ │
│  │  - DeleteMaterialAsync()                               │ │
│  │  - SaveAttendanceAsync()                               │ │
│  │  - UpdateNotesAsync()                                  │ │
│  │  - ToggleCompletionAsync()                             │ │
│  │  - GetEnrolledStudentsAsync()                          │ │
│  └────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────┐
│                      Data Layer                              │
│  - LectureSession entity (updated with IsCompleted)          │
│  - SessionMaterial entity (new)                              │
│  - SessionAttendance entity (new)                            │
│  - LectureSessionLecturer entity (existing)                  │
│  - CourseOffering entity (existing)                          │
│  - ProgramEnrollment entity (existing)                       │
└─────────────────────────────────────────────────────────────┘
```

### Data Flow

**View Sessions Flow:**

```
1. User navigates to session management page
2. UI determines user role (admin/lecturer)
3. UI fetches sessions with filters
4. API queries sessions based on role and filters
5. UI displays session list with sorting
```

**Edit Session Flow (Admin):**

```
1. Admin selects session to edit
2. UI displays edit modal with current data
3. Admin modifies fields
4. API validates changes and checks conflicts
5. API updates session
6. UI refreshes session list
```

**Upload Material Flow:**

```
1. Lecturer selects files to upload
2. UI validates file types and sizes
3. API uploads files to blob storage
4. API creates SessionMaterial records
5. UI displays uploaded materials
```

**Take Attendance Flow:**

```
1. Lecturer opens attendance modal
2. API fetches enrolled students
3. UI displays student list with checkboxes
4. Lecturer marks present/absent
5. API saves/updates SessionAttendance records
6. UI displays attendance statistics
```

## Components and Interfaces

### Backend Components (C# / .NET)

#### 1. SessionMaterial Entity (New)

```csharp
public sealed class SessionMaterial
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LectureSessionId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public Guid UploadedBy { get; set; }

    // Navigation properties
    public LectureSession LectureSession { get; set; } = null!;
    public AppUser UploadedByUser { get; set; } = null!;
}
```

#### 2. SessionAttendance Entity (New)

```csharp
public sealed class SessionAttendance
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LectureSessionId { get; set; }
    public Guid StudentId { get; set; }
    public bool IsPresent { get; set; }
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
    public Guid RecordedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public Guid? ModifiedBy { get; set; }

    // Navigation properties
    public LectureSession LectureSession { get; set; } = null!;
    public AppUser Student { get; set; } = null!;
    public AppUser RecordedByUser { get; set; } = null!;
    public AppUser? ModifiedByUser { get; set; }
}
```

#### 3. Updated LectureSession Entity

```csharp
public sealed class LectureSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CourseOfferingId { get; set; }
    public Guid? TimetableSlotId { get; set; }
    public DateOnly SessionDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public Guid? VenueId { get; set; }
    public string? Notes { get; set; }
    public bool IsManuallyCreated { get; set; }
    public bool IsCompleted { get; set; } = false;  // NEW FIELD
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid CreatedBy { get; set; }

    // Navigation properties
    public CourseOffering CourseOffering { get; set; } = null!;
    public LectureTimetableSlot? TimetableSlot { get; set; }
    public Subject? Venue { get; set; }
    public AppUser CreatedByUser { get; set; } = null!;
    public ICollection<LectureSessionLecturer> SessionLecturers { get; set; } = new List<LectureSessionLecturer>();
    public ICollection<SessionMaterial> Materials { get; set; } = new List<SessionMaterial>();  // NEW
    public ICollection<SessionAttendance> Attendance { get; set; } = new List<SessionAttendance>();  // NEW
}
```

#### 4. SessionManagementService Interface

```csharp
public interface ISessionManagementService
{
    Task<PagedResult<SessionListItem>> GetSessionsAsync(
        SessionFilterRequest filters,
        Guid userId,
        bool isAdmin);

    Task<SessionDetailsResponse> GetSessionDetailsAsync(
        Guid sessionId,
        Guid userId,
        bool isAdmin);

    Task<LectureSession> UpdateSessionAsync(
        Guid sessionId,
        UpdateSessionRequest request,
        Guid userId);

    Task DeleteSessionAsync(Guid sessionId, Guid userId);

    Task<SessionMaterial> UploadMaterialAsync(
        Guid sessionId,
        IFormFile file,
        Guid userId);

    Task DeleteMaterialAsync(Guid materialId, Guid userId);

    Task SaveAttendanceAsync(
        Guid sessionId,
        List<AttendanceRecord> records,
        Guid userId);

    Task UpdateNotesAsync(
        Guid sessionId,
        string notes,
        Guid userId);

    Task ToggleCompletionAsync(
        Guid sessionId,
        bool isCompleted,
        Guid userId);

    Task<List<EnrolledStudent>> GetEnrolledStudentsAsync(
        Guid courseOfferingId);

    Task<AttendanceStatistics> GetAttendanceStatisticsAsync(
        Guid sessionId);
}
```

#### 5. API Endpoints

**GetSessionsEndpoint**

- **Route**: `GET /api/lecture-sessions`
- **Query Parameters**:
  ```csharp
  public record SessionFilterRequest(
      Guid? CourseOfferingId,
      Guid? AcademicSessionId,
      Guid? LecturerId,
      DateOnly? StartDate,
      DateOnly? EndDate,
      bool? IsCompleted,
      int Page = 1,
      int PageSize = 20);
  ```
- **Response**:

  ```csharp
  public record PagedResult<T>(
      List<T> Items,
      int TotalCount,
      int Page,
      int PageSize);

  public record SessionListItem(
      Guid Id,
      string CourseCode,
      string CourseName,
      DateOnly SessionDate,
      TimeOnly StartTime,
      TimeOnly EndTime,
      string? VenueName,
      List<string> LecturerNames,
      bool IsManuallyCreated,
      bool IsCompleted,
      int MaterialCount,
      bool HasAttendance);
  ```

**GetSessionDetailsEndpoint**

- **Route**: `GET /api/lecture-sessions/{id}`
- **Response**:

  ```csharp
  public record SessionDetailsResponse(
      Guid Id,
      string CourseCode,
      string CourseName,
      DateOnly SessionDate,
      TimeOnly StartTime,
      TimeOnly EndTime,
      string? VenueName,
      List<LecturerInfo> Lecturers,
      bool IsManuallyCreated,
      bool IsCompleted,
      string? Notes,
      List<MaterialInfo> Materials,
      AttendanceStatistics? AttendanceStats);

  public record LecturerInfo(
      Guid Id,
      string Name,
      string Email);

  public record MaterialInfo(
      Guid Id,
      string FileName,
      string FileUrl,
      long FileSizeBytes,
      DateTime UploadedAt,
      string UploadedByName);

  public record AttendanceStatistics(
      int TotalStudents,
      int PresentCount,
      int AbsentCount,
      decimal AttendancePercentage);
  ```

**UpdateSessionEndpoint** (Admin only)

- **Route**: `PUT /api/lecture-sessions/{id}`
- **Request**:
  ```csharp
  public record UpdateSessionRequest(
      DateOnly SessionDate,
      TimeOnly StartTime,
      TimeOnly EndTime,
      Guid? VenueId,
      List<Guid> LecturerIds,
      string? Notes);
  ```
- **Response**: `LectureSession`

**DeleteSessionEndpoint** (Admin only)

- **Route**: `DELETE /api/lecture-sessions/{id}`
- **Response**: `204 No Content`

**UploadMaterialEndpoint**

- **Route**: `POST /api/lecture-sessions/{id}/materials`
- **Request**: `multipart/form-data` with file
- **Response**: `SessionMaterial`

**DeleteMaterialEndpoint**

- **Route**: `DELETE /api/lecture-sessions/materials/{materialId}`
- **Response**: `204 No Content`

**SaveAttendanceEndpoint**

- **Route**: `POST /api/lecture-sessions/{id}/attendance`
- **Request**:

  ```csharp
  public record SaveAttendanceRequest(
      List<AttendanceRecord> Records);

  public record AttendanceRecord(
      Guid StudentId,
      bool IsPresent);
  ```

- **Response**: `AttendanceStatistics`

**UpdateSessionNotesEndpoint**

- **Route**: `PATCH /api/lecture-sessions/{id}/notes`
- **Request**:
  ```csharp
  public record UpdateNotesRequest(string Notes);
  ```
- **Response**: `LectureSession`

**MarkSessionCompletedEndpoint**

- **Route**: `PATCH /api/lecture-sessions/{id}/completion`
- **Request**:
  ```csharp
  public record ToggleCompletionRequest(bool IsCompleted);
  ```
- **Response**: `LectureSession`

### Frontend Components (Angular)

#### SessionManagementComponent

**Responsibilities:**

- Display session list with filtering
- Render session details modal
- Provide edit functionality (admin only)
- Handle materials upload
- Manage attendance taking
- Update session notes
- Toggle completion status
- Apply Wigwe theme styling
- Display alerts for all actions

**Key Methods:**

```typescript
interface SessionManagementComponent {
  loadSessions(filters: SessionFilters): void;
  applyFilters(): void;
  viewSessionDetails(sessionId: string): void;
  editSession(sessionId: string): void; // Admin only
  deleteSession(sessionId: string): void; // Admin only
  uploadMaterials(sessionId: string, files: File[]): void;
  deleteMaterial(materialId: string): void;
  openAttendanceModal(sessionId: string): void;
  saveAttendance(records: AttendanceRecord[]): void;
  updateNotes(sessionId: string, notes: string): void;
  toggleCompletion(sessionId: string, isCompleted: boolean): void;
  showAlert(alert: Alert): void;
  dismissAlert(alertId: string): void;
}
```

**Signals:**

```typescript
const sessions = signal<SessionListItem[]>([]);
const selectedSession = signal<SessionDetailsResponse | null>(null);
const filters = signal<SessionFilters>({});
const loading = signal<boolean>(false);
const uploading = signal<boolean>(false);
const savingAttendance = signal<boolean>(false);
const alerts = signal<Alert[]>([]);
const isAdmin = signal<boolean>(false);
const currentUserId = signal<string>("");
const enrolledStudents = signal<EnrolledStudent[]>([]);
const attendanceRecords = signal<Map<string, boolean>>(new Map());
```

## Data Models

### SessionMaterial

| Field            | Type     | Description          | Constraints              |
| ---------------- | -------- | -------------------- | ------------------------ |
| Id               | Guid     | Primary key          | Required                 |
| LectureSessionId | Guid     | Reference to session | Required, FK             |
| FileName         | string   | Original file name   | Required, max 500 chars  |
| FileUrl          | string   | Blob storage URL     | Required, max 2000 chars |
| FileSizeBytes    | long     | File size in bytes   | Required                 |
| ContentType      | string   | MIME type            | Required, max 100 chars  |
| UploadedAt       | DateTime | Upload timestamp     | Required                 |
| UploadedBy       | Guid     | Uploader user ID     | Required, FK             |

### SessionAttendance

| Field            | Type      | Description              | Constraints  |
| ---------------- | --------- | ------------------------ | ------------ |
| Id               | Guid      | Primary key              | Required     |
| LectureSessionId | Guid      | Reference to session     | Required, FK |
| StudentId        | Guid      | Reference to student     | Required, FK |
| IsPresent        | bool      | Attendance status        | Required     |
| RecordedAt       | DateTime  | Initial record timestamp | Required     |
| RecordedBy       | Guid      | Recording user ID        | Required, FK |
| ModifiedAt       | DateTime? | Last modification time   | Optional     |
| ModifiedBy       | Guid?     | Last modifier user ID    | Optional, FK |

**Unique Constraint**: `(LectureSessionId, StudentId)`

### Updated LectureSession

Added field:

| Field       | Type | Description             | Constraints             |
| ----------- | ---- | ----------------------- | ----------------------- |
| IsCompleted | bool | Session delivery status | Required, default false |

### AttendanceStatistics

```csharp
public record AttendanceStatistics(
    int TotalStudents,
    int PresentCount,
    int AbsentCount,
    decimal AttendancePercentage);
```

Calculation: `AttendancePercentage = (PresentCount / TotalStudents) × 100`

## Error Handling

### Validation Errors

| Error Code           | Condition                             | HTTP Status | Message                                                       |
| -------------------- | ------------------------------------- | ----------- | ------------------------------------------------------------- |
| SESSION_NOT_FOUND    | Invalid session ID                    | 404         | "Lecture session not found"                                   |
| UNAUTHORIZED_ACCESS  | Lecturer accessing unassigned session | 403         | "You are not authorized to access this session"               |
| ADMIN_ONLY           | Non-admin attempting admin action     | 403         | "This action requires administrator privileges"               |
| INVALID_FILE_TYPE    | Unsupported file format               | 400         | "File type not supported. Allowed: PDF, DOC, DOCX, PPT, PPTX" |
| FILE_TOO_LARGE       | File exceeds 50MB                     | 400         | "File size exceeds maximum allowed size of 50MB"              |
| INVALID_DATE_RANGE   | End time before start time            | 400         | "End time must be after start time"                           |
| SESSION_DATE_INVALID | Date outside academic session         | 400         | "Session date must fall within the academic session period"   |
| MATERIAL_NOT_FOUND   | Invalid material ID                   | 404         | "Material not found"                                          |
| STUDENT_NOT_ENROLLED | Student not in course offering        | 400         | "Student is not enrolled in this course offering"             |
| NOTES_TOO_LONG       | Notes exceed 2000 characters          | 400         | "Notes cannot exceed 2000 characters"                         |

### Error Response Format

```csharp
public record ErrorResponse(
    string Code,
    string Message,
    Dictionary<string, string[]>? ValidationErrors = null);
```

## Testing Strategy

This feature involves CRUD operations, file uploads, business logic validation, and role-based access control. The testing strategy will use:

### Unit Tests

Unit tests will cover:

1. **Authorization Logic**
   - Admin can access all sessions
   - Lecturer can only access assigned sessions
   - Unauthorized access returns 403

2. **Validation Rules**
   - File type validation (PDF, DOC, DOCX, PPT, PPTX)
   - File size validation (max 50MB)
   - Date range validation
   - Notes length validation (max 2000 chars)

3. **Business Logic**
   - Attendance statistics calculation
   - Enrolled students retrieval
   - Session filtering logic

4. **Data Transformations**
   - Session list item mapping
   - Session details response mapping
   - Attendance record processing

### Integration Tests

Integration tests will verify:

1. **Database Operations**
   - Creating SessionMaterial records
   - Creating/updating SessionAttendance records
   - Updating LectureSession IsCompleted field
   - Querying sessions with filters
   - Deleting sessions with cascading deletes

2. **API Endpoints**
   - Request/response serialization
   - Authentication and authorization
   - File upload handling
   - Error response formatting

3. **End-to-End Workflows**
   - Complete session viewing flow
   - Complete materials upload flow
   - Complete attendance taking flow
   - Complete session editing flow (admin)

### UI Tests

1. **Component Tests**
   - Filter controls behavior
   - Modal display and interaction
   - File upload UI
   - Attendance checkbox handling
   - Alert display and dismissal

2. **Integration Tests**
   - API service integration
   - Role-based UI rendering
   - Navigation and routing
   - Error handling and display

### Test Data Strategy

- Use in-memory database for unit tests
- Use test database with seed data for integration tests
- Mock file storage service for upload tests
- Generate test data covering edge cases (boundary dates, large files, many students)

## Implementation Notes

### Authorization Middleware

```csharp
public static class SessionAuthorizationExtensions
{
    public static async Task<bool> CanAccessSessionAsync(
        this LmsDbContext context,
        Guid sessionId,
        Guid userId,
        bool isAdmin)
    {
        if (isAdmin) return true;

        return await context.LectureSessionLecturers
            .AnyAsync(sl => sl.LectureSessionId == sessionId && sl.LecturerId == userId);
    }
}
```

### File Upload Service

```csharp
public interface IFileStorageService
{
    Task<string> UploadFileAsync(
        IFormFile file,
        string containerName,
        string fileName);

    Task DeleteFileAsync(string fileUrl);
}
```

Implementation uses Azure Blob Storage with container `session-materials`.

### Attendance Query Optimization

```csharp
public async Task<List<EnrolledStudent>> GetEnrolledStudentsAsync(
    Guid courseOfferingId)
{
    return await _context.Enrollments
        .Where(e => e.CourseOffering.Id == courseOfferingId)
        .Select(e => new EnrolledStudent(
            e.UserId,
            e.User.DisplayName ?? e.User.Email ?? "Unknown",
            e.User.Email ?? ""))
        .OrderBy(s => s.Name)
        .ToListAsync();
}
```

### Session Filtering Query

```csharp
public async Task<PagedResult<SessionListItem>> GetSessionsAsync(
    SessionFilterRequest filters,
    Guid userId,
    bool isAdmin)
{
    var query = _context.LectureSessions
        .Include(s => s.CourseOffering)
            .ThenInclude(co => co.Course)
        .Include(s => s.Venue)
        .Include(s => s.SessionLecturers)
            .ThenInclude(sl => sl.Lecturer)
        .Include(s => s.Materials)
        .Include(s => s.Attendance)
        .AsQueryable();

    // Role-based filtering
    if (!isAdmin)
    {
        query = query.Where(s => s.SessionLecturers.Any(sl => sl.LecturerId == userId));
    }

    // Apply filters
    if (filters.CourseOfferingId.HasValue)
    {
        query = query.Where(s => s.CourseOfferingId == filters.CourseOfferingId.Value);
    }

    if (filters.AcademicSessionId.HasValue)
    {
        query = query.Where(s => s.CourseOffering.AcademicSessionId == filters.AcademicSessionId.Value);
    }

    if (filters.LecturerId.HasValue)
    {
        query = query.Where(s => s.SessionLecturers.Any(sl => sl.LecturerId == filters.LecturerId.Value));
    }

    if (filters.StartDate.HasValue)
    {
        query = query.Where(s => s.SessionDate >= filters.StartDate.Value);
    }

    if (filters.EndDate.HasValue)
    {
        query = query.Where(s => s.SessionDate <= filters.EndDate.Value);
    }

    if (filters.IsCompleted.HasValue)
    {
        query = query.Where(s => s.IsCompleted == filters.IsCompleted.Value);
    }

    // Sort by date ascending
    query = query.OrderBy(s => s.SessionDate).ThenBy(s => s.StartTime);

    // Pagination
    var totalCount = await query.CountAsync();
    var items = await query
        .Skip((filters.Page - 1) * filters.PageSize)
        .Take(filters.PageSize)
        .Select(s => new SessionListItem(
            s.Id,
            s.CourseOffering.Course.Code,
            s.CourseOffering.Course.Title,
            s.SessionDate,
            s.StartTime,
            s.EndTime,
            s.Venue != null ? s.Venue.Name : null,
            s.SessionLecturers.Select(sl => sl.Lecturer.DisplayName ?? sl.Lecturer.Email ?? "Unknown").ToList(),
            s.IsManuallyCreated,
            s.IsCompleted,
            s.Materials.Count,
            s.Attendance.Any()))
        .ToListAsync();

    return new PagedResult<SessionListItem>(items, totalCount, filters.Page, filters.PageSize);
}
```

### Performance Considerations

- **Eager Loading**: Use `.Include()` to avoid N+1 queries
- **Pagination**: Implement server-side pagination for large session lists
- **Indexes**: Add indexes on `SessionDate`, `CourseOfferingId`, `IsCompleted`
- **Composite Index**: Add composite index on `(LectureSessionId, StudentId)` for attendance queries
- **File Upload**: Use streaming for large file uploads
- **Caching**: Cache enrolled students list during attendance taking

### Security Considerations

- **Authorization**: Verify user permissions on every request
- **File Validation**: Validate file types and sizes on server
- **SQL Injection**: Use parameterized queries (handled by EF Core)
- **XSS Prevention**: Sanitize notes input
- **CSRF Protection**: Use anti-forgery tokens
- **Audit Trail**: Log all modifications with user and timestamp

## UI/UX Requirements

### Wigwe Theme Integration

The UI must follow the Wigwe University brand guidelines:

**Color Palette:**

- Primary: Wigwe brand colors
- Success: Green for completed sessions
- Warning: Amber for pending actions
- Error: Red for validation errors
- Info: Blue for informational messages

**Typography:**

- Wigwe-approved font families
- Consistent heading hierarchy
- Readable body text

**Component Styling:**

- Buttons: Wigwe button styles
- Forms: Consistent input styling
- Cards: Wigwe card components
- Tables: Wigwe table styling
- Modals: Wigwe modal styling

### Alert System Requirements

**Alert Types:**

1. **Success Alerts** (Green)
   - Session updated successfully
   - Materials uploaded successfully
   - Attendance saved successfully
   - Session deleted successfully

2. **Error Alerts** (Red)
   - Validation failures
   - API errors
   - File upload failures
   - Unauthorized access attempts

3. **Warning Alerts** (Amber)
   - Conflict warnings
   - Unsaved changes warnings

4. **Info Alerts** (Blue)
   - Processing status
   - Loading indicators

**Alert Behavior:**

- Position: Top-right corner
- Duration: Success/Info auto-dismiss after 5s, Error/Warning persist
- Dismissible: All alerts have close button
- Stacking: Multiple alerts stack vertically
- Animation: Smooth slide-in/fade-out

### Session List View

**Layout:**

- Filter panel on left or top
- Session cards/table in main area
- Pagination controls at bottom

**Session Card/Row:**

- Course code and name
- Date and time
- Venue
- Lecturer names
- Session type badge (Automatic/Manual)
- Completion status indicator
- Material count badge
- Attendance indicator
- Action buttons (View, Edit, Delete)

**Filters:**

- Course offering dropdown
- Academic session dropdown
- Lecturer dropdown (admin only)
- Date range picker
- Completion status toggle
- Clear filters button

### Session Details Modal

**Sections:**

1. **Header**: Course name, date, time
2. **Details**: Venue, lecturers, session type
3. **Notes**: Display/edit notes (lecturer)
4. **Materials**: List with download links, upload button (lecturer)
5. **Attendance**: Statistics display, "Take Attendance" button (lecturer)
6. **Actions**: Mark completed, Edit (admin), Delete (admin)

### Materials Upload

**UI Elements:**

- File input with drag-and-drop
- File type indicator (PDF, DOC, DOCX, PPT, PPTX)
- File size validation feedback
- Upload progress bar
- Material list with delete buttons

### Attendance Modal

**Layout:**

- Student list with checkboxes
- "Select All" / "Deselect All" buttons
- Search/filter students
- Save button
- Statistics summary at top

**Student Row:**

- Student name
- Student ID
- Present/Absent checkbox
- Visual indicator for saved status

### Responsive Design

- Mobile-friendly layout
- Touch-friendly controls
- Collapsible filter panel on mobile
- Stacked cards on small screens
