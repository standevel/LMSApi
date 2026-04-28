# Requirements Document

## Introduction

The Lecture Session Management System provides comprehensive viewing and management capabilities for lecture sessions. It enables admins to oversee all sessions across the institution while allowing lecturers to manage their assigned sessions, including uploading materials, taking attendance, and tracking session completion.

## Glossary

- **Session_Management_System**: The lecture session management feature that provides viewing, filtering, and management capabilities
- **Admin**: A user with administrative privileges who can view and manage all lecture sessions
- **Lecturer**: A user assigned to teach specific lecture sessions
- **Lecture_Session**: A specific instance of a lecture occurring on a particular date and time
- **Session_Material**: A document or file (PDF, document, slide) uploaded for a lecture session
- **Session_Attendance**: A record tracking student attendance for a specific lecture session
- **Course_Offering**: A specific instance of a course being taught in an academic session
- **Academic_Session**: A semester or term during which courses are offered
- **Timetable_Slot**: A recurring scheduled time slot from which lecture sessions can be generated
- **Session_Type**: Classification indicating whether a session was created automatically from a timetable or manually

## Requirements

### Requirement 1: View Lecture Sessions

**User Story:** As an admin or lecturer, I want to view lecture sessions in a list format, so that I can see scheduled sessions at a glance

#### Acceptance Criteria

1. THE Session_Management_System SHALL display lecture sessions in a list view
2. FOR EACH Lecture_Session displayed, THE Session_Management_System SHALL show the course name, course code, session date, session time, venue, assigned lecturers, and session type
3. WHEN an admin views sessions, THE Session_Management_System SHALL display all sessions across all courses
4. WHEN a lecturer views sessions, THE Session_Management_System SHALL display only sessions where the lecturer is assigned
5. THE Session_Management_System SHALL sort sessions by date in ascending order by default

### Requirement 2: Filter Lecture Sessions

**User Story:** As an admin or lecturer, I want to filter lecture sessions by various criteria, so that I can find specific sessions quickly

#### Acceptance Criteria

1. WHERE the user selects a course offering filter, THE Session_Management_System SHALL display only sessions for the selected course offering
2. WHERE the user selects a date range filter, THE Session_Management_System SHALL display only sessions within the specified date range
3. WHERE the user selects a lecturer filter, THE Session_Management_System SHALL display only sessions assigned to the selected lecturer
4. WHERE the user selects an academic session filter, THE Session_Management_System SHALL display only sessions within the selected academic session
5. THE Session_Management_System SHALL allow multiple filters to be applied simultaneously
6. THE Session_Management_System SHALL update the session list within 500ms after filter changes

### Requirement 3: View Session Details

**User Story:** As an admin or lecturer, I want to view detailed information about a lecture session, so that I can see all relevant session data

#### Acceptance Criteria

1. WHEN a user selects a Lecture_Session, THE Session_Management_System SHALL display the session details view
2. THE Session_Management_System SHALL display the course name, course code, session date, start time, end time, venue, assigned lecturers, session type, and notes
3. WHERE the session has uploaded materials, THE Session_Management_System SHALL display a list of Session_Materials with file names and upload timestamps
4. WHERE attendance has been taken, THE Session_Management_System SHALL display attendance statistics including total students, present count, and absent count
5. THE Session_Management_System SHALL indicate whether the session is marked as completed

### Requirement 4: Edit Session Details (Admin Only)

**User Story:** As an admin, I want to edit lecture session details, so that I can correct scheduling errors or accommodate changes

#### Acceptance Criteria

1. WHEN an admin requests to edit a Lecture_Session, THE Session_Management_System SHALL display an edit form with current session data
2. THE Session_Management_System SHALL allow the admin to modify the session date, start time, end time, venue, assigned lecturers, and notes
3. WHEN the admin submits changes, THE Session_Management_System SHALL validate that the end time is after the start time
4. WHEN the admin submits changes, THE Session_Management_System SHALL validate that the session date falls within the academic session period
5. IF the modified session creates a lecturer conflict, THEN THE Session_Management_System SHALL display a warning message indicating the conflict
6. IF the modified session creates a venue conflict, THEN THE Session_Management_System SHALL display a warning message indicating the conflict
7. THE Session_Management_System SHALL save the changes and update the session list within 1 second

### Requirement 5: Delete Sessions (Admin Only)

**User Story:** As an admin, I want to delete lecture sessions, so that I can remove cancelled or erroneous sessions

#### Acceptance Criteria

1. WHEN an admin requests to delete a Lecture_Session, THE Session_Management_System SHALL display a confirmation dialog
2. THE Session_Management_System SHALL indicate in the confirmation dialog whether the session has associated materials or attendance records
3. WHEN the admin confirms deletion, THE Session_Management_System SHALL remove the Lecture_Session and all associated Session_Materials and Session_Attendance records
4. THE Session_Management_System SHALL update the session list within 1 second after deletion
5. IF the deletion fails, THEN THE Session_Management_System SHALL display an error message

