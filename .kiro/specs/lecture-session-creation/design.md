# Design Document: Lecture Session Creation

## Overview

This feature enables administrators to create individual lecture session instances for course offerings using three methods:

1. **Automatic Generation (Single Course)**: Creates sessions from existing timetable slots for a specific course offering
2. **Bulk Generation (All Courses)**: Creates sessions for all courses in the semester timetable at once
3. **Manual Creation**: Creates individual sessions by selecting specific dates and times

The system generates `LectureSession` entities that represent specific occurrences of lectures on particular dates. These sessions can be used for attendance tracking, content delivery, and schedule management throughout the semester.

### Key Design Decisions

- **Session as Snapshot**: Each `LectureSession` captures a snapshot of scheduling data (lecturers, venue, time) at creation time, allowing independent modification without affecting the source timetable
- **Multiple Lecturers Support**: Sessions support multiple lecturers through a many-to-many relationship, allowing all assigned lecturers to access and manage the session
- **Three Creation Modes**: Single course automatic generation, bulk generation for all courses, and manual creation to optimize for different administrative workflows
- **Conflict Detection**: Non-blocking warnings that inform but don't prevent creation, giving admins flexibility
- **Date-Based Generation**: Uses academic calendar dates and day-of-week matching to calculate session dates from recurring timetable slots

## Architecture

### Component Structure

```
┌─────────────────────────────────────────────────────────────┐
│                     Presentation Layer                       │
│  ┌────────────────────────────────────────────────────────┐ │
│  │  LectureSessionCreationComponent (Angular)             │ │
│  │  - Mode selection UI                                   │ │
│  │  - Automatic generation form                           │ │
│  │  - Manual creation form                                │ │
│  │  - Conflict display                                    │ │
│  └────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
                            │
                            │ HTTP/REST
                            ▼
┌─────────────────────────────────────────────────────────────┐
│                      API Layer (C#)                          │
│  ┌────────────────────────────────────────────────────────┐ │
│  │  LectureSessionEndpoints                               │ │
│  │  - GenerateSessionsFromTimetableEndpoint               │ │
│  │  - GenerateBulkSessionsForSemesterEndpoint             │ │
│  │  - CreateManualSessionEndpoint                         │ │
│  │  - GetTimetableSlotsForOfferingEndpoint                │ │
│  │  - GetAllCourseOfferingsForSessionEndpoint             │ │
│  │  - ValidateSessionConflictsEndpoint                    │ │
│  └────────────────────────────────────────────────────────┘ │
│                            │                                 │
│  ┌────────────────────────────────────────────────────────┐ │
│  │  LectureSessionService                                 │ │
│  │  - GenerateSessionsFromSlots()                         │ │
│  │  - GenerateBulkSessionsForSemester()                   │ │
│  │  - CreateManualSession()                               │ │
│  │  - DetectConflicts()                                   │ │
│  │  - CalculateSessionDates()                             │ │
│  └────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────┐
│                      Data Layer                              │
│  - LectureSession entity                                     │
│  - LectureSessionLecturer entity (join table)                │
│  - LectureTimetableSlot entity (existing)                    │
│  - CourseOffering entity (existing)                          │
│  - AcademicSession entity (existing)                         │
└─────────────────────────────────────────────────────────────┘
```

### Data Flow

**Automatic Generation Flow (Single Course):**

```
1. Admin selects course offering + end date
2. UI fetches timetable slots for offering
3. Admin selects slot(s) to generate from
4. API calculates dates from start to end date
5. API creates LectureSession for each occurrence
6. API detects conflicts across all sessions
7. UI displays summary with conflict warnings
```

**Bulk Generation Flow (All Courses):**

```
1. Admin selects academic session + end date
2. UI displays all course offerings with timetable slots
3. Admin confirms bulk generation
4. API fetches all timetable slots for all course offerings
5. API calculates dates for each slot from start to end date
6. API creates LectureSession for all occurrences across all courses
7. API detects conflicts across all sessions
8. UI displays comprehensive summary with sessions per course and conflicts
```

