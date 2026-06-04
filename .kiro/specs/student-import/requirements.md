# Requirements Document

## Introduction

The Student Import feature enables Registry administrators at Wigwe University to bulk-onboard existing students into the LMS by uploading an Excel spreadsheet exported from the university's registration system. The spreadsheet ("Students per Educational Program") contains student personal data, academic program placement, registration status, and contact information. The feature parses each row, validates the data, creates LMS user accounts and student records, links students to the correct academic programs and levels, and produces a detailed import report. The UI follows the established dark theme (`#1a1a1a` background, `#f5c518` gold accent, white text) used across the Wigwe University LMS.

---

## Glossary

- **Importer**: The Student Import feature, comprising a backend import service, an API endpoint, and an Angular admin UI page.
- **Admin**: An authenticated LMS user with the `Admin` or `Registry` role who has permission to run student imports.
- **Spreadsheet**: An Excel file (`.xlsx` or `.xls`) in the "Students per Educational Program" format with the columns described in the Spreadsheet Column Mapping section below.
- **ImportRow**: A single data row from the Spreadsheet, representing one student record.
- **ImportJob**: A `BulkOperation` entity with `OperationType = "StudentImport"` that tracks the lifecycle of a single import file upload.
- **ImportResult**: The per-row outcome of an ImportJob, stored as JSON in `BulkOperation.ResultData`. Each entry records success or failure and a human-readable reason.
- **Student**: A `Student` entity in the LMS database representing an enrolled student.
- **AppUser**: An `AppUser` entity in the LMS database representing a system user account linked to a Student.
- **AcademicProgram**: An `AcademicProgram` entity matched by the `Program` column value.
- **AcademicLevel**: An `AcademicLevel` entity matched by the `Year-Semester` column value (e.g., "Year 1 - Semester 1").
- **AcademicSession**: The currently active `AcademicSession` used as the enrollment session for imported students.
- **RegistrationStatus**: The student's status from the spreadsheet — values include "Provisionally Accepted", "Registered", and others.
- **StudentNumber**: The value from the `Registration Number` column, stored as `Student.StudentNumber`.
- **DuplicateStudent**: A row whose `Registration Number`, `Username`, or `Contact e-mail` already exists in the LMS database.
- **Toast**: A transient, non-blocking notification displayed for success or error feedback in the UI.
- **Preview**: A dry-run analysis of the uploaded spreadsheet that shows what would be created, updated, or skipped without writing to the database.

### Spreadsheet Column Mapping

| Column Header | Field |
|---|---|
| First Name | `Student.FirstName` |
| Last Name | `Student.LastName` |
| Middle Name | `Student.MiddleName` |
| Father | Stored in import result metadata (informational) |
| Mother | Stored in import result metadata (informational) |
| Gender | `AppUser` profile metadata |
| Registration Number | `Student.StudentNumber` |
| Program | Matched to `AcademicProgram.Name` |
| Year-Semester | Matched to `AcademicLevel.Name` |
| Specialization | Stored as supplementary student metadata |
| Registration Status | Determines initial `Student.Status` |
| Status Color | Informational; not persisted |
| Annual Results Model | Stored as supplementary metadata |
| Curriculum | Matched to `Curriculum.Name` for enrollment |
| Application ID | Stored as external reference |
| Registration Status Change | Informational; logged |
| Count as reregistered | Informational; logged |
| Main Class | Stored as supplementary metadata |
| Mobile Phone | `Student.Phone` |
| Birthdate | Stored as supplementary metadata |
| Username | `AppUser.Username` |
| Contact e-mail | `Student.PersonalEmail` / `AppUser.Email` |

---

## Requirements

### Requirement 1: Upload Spreadsheet File

**User Story:** As a Registry admin, I want to upload the "Students per Educational Program" Excel file, so that the system can begin processing the student records it contains.

#### Acceptance Criteria

1. THE Importer SHALL provide a file upload UI at the route `/dashboard/registry/student-import` that is accessible only to authenticated users with the `Admin` or `Registry` role.
2. IF an authenticated user without the `Admin` or `Registry` role navigates to `/dashboard/registry/student-import`, THEN THE Importer SHALL redirect them to the dashboard home page.
3. WHEN an admin selects a file, THE Importer SHALL accept only files with the `.xlsx` or `.xls` extension and reject all other file types with an inline validation message indicating that only Excel files are supported.
4. IF the selected file exceeds 10 MB, THEN THE Importer SHALL display an inline validation message indicating the maximum file size limit before any upload attempt is made.
5. WHEN an admin selects a valid file within the size limit, THE Importer SHALL display the file name and size (in KB, rounded to 2 decimal places) before the admin confirms the upload.
6. WHEN the admin confirms the upload, THE Importer SHALL POST the file to the upload endpoint as `multipart/form-data` and, upon success, display the returned `ImportJob` ID to the admin.
7. IF the upload request fails due to a network or server error, THEN THE Importer SHALL display an error Toast indicating the upload failed and prompting the admin to retry.

