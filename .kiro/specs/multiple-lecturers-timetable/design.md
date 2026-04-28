# Multiple Lecturers Timetable Bugfix Design

## Overview

The timetable edit overlay (`timetable-edit-overlay.component.ts`) incorrectly uses a single-select dropdown for lecturer assignment instead of multi-select checkboxes. This prevents users from assigning or modifying multiple lecturers (main + co-lecturers) when editing existing timetable slots. The fix involves replacing the single-select dropdown with a multi-select checkbox interface, consistent with the create overlay and the working implementation in `Overlay/TimetableEditOverlay.component.ts`.

## Glossary

- **Bug_Condition (C)**: The condition that triggers the bug - when the edit overlay displays a single-select dropdown instead of multi-select checkboxes for lecturer assignment
- **Property (P)**: The desired behavior - the edit overlay should display multi-select checkboxes allowing selection of multiple lecturers (main + co-lecturers)
- **Preservation**: Existing functionality that must remain unchanged - single lecturer assignment, create overlay behavior, backend data storage, cancel functionality
- **TimetableEditOverlayComponent**: The component in `LMS-UI/src/app/components/TimetableManager/timetable-edit-overlay/timetable-edit-overlay.component.ts` that displays the edit overlay
- **selectedLecturerId**: The current single-select property that stores only one lecturer ID
- **selectedLecturerIds**: The required Set<string> property that should store multiple lecturer IDs
- **coLecturerIds**: The array of co-lecturer IDs that should be sent to the backend alongside the main lecturerId

## Bug Details

### Bug Condition

The bug manifests when a user opens the edit overlay for a timetable slot. The component uses a single-select dropdown (`<select>` with `[(ngModel)]="selectedLecturerId"`) instead of multi-select checkboxes, making it impossible to assign or view multiple lecturers.

**Formal Specification:**

```
FUNCTION isBugCondition(input)
  INPUT: input of type UserAction (opening edit overlay)
  OUTPUT: boolean

  RETURN input.action == "OPEN_EDIT_OVERLAY"
         AND editOverlayComponent.usesDropdown == true
         AND editOverlayComponent.allowsMultipleSelection == false
         AND slot.hasMultipleLecturers == true OR user.wantsToAssignMultipleLecturers == true
END FUNCTION
```

### Examples

- User opens edit overlay for a slot with 1 main lecturer and 2 co-lecturers → Only the main lecturer appears in the dropdown, co-lecturers are hidden
- User opens edit overlay and wants to add a co-lecturer → Can only select one lecturer from the dropdown, cannot add additional lecturers
- User opens edit overlay for a slot with only 1 lecturer → Dropdown shows that lecturer, but user cannot add co-lecturers
- User saves changes after selecting a different lecturer → Only the newly selected lecturer is saved, any existing co-lecturers are lost

## Expected Behavior

### Preservation Requirements

**Unchanged Behaviors:**

- The create overlay (in `TimetableManager.component.ts`) must continue to use multi-select checkboxes for lecturer assignment
- Single lecturer assignment (no co-lecturers) must continue to work correctly in both create and edit overlays
- Backend API must continue to receive `lecturerId` (main) and `coLecturerIds` (array) in the update request
- Cancel button must continue to discard changes and close the overlay without saving
- Delete functionality must continue to work as expected
- All other form fields (time, venue, notes) must continue to function correctly

**Scope:**
All inputs and interactions that do NOT involve the lecturer selection UI should be completely unaffected by this fix. This includes:

- Time range selection (start time, end time)
- Venue input field
- Notes textarea
- Save button behavior (validation, API calls)
- Cancel button behavior
- Delete button behavior
- Overlay open/close animations

## Hypothesized Root Cause

Based on the bug description and code analysis, the root cause is:

1. **Wrong UI Component**: The edit overlay uses a `<select>` dropdown element instead of checkbox list
   - Template has: `<select [(ngModel)]="selectedLecturerId">`
   - Should have: Checkbox list with `(change)="toggleLecturer(lecturer.id)"`

2. **Wrong Data Structure**: The component uses a single string property instead of a Set
   - Component has: `selectedLecturerId: string | undefined`
   - Should have: `selectedLecturerIds = new Set<string>()`

3. **Missing Initialization Logic**: The component doesn't populate selected lecturers from `slot.coLecturerIds`
   - Current: Only initializes `selectedLecturerId` from `slot.lecturerId`
   - Should: Initialize Set with both `slot.lecturerId` and all IDs from `slot.coLecturerIds`