**Manual Creation Flow:**

```
1. Admin selects date, time, course offering
2. Admin specifies lecturers (multiple), venue, notes
3. API validates date within academic session
4. API checks for conflicts (all lecturers)
5. API creates single LectureSession with lecturer associations
6. UI displays confirmation or conflict warning
```

## Components and Interfaces

### Backend Components (C# / .NET)

#### 1. LectureSession Entity

```csharp
public sealed class LectureSession
{
    public Guid Id { get; set; }
    public Guid CourseOfferingId { get; set; }
    public Guid? TimetableSlotId { get; set; }  // null for manual sessions
    public DateOnly SessionDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public Guid? VenueId { get; set; }
    public string? Notes { get; set; }
    public bool IsManuallyCreated { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid CreatedBy { get; set; }

    // Navigation properties
    public CourseOffering CourseOffering { get; set; }
    public LectureTimetableSlot? TimetableSlot { get; set; }
    public ICollection<LectureSessionLecturer> SessionLecturers { get; set; }
    public Subject? Venue { get; set; }
    public AppUser CreatedByUser { get; set; }
}
```

#### 2. LectureSessionLecturer Entity (Join Table)

```csharp
public sealed class LectureSessionLecturer
{
    public Guid LectureSessionId { get; set; }
    public Guid LecturerId { get; set; }

    // Navigation properties
    public LectureSession LectureSession { get; set; }
    public AppUser Lecturer { get; set; }
}
```

#### 3. LectureSessionService Interface

```csharp
public interface ILectureSessionService
{
    Task<SessionGenerationResult> GenerateSessionsFromTimetableAsync(
        List<Guid> timetableSlotIds,
        DateOnly endDate,
        Guid userId);

    Task<BulkSessionGenerationResult> GenerateBulkSessionsForSemesterAsync(
        Guid academicSessionId,
        DateOnly endDate,
        Guid userId);

    Task<LectureSession> CreateManualSessionAsync(
        CreateManualSessionRequest request,
        Guid userId);

    Task<List<ConflictWarning>> DetectConflictsAsync(
        DateOnly date,
        TimeOnly startTime,
        TimeOnly endTime,
        List<Guid> lecturerIds,
        Guid? venueId);

    Task<List<LectureTimetableSlot>> GetTimetableSlotsForOfferingAsync(
        Guid courseOfferingId);

    Task<List<CourseOffering>> GetCourseOfferingsWithTimetableSlotsAsync(
        Guid academicSessionId);
}
```

#### 4. API Endpoints

**GenerateSessionsFromTimetableEndpoint**

- **Route**: `POST /api/lecture-sessions/generate`
- **Request**:
  ```csharp
  public record GenerateSessionsRequest(
      List<Guid> TimetableSlotIds,
      DateOnly EndDate);
  ```
- **Response**:
  ```csharp
  public record SessionGenerationResult(
      int TotalSessionsCreated,
      Dictionary<Guid, int> SessionsPerSlot,
      List<ConflictWarning> Conflicts);
  ```

**GenerateBulkSessionsForSemesterEndpoint**

- **Route**: `POST /api/lecture-sessions/generate-bulk`
- **Request**:
  ```csharp
  public record GenerateBulkSessionsRequest(
      Guid AcademicSessionId,
      DateOnly EndDate);
  ```
- **Response**:

  ```csharp
  public record BulkSessionGenerationResult(
      int TotalSessionsCreated,
      Dictionary<Guid, CourseGenerationSummary> SessionsPerCourse,
      List<ConflictWarning> Conflicts);

  public record CourseGenerationSummary(
      string CourseName,
      int TotalSessions,
      Dictionary<Guid, int> SessionsPerSlot);
  ```

**GetAllCourseOfferingsForSessionEndpoint**

- **Route**: `GET /api/lecture-sessions/course-offerings/{academicSessionId}`
- **Response**: `List<CourseOfferingWithSlotCount>`
  ```csharp
  public record CourseOfferingWithSlotCount(
      Guid Id,
      string CourseName,
      int TimetableSlotCount);
  ```