### Requirement 6: Upload Course Materials (Lecturer)

**User Story:** As a lecturer, I want to upload course materials for my sessions, so that students can access learning resources

#### Acceptance Criteria

1. WHEN a lecturer views a session they are assigned to, THE Session_Management_System SHALL display an upload materials option
2. THE Session_Management_System SHALL allow the lecturer to select one or more files to upload
3. THE Session_Management_System SHALL accept PDF, DOC, DOCX, PPT, and PPTX file formats
4. THE Session_Management_System SHALL reject files larger than 50MB
5. WHEN the lecturer uploads files, THE Session_Management_System SHALL create Session_Material records linked to the Lecture_Session
6. THE Session_Management_System SHALL display uploaded materials with file names, file sizes, and upload timestamps
7. THE Session_Management_System SHALL allow the lecturer to delete materials they uploaded

### Requirement 7: Take Attendance (Lecturer)

**User Story:** As a lecturer, I want to take attendance for my sessions, so that I can track student participation

#### Acceptance Criteria

1. WHEN a lecturer views a session they are assigned to, THE Session_Management_System SHALL display a take attendance option
2. WHEN the lecturer selects take attendance, THE Session_Management_System SHALL display a list of all students enrolled in the course offering
3. FOR EACH student, THE Session_Management_System SHALL display the student name and student ID
4. THE Session_Management_System SHALL allow the lecturer to mark each student as present or absent
5. WHEN the lecturer submits attendance, THE Session_Management_System SHALL create Session_Attendance records for all students
6. WHERE attendance has already been taken, THE Session_Management_System SHALL allow the lecturer to modify attendance records
7. THE Session_Management_System SHALL save attendance changes within 1 second

### Requirement 8: Mark Session as Completed (Lecturer)

**User Story:** As a lecturer, I want to mark sessions as completed, so that I can track which sessions have been delivered

#### Acceptance Criteria

1. WHEN a lecturer views a session they are assigned to, THE Session_Management_System SHALL display a mark as completed option
2. THE Session_Management_System SHALL allow the lecturer to mark the session as completed
3. WHEN the lecturer marks a session as completed, THE Session_Management_System SHALL update the session status
4. THE Session_Management_System SHALL display a visual indicator for completed sessions in the session list
5. THE Session_Management_System SHALL allow the lecturer to unmark a completed session

### Requirement 9: Add Session Notes (Lecturer)

**User Story:** As a lecturer, I want to add notes to my sessions, so that I can record important information about what was covered

#### Acceptance Criteria

1. WHEN a lecturer views a session they are assigned to, THE Session_Management_System SHALL display an add notes option
2. THE Session_Management_System SHALL allow the lecturer to enter or edit text notes up to 2000 characters
3. WHEN the lecturer saves notes, THE Session_Management_System SHALL update the Lecture_Session notes field
4. THE Session_Management_System SHALL display the notes in the session details view
5. THE Session_Management_System SHALL save notes changes within 1 second

### Requirement 10: Session Type Indication

**User Story:** As an admin or lecturer, I want to see whether a session was created automatically or manually, so that I can understand the session origin

#### Acceptance Criteria

1. THE Session_Management_System SHALL display a session type indicator for each Lecture_Session
2. WHERE the session was created from a Timetable_Slot, THE Session_Management_System SHALL display "Automatic" as the session type
3. WHERE the session was created manually, THE Session_Management_System SHALL display "Manual" as the session type
4. THE Session_Management_System SHALL use distinct visual styling for automatic and manual sessions

### Requirement 11: View Attendance Statistics

**User Story:** As an admin or lecturer, I want to view attendance statistics for sessions, so that I can monitor student participation

#### Acceptance Criteria

1. WHERE attendance has been taken for a Lecture_Session, THE Session_Management_System SHALL calculate the total number of enrolled students
2. THE Session_Management_System SHALL calculate the number of students marked present
3. THE Session_Management_System SHALL calculate the number of students marked absent
4. THE Session_Management_System SHALL calculate the attendance percentage as (present count / total students) × 100
5. THE Session_Management_System SHALL display these statistics in the session details view

### Requirement 12: Access Control

**User Story:** As the system, I want to enforce role-based access control, so that users can only perform authorized actions

#### Acceptance Criteria

1. THE Session_Management_System SHALL allow admins to view all sessions regardless of assignment
2. THE Session_Management_System SHALL restrict lecturers to viewing only sessions where they are assigned
3. THE Session_Management_System SHALL allow only admins to edit session details
4. THE Session_Management_System SHALL allow only admins to delete sessions
5. THE Session_Management_System SHALL allow lecturers to upload materials, take attendance, mark completion, and add notes only for sessions where they are assigned
6. IF a user attempts an unauthorized action, THEN THE Session_Management_System SHALL return a 403 Forbidden response