4. **Wrong Update Request**: The component sends only `newLecturerId` instead of `newLecturerId` + `coLecturerIds`
   - Current: `{ newLecturerId: this.selectedLecturerId, ... }`
   - Should: `{ newLecturerId: primaryId, coLecturerIds: coIds, ... }`

## Correctness Properties

Property 1: Bug Condition - Multi-Select Lecturer Assignment in Edit Overlay

_For any_ user action where the edit overlay is opened for a timetable slot (regardless of current lecturer assignments), the edit overlay SHALL display a multi-select checkbox interface for lecturer assignment, allowing the user to select one or more lecturers, and SHALL correctly initialize the checkboxes to reflect all currently assigned lecturers (main + co-lecturers).

**Validates: Requirements 2.1, 2.2, 2.3**

Property 2: Preservation - Non-Lecturer Fields and Single Lecturer Assignment

_For any_ user interaction that does NOT involve the lecturer selection UI (time fields, venue, notes, cancel, delete) OR involves assigning only a single lecturer, the fixed edit overlay SHALL produce exactly the same behavior as the original overlay, preserving all existing functionality for non-lecturer interactions and single-lecturer assignments.

**Validates: Requirements 3.1, 3.2, 3.3, 3.4**

## Fix Implementation

### Changes Required

The fix should mirror the working implementation in `Overlay/TimetableEditOverlay.component.ts`.

**File**: `LMS-UI/src/app/components/TimetableManager/timetable-edit-overlay/timetable-edit-overlay.component.ts`

**Component Class**: `TimetableEditOverlayComponent`

**Specific Changes**:

1. **Replace Data Structure**: Change from single string to Set
   - Remove: `selectedLecturerId: string | undefined;`
   - Add: `selectedLecturerIds = new Set<string>();`

2. **Update Initialization Logic**: Populate Set with all assigned lecturers

   ```typescript
   // In initializeForm() or ngOnInit()
   this.selectedLecturerIds = new Set<string>();
   if (this.slot.lecturerId) {
     this.selectedLecturerIds.add(this.slot.lecturerId);
   }
   (this.slot.coLecturerIds || []).forEach((id) => {
     this.selectedLecturerIds.add(id);
   });
   ```

3. **Add Toggle Method**: Implement checkbox toggle logic

   ```typescript
   toggleLecturer(id: string): void {
     if (this.selectedLecturerIds.has(id)) {
       this.selectedLecturerIds.delete(id);
     } else {
       this.selectedLecturerIds.add(id);
     }
   }

   isLecturerSelected(id: string): boolean {
     return this.selectedLecturerIds.has(id);
   }
   ```

4. **Update Save Logic**: Split selected IDs into main + co-lecturers

   ```typescript
   // In updateSlot()
   const allLecturerIds = Array.from(this.selectedLecturerIds);
   const primaryId = allLecturerIds[0] || undefined;
   const coIds = allLecturerIds.slice(1);

   const request: UpdateLectureTimetableSlotRequest = {
     newLecturerId: primaryId,
     coLecturerIds: coIds.length > 0 ? coIds : [],
     // ... other fields
   };
   ```

5. **Update Template**: Replace dropdown with checkbox list
   - Remove the `<select>` element and its options
   - Add checkbox list structure matching the working overlay

**File**: `LMS-UI/src/app/components/TimetableManager/timetable-edit-overlay/timetable-edit-overlay.component.html`

**Template Changes**:

- Replace the lecturer `<select>` dropdown section with a checkbox list
- Use `@for` to iterate over `lecturerOptions`
- Each item should have a checkbox with `(change)="toggleLecturer(lecturer.id)"`
- Use `[checked]="isLecturerSelected(lecturer.id)"` for checkbox state
- Add visual styling to match the working overlay (selected state, checkmark icon)

## Testing Strategy

### Validation Approach

The testing strategy follows a two-phase approach: first, surface counterexamples that demonstrate the bug on unfixed code (single-select dropdown prevents multi-lecturer assignment), then verify the fix works correctly (multi-select checkboxes allow multiple lecturers) and preserves existing behavior (single lecturer assignment, other fields, cancel/delete).

### Exploratory Bug Condition Checking

**Goal**: Surface counterexamples that demonstrate the bug BEFORE implementing the fix. Confirm that the edit overlay uses a single-select dropdown and cannot handle multiple lecturers.

**Test Plan**: Manually test the edit overlay on the UNFIXED code by opening it for slots with multiple lecturers and attempting to assign multiple lecturers. Observe that only one lecturer can be selected and co-lecturers are lost.