**CreateManualSessionEndpoint**

- **Route**: `POST /api/lecture-sessions/manual`
- **Request**:
  ```csharp
  public record CreateManualSessionRequest(
      Guid CourseOfferingId,
      DateOnly SessionDate,
      TimeOnly StartTime,
      TimeOnly EndTime,
      List<Guid> LecturerIds,
      Guid? VenueId,
      string? Notes);
  ```
- **Response**: `LectureSession`

**ValidateSessionConflictsEndpoint**

- **Route**: `POST /api/lecture-sessions/validate-conflicts`
- **Request**:
  ```csharp
  public record ValidateConflictsRequest(
      DateOnly Date,
      TimeOnly StartTime,
      TimeOnly EndTime,
      List<Guid> LecturerIds,
      Guid? VenueId);
  ```
- **Response**: `List<ConflictWarning>`

### Frontend Components (Angular)

#### LectureSessionCreationComponent

**Responsibilities:**

- Display mode selection (automatic single course, bulk all courses, manual)
- Render automatic generation form with timetable slot selection for single course
- Render bulk generation form with course offering preview for all courses
- Render manual creation form with date/time picker
- Display conflict warnings
- Show generation summary (single course or bulk)
- Apply Wigwe theme styling consistently
- Display alerts for all user actions (success, error, warning, info)

**Key Methods:**

```typescript
interface LectureSessionCreationComponent {
  selectCreationMode(mode: "automatic" | "bulk" | "manual"): void;
  loadTimetableSlotsForOffering(offeringId: string): void;
  loadCourseOfferingsForSession(sessionId: string): void;
  generateSessionsFromTimetable(slotIds: string[], endDate: Date): void;
  generateBulkSessionsForSemester(sessionId: string, endDate: Date): void;
  createManualSession(sessionData: ManualSessionData): void;
  validateConflicts(sessionData: SessionValidationData): void;
  displayGenerationSummary(result: SessionGenerationResult): void;
  displayBulkGenerationSummary(result: BulkSessionGenerationResult): void;
  showAlert(alert: Alert): void;
  dismissAlert(alertId: string): void;
}
```

## Data Models

### LectureSession

| Field             | Type     | Description                        | Constraints                   |
| ----------------- | -------- | ---------------------------------- | ----------------------------- |
| Id                | Guid     | Primary key                        | Required                      |
| CourseOfferingId  | Guid     | Reference to course offering       | Required, FK                  |
| TimetableSlotId   | Guid?    | Reference to source timetable slot | Optional, FK, null for manual |
| SessionDate       | DateOnly | Date of the lecture                | Required                      |
| StartTime         | TimeOnly | Start time                         | Required                      |
| EndTime           | TimeOnly | End time                           | Required, > StartTime         |
| VenueId           | Guid?    | Assigned venue                     | Optional, FK                  |
| Notes             | string?  | Additional notes                   | Optional, max 1000 chars      |
| IsManuallyCreated | bool     | Creation mode flag                 | Required                      |
| CreatedAt         | DateTime | Creation timestamp                 | Required                      |
| CreatedBy         | Guid     | Creating admin user                | Required, FK                  |

### LectureSessionLecturer

| Field            | Type | Description                  | Constraints      |
| ---------------- | ---- | ---------------------------- | ---------------- |
| LectureSessionId | Guid | Reference to lecture session | Required, FK, PK |
| LecturerId       | Guid | Reference to lecturer        | Required, FK, PK |

### ConflictWarning

```csharp
public record ConflictWarning(
    ConflictType Type,
    string Description,
    Guid ConflictingSessionId,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime);

public enum ConflictType
{
    LecturerConflict,
    VenueConflict
}
```

### SessionGenerationResult

```csharp
public record SessionGenerationResult(
    int TotalSessionsCreated,
    Dictionary<Guid, int> SessionsPerSlot,
    List<ConflictWarning> Conflicts);
```