---

### Requirement 2: Preview Import Before Committing

**User Story:** As a Registry admin, I want to see a preview of what will be imported before committing the operation, so that I can catch errors in the file and avoid creating bad data.

#### Acceptance Criteria

1. WHEN the file upload succeeds, THE Importer SHALL automatically request a preview and display a loading indicator while the preview is being fetched, then show the preview summary before the admin can confirm the import.
2. THE Importer SHALL display in the preview: total data row count, count of rows that will create new students (valid rows), count of rows identified as duplicates, and count of rows with validation errors. Duplicate rows and validation-error rows are mutually exclusive — a row classified as a duplicate is not also counted as a validation error.
3. WHEN a row references a `Program` value that does not match any `AcademicProgram` in the database, THE Importer SHALL flag that row as a validation error in the preview with a message that identifies the unrecognized program value.
4. WHEN a row references a `Year-Semester` value that does not match any `AcademicLevel` in the database, THE Importer SHALL flag that row as a validation error with a message that identifies the unrecognized level value.
5. WHEN the preview contains at least one valid row, THE Importer SHALL enable the "Confirm Import" button.
6. WHEN the preview contains zero valid rows, THE Importer SHALL disable the "Confirm Import" button and display a message indicating that no importable rows were found and prompting the admin to fix the file and re-upload.
7. IF the preview request fails, THEN THE Importer SHALL display an error Toast indicating the failure and clear the preview section so no stale data is shown.

---

### Requirement 3: Execute Student Import

**User Story:** As a Registry admin, I want to confirm and execute the import, so that the valid student records are created in the LMS.

#### Acceptance Criteria

1. WHEN the admin clicks "Confirm Import", THE Importer SHALL call the execute endpoint to start the import.
2. WHILE the import is executing, THE Importer SHALL display a progress indicator that updates after each record is processed, showing the count of processed records versus the total valid record count.
3. WHEN a valid ImportRow is processed, THE Import_Service SHALL atomically create one `AppUser` record (using the `Username` column as `AppUser.Username` and the `Contact e-mail` column as `AppUser.Email`) and one linked `Student` record within a single database transaction, so that no orphaned `AppUser` or `Student` record is persisted if either creation fails.
4. THE Import_Service SHALL populate the `Student` record with `StudentNumber`, `FirstName`, `LastName`, `MiddleName`, `Phone`, `PersonalEmail`, `AcademicProgramId`, `LevelId`, and `AcademicSessionId` derived from the ImportRow.
5. THE Import_Service SHALL map the `Registration Status` column value to `Student.Status` — "Registered" maps to `StudentStatus.Active`, and all other status values map to `StudentStatus.Inactive`.
6. WHEN a valid `Curriculum` value is present in an ImportRow and a matching `Curriculum` entity exists, THE Import_Service SHALL create a `ProgramEnrollment` record linking the student to the program, level, session, and curriculum. IF the `ProgramEnrollment` creation fails, THEN the `Student` and `AppUser` records SHALL still be committed and the row outcome SHALL be recorded as `Created` with a supplementary warning note in the `ImportResult`.
7. WHEN a row is a `DuplicateStudent`, THE Import_Service SHALL skip that row and record it in the `ImportResult` with outcome `Skipped` and a reason indicating duplication.
8. WHEN a row has validation errors identified during preview, THE Import_Service SHALL skip that row and record it in the `ImportResult` with outcome `Failed` and the specific validation reason.
9. IF a per-row database operation fails due to a transient error, THEN THE Import_Service SHALL record that row in the `ImportResult` with outcome `Failed` and the error reason, and SHALL continue processing the remaining rows without aborting the entire job.
10. WHEN the import completes, THE Import_Service SHALL update the `ImportJob` status to `Completed` and set `TotalRecords`, `ProcessedRecords`, and `FailedRecords` counts.
11. IF an unhandled exception occurs during import execution, THEN THE Import_Service SHALL mark the `ImportJob` status as `Failed` and store the error message in `BulkOperation.ErrorMessage`.