**Test Cases**:

1. **Multi-Lecturer Slot Test**: Open edit overlay for a slot with 1 main + 2 co-lecturers (will fail - only shows main lecturer in dropdown)
2. **Add Co-Lecturer Test**: Open edit overlay and attempt to add a co-lecturer to a single-lecturer slot (will fail - dropdown only allows one selection)
3. **Save Multi-Lecturer Test**: Select a different lecturer in dropdown and save (will fail - co-lecturers are lost, only new lecturer is saved)
4. **UI Component Test**: Inspect the DOM to confirm a `<select>` element is used instead of checkboxes (will confirm root cause)

**Expected Counterexamples**:

- Edit overlay displays `<select>` dropdown instead of checkbox list
- Only one lecturer can be selected at a time
- Co-lecturers are not displayed when opening edit overlay for multi-lecturer slots
- Saving changes loses co-lecturer assignments
- Possible root cause: Wrong UI component (dropdown vs checkboxes), wrong data structure (string vs Set)

### Fix Checking

**Goal**: Verify that for all inputs where the bug condition holds (opening edit overlay, assigning multiple lecturers), the fixed component produces the expected behavior (displays checkboxes, allows multi-select, saves all lecturers).

**Pseudocode:**

```
FOR ALL input WHERE isBugCondition(input) DO
  result := openEditOverlay_fixed(input)
  ASSERT result.usesCheckboxes == true
  ASSERT result.allowsMultipleSelection == true
  ASSERT result.displaysAllAssignedLecturers == true
  ASSERT result.savesAllSelectedLecturers == true
END FOR
```

### Preservation Checking

**Goal**: Verify that for all inputs where the bug condition does NOT hold (single lecturer assignment, non-lecturer fields, cancel/delete actions), the fixed component produces the same result as the original component.

**Pseudocode:**

```
FOR ALL input WHERE NOT isBugCondition(input) DO
  ASSERT editOverlay_original(input) = editOverlay_fixed(input)
END FOR
```

**Testing Approach**: Property-based testing is recommended for preservation checking because:

- It generates many test cases automatically across the input domain (different slot configurations, field values)
- It catches edge cases that manual unit tests might miss (empty fields, boundary times, special characters)
- It provides strong guarantees that behavior is unchanged for all non-buggy inputs

**Test Plan**: Observe behavior on UNFIXED code first for single-lecturer assignment and other fields, then write property-based tests capturing that behavior.

**Test Cases**:

1. **Single Lecturer Assignment Preservation**: Assign only one lecturer in edit overlay, verify it saves correctly (should work on both unfixed and fixed code)
2. **Time Fields Preservation**: Modify start/end time in edit overlay, verify changes save correctly
3. **Venue Field Preservation**: Modify venue in edit overlay, verify changes save correctly
4. **Notes Field Preservation**: Modify notes in edit overlay, verify changes save correctly
5. **Cancel Preservation**: Open edit overlay, make changes, click cancel, verify no changes are saved
6. **Delete Preservation**: Open edit overlay, click delete, verify slot is deleted

### Unit Tests

- Test `toggleLecturer()` method adds/removes lecturers from Set correctly
- Test `isLecturerSelected()` method returns correct boolean for selected/unselected lecturers
- Test initialization logic populates `selectedLecturerIds` from `slot.lecturerId` and `slot.coLecturerIds`
- Test save logic splits `selectedLecturerIds` into `primaryId` and `coIds` correctly
- Test edge case: no lecturers selected (should handle gracefully)
- Test edge case: only one lecturer selected (should set `primaryId`, empty `coIds`)

### Property-Based Tests

- Generate random slot configurations (0-5 lecturers) and verify edit overlay displays all assigned lecturers with correct checkbox states
- Generate random lecturer selections (1-10 lecturers) and verify save logic correctly splits into main + co-lecturers
- Generate random field values (time, venue, notes) and verify preservation of non-lecturer field behavior
- Test that for any slot with N lecturers, opening edit overlay and saving without changes preserves all N lecturers

### Integration Tests

- Test full flow: open edit overlay for multi-lecturer slot → verify all lecturers displayed → add new co-lecturer → save → verify backend receives correct data
- Test full flow: open edit overlay for single-lecturer slot → add 2 co-lecturers → save → verify all 3 lecturers are saved
- Test full flow: open edit overlay for multi-lecturer slot → remove 1 co-lecturer → save → verify remaining lecturers are saved
- Test visual feedback: verify checkboxes display selected state correctly, verify checkmark icon appears for selected lecturers