### BulkSessionGenerationResult

```csharp
public record BulkSessionGenerationResult(
    int TotalSessionsCreated,
    Dictionary<Guid, CourseGenerationSummary> SessionsPerCourse,
    List<ConflictWarning> Conflicts);

public record CourseGenerationSummary(
    string CourseName,
    int TotalSessions,
    Dictionary<Guid, int> SessionsPerSlot);
```

## Error Handling

### Validation Errors

| Error Code                | Condition                             | HTTP Status | Message                                                     |
| ------------------------- | ------------------------------------- | ----------- | ----------------------------------------------------------- |
| INVALID_END_DATE          | End date before session start         | 400         | "End date must be after academic session start date"        |
| END_DATE_EXCEEDS_SESSION  | End date after session end            | 400         | "End date cannot exceed academic session end date"          |
| INVALID_DATE_RANGE        | Session date outside academic session | 400         | "Session date must fall within the academic session period" |
| INVALID_TIME_RANGE        | End time before start time            | 400         | "End time must be after start time"                         |
| COURSE_OFFERING_NOT_FOUND | Invalid course offering ID            | 404         | "Course offering not found"                                 |
| TIMETABLE_SLOT_NOT_FOUND  | Invalid timetable slot ID             | 404         | "Timetable slot not found"                                  |
| NO_TIMETABLE_SLOTS        | No slots for offering                 | 400         | "No timetable slots found for this course offering"         |

### Conflict Warnings

Conflicts are **non-blocking warnings** that inform the admin but allow creation to proceed:

- **Lecturer Conflict**: Another session exists with the same lecturer at overlapping time
- **Venue Conflict**: Another session exists with the same venue at overlapping time

The API returns conflict warnings in the response, and the UI displays them prominently with an option to proceed.

### Error Response Format

```csharp
public record ErrorResponse(
    string Code,
    string Message,
    Dictionary<string, string[]>? ValidationErrors = null);
```

## Testing Strategy

This feature involves primarily CRUD operations, date calculations, business logic validation, and database interactions. The testing strategy will use:

### Unit Tests

Unit tests will cover:

1. **Date Calculation Logic**
   - Calculating session dates from timetable slots and week numbers
   - Validating dates fall within academic session boundaries
   - Handling edge cases (semester start/end, holidays)

2. **Conflict Detection Logic**
   - Identifying lecturer time overlaps
   - Identifying venue time overlaps
   - Handling null lecturer/venue cases

3. **Validation Rules**
   - End date validation (not before start, not after session end)
   - Time range validation (end after start)
   - Academic session boundary validation

4. **Business Logic**
   - Session creation from timetable slot data
   - Manual session creation with custom data
   - Generation summary calculation

### Integration Tests

Integration tests will verify:

1. **Database Operations**
   - Creating lecture sessions in the database
   - Querying existing sessions for conflict detection
   - Fetching timetable slots for course offerings
   - Transaction handling for bulk generation

2. **API Endpoints**
   - Request/response serialization
   - Authentication and authorization
   - Error response formatting
   - Conflict warning inclusion in responses

3. **End-to-End Workflows**
   - Complete automatic generation flow
   - Complete manual creation flow
   - Conflict detection across multiple sessions

### UI Tests

1. **Component Tests**
   - Mode selection behavior
   - Form validation
   - Conflict warning display
   - Summary display

2. **Integration Tests**
   - API service integration
   - Navigation between modes
   - Error handling and display

### Test Data Strategy

- Use in-memory database for unit tests
- Use test database with seed data for integration tests
- Mock external dependencies (authentication, authorization)
- Generate test data covering edge cases (boundary dates, overlapping times)

## Implementation Notes

### Date Calculation Algorithm

For automatic generation from timetable slots:

```csharp
public List<DateOnly> CalculateSessionDates(
    LectureTimetableSlot slot,
    DateOnly startDate,
    DateOnly endDate)
{
    var dates = new List<DateOnly>();
    var current = startDate;

    // Find first occurrence of the slot's day of week
    while (current.DayOfWeek != slot.DayOfWeek && current <= endDate)
    {
        current = current.AddDays(1);
    }

    // Add all occurrences until end date
    while (current <= endDate)
    {
        dates.Add(current);
        current = current.AddDays(7); // Next week
    }

    return dates;
}
```

### Conflict Detection Query

```csharp
public async Task<List<ConflictWarning>> DetectConflictsAsync(
    DateOnly date,
    TimeOnly startTime,
    TimeOnly endTime,
    List<Guid> lecturerIds,
    Guid? venueId)
{
    var conflicts = new List<ConflictWarning>();

    // Check lecturer conflicts for each lecturer
    if (lecturerIds.Any())
    {
        var lecturerConflicts = await _context.LectureSessions
            .Where(s => s.SessionDate == date
                && s.StartTime < endTime
                && s.EndTime > startTime
                && s.SessionLecturers.Any(sl => lecturerIds.Contains(sl.LecturerId)))
            .Include(s => s.SessionLecturers)
            .ToListAsync();

        conflicts.AddRange(lecturerConflicts.Select(s =>
            new ConflictWarning(
                ConflictType.LecturerConflict,
                $"One or more lecturers are already scheduled for another session",
                s.Id,
                s.SessionDate,
                s.StartTime,
                s.EndTime)));
    }

    // Check venue conflicts
    if (venueId.HasValue)
    {
        var venueConflicts = await _context.LectureSessions
            .Where(s => s.SessionDate == date
                && s.VenueId == venueId
                && s.StartTime < endTime
                && s.EndTime > startTime)
            .ToListAsync();

        conflicts.AddRange(venueConflicts.Select(s =>
            new ConflictWarning(
                ConflictType.VenueConflict,
                $"Venue is already scheduled for another session",
                s.Id,
                s.SessionDate,
                s.StartTime,
                s.EndTime)));
    }

    return conflicts;
}
```

### UI State Management

The Angular component will manage state for:

- Selected creation mode (automatic, bulk, or manual)
- Selected academic session and course offering
- Selected timetable slots (for automatic mode)
- Course offerings list (for bulk mode)
- Form data (for manual mode)
- Conflict warnings
- Generation results (single or bulk)

Use Angular signals for reactive state management:

```typescript
const creationMode = signal<"automatic" | "bulk" | "manual" | null>(null);
const selectedSlots = signal<TimetableSlot[]>([]);
const courseOfferings = signal<CourseOfferingWithSlotCount[]>([]);
const conflicts = signal<ConflictWarning[]>([]);
const generationResult = signal<SessionGenerationResult | null>(null);
const bulkGenerationResult = signal<BulkSessionGenerationResult | null>(null);
const alerts = signal<Alert[]>([]);
const isProcessing = signal<boolean>(false);
const processingProgress = signal<number>(0);
```

### Performance Considerations

- **Bulk Generation**: Use batch insert with transaction for creating multiple sessions across all courses
- **Parallel Processing**: Consider processing course offerings in parallel for bulk generation to improve performance
- **Progress Tracking**: For bulk generation, implement progress tracking to show admin which courses are being processed
- **Conflict Detection**: Query optimization with proper indexes on SessionDate, VenueId, and composite index on LectureSessionLecturer table
- **Pagination**: For large timetable slot lists, implement pagination
- **Caching**: Cache academic session dates to avoid repeated queries

### Security Considerations

- **Authorization**: Only admins can create lecture sessions
- **Validation**: Server-side validation of all inputs
- **Audit Trail**: Record creator and creation timestamp for all sessions
- **SQL Injection**: Use parameterized queries (handled by EF Core)

## UI/UX Requirements

### Wigwe Theme Integration

The UI must follow the Wigwe University brand guidelines and design system:

**Color Palette:**

- Primary: Wigwe brand colors (blues, golds as defined in theme)
- Success: Green tones for successful operations
- Warning: Amber/yellow for conflict warnings
- Error: Red tones for validation errors
- Info: Blue tones for informational messages