---

### Requirement 4: View Import Results Report

**User Story:** As a Registry admin, I want to see a detailed results report after the import finishes, so that I know exactly which students were created, which were skipped, and why.

#### Acceptance Criteria

1. WHEN the import job status becomes `Completed` or `Failed`, THE Importer SHALL display a results summary showing: total records processed, count with outcome `Created`, count with outcome `Skipped`, and count with outcome `Failed`.
2. WHEN the import job status becomes `Completed` or `Failed`, THE Importer SHALL render a results table showing each row's registration number, student full name, outcome (`Created`, `Skipped`, or `Failed`), and reason. For rows with outcome `Created`, the reason field SHALL be empty.
3. WHEN the admin clicks "Download Report", THE Importer SHALL generate and download a CSV file containing all rows from the results table.
4. WHEN the import completes and the count of rows with outcome `Failed` is greater than zero, THE Importer SHALL display a warning Toast indicating the number of failed rows and directing the admin to the report. The Toast SHALL auto-dismiss after 5 seconds.
5. WHEN the import completes and the count of rows with outcome `Created` equals the total processed count (i.e., `Failed` count is 0 and `Skipped` count is 0), THE Importer SHALL display a success Toast indicating the number of students created. The Toast SHALL auto-dismiss after 5 seconds.

---

### Requirement 5: Import Job History

**User Story:** As a Registry admin, I want to see a history of past import jobs, so that I can audit previous imports and re-view their results.

#### Acceptance Criteria

1. WHEN the import page loads, THE Importer SHALL request the list of past `StudentImport` jobs and display them ordered by date/time descending, showing the 50 most recent jobs.
2. THE Importer SHALL display for each job: file name, local date and time of submission, submitted-by admin name, status, and counts (total / succeeded / failed).
3. WHEN an admin clicks a past job row, THE Importer SHALL display that job's detailed results report (matching the format in Requirement 4) in a modal or expanded panel.
4. IF the job list request fails, THEN THE Importer SHALL display an error Toast indicating that the history could not be loaded.
5. WHEN the job list is empty, THE Importer SHALL display a message indicating no import jobs have been run yet and prompting the admin to upload a file.

---

### Requirement 6: Backend — Student Import API Endpoints

**User Story:** As a system, I want dedicated API endpoints for the student import workflow, so that the frontend can upload files, preview, execute, and query import jobs through a secure and consistent interface.

#### Acceptance Criteria

1. THE System SHALL expose `POST /api/registry/student-import/upload` that accepts a `multipart/form-data` request containing the Excel file, enforces a server-side 10 MB size limit (returning HTTP 400 if exceeded), stores the file using the existing `FileStorageService`, creates a `BulkOperation` record with `OperationType = "StudentImport"` and status `Uploaded`, and returns the `ImportJob` ID. IF `FileStorageService` fails, THEN the endpoint SHALL return HTTP 500 and no `BulkOperation` record SHALL be persisted.
2. THE System SHALL expose `POST /api/registry/student-import/{jobId}/preview` that parses the uploaded Excel file, validates each row against the rules in Requirements 7 and 8, and returns a `StudentImportPreviewResponse` containing: total row count, valid row count, duplicate row count, failed row count, and a list of per-row validation errors (row index, field name, error description). No data SHALL be written to the database during preview.
3. THE System SHALL expose `POST /api/registry/student-import/{jobId}/execute` that processes all valid rows from the uploaded file, performs database writes as specified in Requirement 3, updates the `BulkOperation` record, and returns the final `ImportJob` status. IF the job is not in `Uploaded` status when execute is called, THEN the endpoint SHALL return HTTP 409 Conflict.
4. THE System SHALL expose `GET /api/registry/student-import/jobs` that returns the 50 most recent `BulkOperation` records with `OperationType = "StudentImport"`, ordered by `CreatedAt` descending.
5. THE System SHALL expose `GET /api/registry/student-import/{jobId}/results` that returns the `ImportResult` entries for a job with status `Completed` or `Failed`. IF the job is still in progress, THEN the endpoint SHALL return HTTP 409 Conflict.
6. IF a request to any of these endpoints is made by an unauthenticated caller, THEN the endpoint SHALL return HTTP 401 Unauthorized. IF a request is made by an authenticated caller without the `Admin` or `Registry` role, THEN the endpoint SHALL return HTTP 403 Forbidden.
7. WHEN `{jobId}` does not correspond to a `BulkOperation` record, OR corresponds to a record with `OperationType` other than `"StudentImport"`, THEN THE System SHALL return HTTP 404.

