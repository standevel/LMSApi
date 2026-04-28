# Bug Condition Exploration Results

## Test Execution Summary

**Date**: 2026-04-08  
**Test File**: `LMS-UI/src/app/components/TimetableManager/timetable-edit-overlay/timetable-edit-overlay.component.test.ts`  
**Status**: ✅ **TESTS FAILED AS EXPECTED** (Confirms bug exists)

## Bug Confirmation

The bug condition exploration test was written to encode the EXPECTED BEHAVIOR (multi-select checkboxes for lecturer assignment). When run against the UNFIXED code, all tests FAILED, which is the CORRECT outcome for this phase. The failures prove that the bug exists.

## Counterexamples Found

### 1. Wrong Data Structure

**Expected**: Component should use `selectedLecturerIds: Set<string>` to store multiple lecturer IDs  
**Actual**: Component uses `selectedLecturerId: string | undefined` (single value)  
**Evidence**: Line 33 in `timetable-edit-overlay.component.ts`

```typescript
selectedLecturerId: string | undefined; // ❌ Single string, not Set
```

### 2. Missing Initialization Logic

**Expected**: Component should initialize selected lecturers from BOTH `slot.lecturerId` AND `slot.coLecturerIds`  
**Actual**: Component only initializes from `slot.lecturerId`, ignoring co-lecturers  
**Evidence**: Lines 52-58 in `timetable-edit-overlay.component.ts`

```typescript
private initializeForm(): void {
  this.selectedLecturerId = this.slot.lecturerId ?? this.slot.lecturer?.id;  // ❌ Only main lecturer
  // Missing: No initialization of co-lecturers from slot.coLecturerIds
  this.selectedVenueId = this.slot.venueId;
  this.selectedDayOfWeek = this.slot.dayOfWeek;
  this.startTime = this.slot.startTime?.substring(0, 5) ?? '08:00';
  this.endTime = this.slot.endTime?.substring(0, 5) ?? '09:00';
  this.notes = this.slot.notes || '';
}
```

### 3. Missing Toggle Method

**Expected**: Component should have `toggleLecturer(id: string)` method for checkbox interaction  
**Actual**: Component has no such method (uses dropdown change handler instead)  
**Evidence**: No `toggleLecturer` method found in component class

### 4. Wrong UI Component in Template

**Expected**: Template should use checkboxes with `(change)="toggleLecturer(lecturer.id)"`  
**Actual**: Template uses `<select>` dropdown with `[(ngModel)]="selectedLecturerId"`  
**Evidence**: Lines 35-42 in `timetable-edit-overlay.component.html`

```html
<select
  [(ngModel)]="selectedLecturerId"
  (change)="onLecturerChange()"
  class="field-input"
>
  @for (option of lecturerOptions; track option.id) {
  <option [value]="option.id">
    {{ option.name }}{{ !option.available ? ' (Unavailable)' : '' }}
  </option>
  }
</select>
```

❌ Single-select dropdown, not multi-select checkboxes

### 5. Wrong Save Logic

**Expected**: Save should send `newLecturerId` (first) + `coLecturerIds` (rest) to backend  
**Actual**: Save only sends `newLecturerId`, losing all co-lecturers  
**Evidence**: Lines 95-103 in `timetable-edit-overlay.component.ts`

```typescript
const request: UpdateLectureTimetableSlotRequest = {
  newLecturerId: this.selectedLecturerId, // ❌ Only one lecturer
  // Missing: coLecturerIds array
  newVenueId: this.selectedVenueId,
  newStartTime: this.startTime,
  newEndTime: this.endTime,
  notes: this.notes || undefined,
};
```

## Test Results Detail

### Test 1: Data Structure Check

**Test**: `should use Set<string> for selectedLecturerIds (NOT single string)`  
**Result**: ❌ FAILED (Expected)  
**Reason**: Component has `selectedLecturerId` (string), not `selectedLecturerIds` (Set)

### Test 2: Initialization Check

**Test**: `should initialize selected lecturers from both lecturerId and coLecturerIds`  
**Result**: ❌ FAILED (Expected)  
**Reason**: `selectedLecturerIds` property doesn't exist; only `selectedLecturerId` is initialized

### Test 3: Toggle Method Check

**Test**: `should have toggleLecturer method for adding/removing lecturers`  
**Result**: ❌ FAILED (Expected)  
**Reason**: `toggleLecturer` method doesn't exist in component

### Test 4: Save Logic Check

**Test**: `should send all selected lecturers (main + co-lecturers) when saving`  
**Result**: ❌ FAILED (Expected)  
**Reason**: Update request doesn't include `coLecturerIds` field

### Test 5: Multi-Select Capability Check

**Test**: `should allow selecting and deselecting multiple lecturers`  
**Result**: ❌ FAILED (Expected)  
**Reason**: Cannot test multi-select because `toggleLecturer` method doesn't exist

## Root Cause Analysis

The root cause is confirmed to be:

1. **Wrong UI Component**: Uses `<select>` dropdown instead of checkbox list
2. **Wrong Data Structure**: Uses `string` instead of `Set<string>`
3. **Missing Logic**: No `toggleLecturer()` or `isLecturerSelected()` methods
4. **Incomplete Initialization**: Doesn't load co-lecturers from `slot.coLecturerIds`
5. **Incomplete Save**: Doesn't send `coLecturerIds` to backend

## Impact

- Users cannot assign multiple lecturers when editing timetable slots
- Existing co-lecturer assignments are hidden when opening edit overlay
- Saving changes loses all co-lecturer assignments
- Only the main lecturer can be modified

## Next Steps

The bug has been successfully confirmed through exploration testing. The test failures prove that:

- The bug exists in the unfixed code
- The expected behavior is clearly defined
- The test will pass once the fix is implemented

**Task 1 Status**: ✅ COMPLETE

- Bug condition exploration test written
- Test run on unfixed code
- Test failures documented (proves bug exists)
- Counterexamples identified and documented

The same test will be re-run after implementing the fix (Task 3.3) to verify the bug is resolved.