**Typography:**

- Use Wigwe-approved font families
- Consistent heading hierarchy (H1-H6)
- Readable body text with appropriate line height

**Component Styling:**

- Buttons: Follow Wigwe button styles (primary, secondary, outline variants)
- Forms: Consistent input field styling with labels and validation states
- Cards: Use Wigwe card components for content grouping
- Tables: Wigwe table styling for session summaries
- Modals: Wigwe modal/dialog styling for confirmations

**Layout:**

- Consistent spacing using Wigwe spacing scale
- Responsive design following Wigwe breakpoints
- Navigation consistent with existing LMS UI

### Alert System Requirements

The system must display alerts for all user actions to provide clear feedback:

**Alert Types and Usage:**

1. **Success Alerts** (Green)
   - Session(s) created successfully
   - Bulk generation completed
   - Manual session saved
   - Example: "Successfully created 45 lecture sessions for Course XYZ"

2. **Error Alerts** (Red)
   - Validation failures
   - API errors
   - Network failures
   - Example: "Failed to create sessions: End date must be within academic session period"

3. **Warning Alerts** (Amber/Yellow)
   - Conflict detections (lecturer/venue)
   - Non-blocking issues
   - Example: "Warning: 3 lecturer conflicts detected. Review conflicts before proceeding."

4. **Info Alerts** (Blue)
   - Processing status
   - Informational messages
   - Example: "Generating sessions for 12 courses. This may take a moment..."

**Alert Behavior:**

- **Position**: Top-right corner or top-center of viewport
- **Duration**:
  - Success: Auto-dismiss after 5 seconds
  - Error: Persist until user dismisses
  - Warning: Persist until user dismisses or acknowledges
  - Info: Auto-dismiss after 3 seconds or when action completes
- **Dismissible**: All alerts should have a close button
- **Stacking**: Multiple alerts should stack vertically
- **Animation**: Smooth slide-in/fade-in entrance, fade-out exit

**Alert Content Structure:**

```typescript
interface Alert {
  type: "success" | "error" | "warning" | "info";
  title: string;
  message: string;
  dismissible: boolean;
  autoDismiss?: number; // milliseconds
  actions?: AlertAction[]; // Optional action buttons
}

interface AlertAction {
  label: string;
  handler: () => void;
  style: "primary" | "secondary";
}
```

**Specific Alert Scenarios:**

1. **Single Course Generation:**
   - Info: "Generating sessions..." (during processing)
   - Success: "Created X sessions for [Course Name]" (on completion)
   - Warning: "X conflicts detected" (if conflicts exist, with "View Details" action)
   - Error: "Generation failed: [error message]" (on failure)

2. **Bulk Generation:**
   - Info: "Generating sessions for X courses..." (during processing)
   - Success: "Created X sessions across Y courses" (on completion)
   - Warning: "X conflicts detected across Y courses" (if conflicts exist)
   - Error: "Bulk generation failed: [error message]" (on failure)

3. **Manual Creation:**
   - Success: "Lecture session created successfully"
   - Warning: "Conflict detected: [details]" (with "Create Anyway" action)
   - Error: "Failed to create session: [validation error]"

4. **Validation Errors:**
   - Error: "End date must be after session start date"
   - Error: "Please select at least one timetable slot"
   - Error: "Please select at least one lecturer"

### Progress Indicators

For long-running operations (especially bulk generation):

- **Progress Bar**: Show percentage completion
- **Status Text**: "Processing course X of Y..."
- **Cancellation**: Provide cancel button for bulk operations
- **Results Preview**: Show running count of sessions created

### Conflict Display

When conflicts are detected:

- **Summary Card**: Show total conflicts by type (lecturer/venue)
- **Detailed List**: Expandable list showing each conflict with:
  - Conflict type (lecturer/venue)
  - Conflicting session details (date, time, course)
  - Affected resource (lecturer name or venue name)
- **Actions**: "Proceed Anyway" or "Cancel" buttons
- **Warning Icon**: Visual indicator for conflict severity