---

### Requirement 7: Excel Parsing and Column Validation

**User Story:** As a system, I want to reliably parse the Excel spreadsheet and validate its contents, so that only structurally and semantically correct rows are imported.

#### Acceptance Criteria

1. THE Import_Service SHALL read the spreadsheet by matching column headers case-insensitively after trimming leading/trailing whitespace and collapsing internal runs of whitespace to a single space. A header that normalizes to a recognized column name SHALL be treated as that column.
2. WHEN a required column (`First Name`, `Last Name`, `Registration Number`, `Program`, `Year-Semester`, `Contact e-mail`, `Username`) is missing from the file after normalization, THE Import_Service SHALL reject the entire file and return a single error identifying the missing column. No rows SHALL be processed.
3. WHEN a row has an empty `Registration Number`, THE Import_Service SHALL flag that row as invalid with reason: "Registration Number is required."
4. WHEN a row has an empty `First Name` or `Last Name`, THE Import_Service SHALL flag that row as invalid with reason: "First Name and Last Name are required."
5. WHEN a row has a `Contact e-mail` value that does not contain exactly one `@` character with at least one character before it and at least one `.` after it, THE Import_Service SHALL flag that row as invalid with reason: "Contact e-mail is not a valid email address."
6. WHEN a row has an empty `Username`, THE Import_Service SHALL flag that row as invalid with reason: "Username is required."
7. THE Import_Service SHALL skip rows where all cells are empty or contain only whitespace, without flagging them as errors.
8. WHEN a row has multiple validation errors, THE Import_Service SHALL record all errors for that row in the `ImportResult`, not just the first one encountered.

---

### Requirement 8: Duplicate Detection

**User Story:** As a system, I want to detect and skip duplicate students during import, so that existing LMS accounts are not overwritten or duplicated.

#### Acceptance Criteria

1. WHEN a row's `Registration Number` matches an existing `Student.StudentNumber` in the database (case-insensitive), THE Import_Service SHALL classify that row as a `DuplicateStudent` and skip it.
2. WHEN a row's `Username` matches an existing `AppUser.Username` in the database (case-insensitive), THE Import_Service SHALL classify that row as a `DuplicateStudent` and skip it.
3. WHEN a row's `Contact e-mail` matches an existing `AppUser.Email` in the database (case-insensitive), THE Import_Service SHALL classify that row as a `DuplicateStudent` and skip it.
4. THE Import_Service SHALL complete all duplicate checks across all rows before beginning any insert operations, so that no student record is persisted until the full duplicate set is known.
5. WHEN a row matches on more than one duplicate field (e.g., both `Registration Number` and `Username` already exist), THE Import_Service SHALL classify that row as a single `DuplicateStudent` and count it once.
6. THE Importer SHALL report the total count of duplicate rows (counted as in criterion 5) separately from the count of failed rows in both the preview response and the final results report.

---

### Requirement 9: UI Styling and Interaction Standards

**User Story:** As a Registry admin, I want the student import page to match the Wigwe University LMS dark theme and provide clear feedback at every step, so that the experience is consistent and professional.

#### Acceptance Criteria

1. THE Importer SHALL apply the LMS dark theme token set (background, accent, and text colors) to all page elements on the student import page, consistent with the rest of the LMS dashboard.
2. WHILE any API request is in-flight, THE Importer SHALL display a loading spinner or skeleton overlay on the section awaiting the response.
3. WHEN the admin completes a file upload, preview request, or import execution, THE Importer SHALL display a Toast notification communicating the outcome (success or error) of that action.
4. WHEN a Toast is displayed, THE Importer SHALL automatically dismiss it after 4 seconds. The admin SHALL also be able to manually dismiss the Toast before the 4-second timeout.
5. WHILE an API request triggered by a button (upload, preview, confirm import) is in-flight, THE Importer SHALL disable that button to prevent duplicate submissions.
6. WHEN the in-flight API request completes (success or error), THE Importer SHALL re-enable the corresponding button.
7. THE Importer SHALL be responsive: on viewports 1024 px wide and above (desktop), content SHALL be laid out in a multi-column format; on viewports below 1024 px (tablet/mobile), content SHALL stack in a single-column format.
