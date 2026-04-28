# Bugfix Requirements Document

## Introduction

The timetable management system is designed to support assigning multiple lecturers to a course (one main lecturer and multiple co-lecturers). However, the lecturer multi-select functionality is broken in the timetable edit overlay, preventing users from assigning or modifying multiple lecturers for existing timetable slots. The edit overlay uses a single-select dropdown instead of the multi-select interface, making it impossible to assign co-lecturers when editing slots.

This bugfix addresses the broken multi-select functionality in the edit overlay, ensuring users can properly assign and manage multiple lecturers for timetable slots.

## Bug Analysis

### Current Behavior (Defect)

1.1 WHEN a user opens the edit overlay for a timetable slot THEN the system displays a single-select dropdown for lecturer assignment instead of a multi-select interface

1.2 WHEN a user attempts to assign multiple lecturers (main + co-lecturers) in the edit overlay THEN the system only allows selection of one lecturer

1.3 WHEN a timetable slot already has multiple lecturers assigned and a user opens the edit overlay THEN the system only shows the main lecturer in the single-select dropdown, hiding co-lecturer assignments

1.4 WHEN a user saves changes in the edit overlay THEN the system only sends a single lecturerId to the backend, losing any co-lecturer assignments

### Expected Behavior (Correct)

2.1 WHEN a user opens the edit overlay for a timetable slot THEN the system SHALL display a multi-select interface (checkboxes) for lecturer assignment, consistent with the create overlay

2.2 WHEN a user assigns multiple lecturers in the edit overlay THEN the system SHALL allow selection of one main lecturer and multiple co-lecturers

2.3 WHEN a timetable slot already has multiple lecturers assigned and a user opens the edit overlay THEN the system SHALL display all assigned lecturers (main + co-lecturers) with appropriate checkboxes selected

2.4 WHEN a user saves changes in the edit overlay THEN the system SHALL send the main lecturerId and an array of coLecturerIds to the backend, preserving all lecturer assignments

### Unchanged Behavior (Regression Prevention)

3.1 WHEN a user creates a new timetable slot using the create overlay THEN the system SHALL CONTINUE TO provide multi-select functionality for lecturer assignment with checkboxes

3.2 WHEN a user assigns a single lecturer (no co-lecturers) in the edit overlay THEN the system SHALL CONTINUE TO save and display that single lecturer correctly

3.3 WHEN the backend receives an update request THEN the system SHALL CONTINUE TO store the main lecturer in the LecturerId field and co-lecturers in the CoLecturersJson field

3.4 WHEN a user cancels the edit overlay THEN the system SHALL CONTINUE TO discard changes and close the overlay without saving
