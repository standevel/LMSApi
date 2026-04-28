# Implementation Plan: Student Fee Payment Portal

## Overview

Implement the student-facing fee payment portal in two layers: thin backend additions (one new endpoint + two ownership guards) and a new Angular standalone component with Paystack, Hydrogen, and manual-receipt payment flows.

## Tasks

- [ ] 1. Add `GetMyBillEndpoint` to the backend
  - [x] 1.1 Create `GetMyBillEndpoint` in `LMS-API/Endpoints/Fees/StudentBillEndpoints.cs`
    - Inherit `ApiEndpointWithoutRequest<StudentBillResponse>`
    - Route: `GET /api/fees/my-bill`; restrict to `Student` role only
    - Bind optional `sessionId` query param via `Query<Guid?>("sessionId", isRequired: false)`
    - Resolve `studentId` from `HttpContext.Items["CurrentUserId"]` (set by `UserProvisioningMiddleware`)
    - If `sessionId` is null, inject `IAcademicSessionRepository` and call `GetActiveAsync(ct)`; return 400 with `"No active session found."` if none exists
    - Delegate to `IFeeService.GetStudentBillAsync(studentId, sessionId)`; return 404 if null
    - Map result to `StudentBillResponse` using the same projection as `GetStudentBillEndpoint`
    - _Requirements: 8.1, 8.2, 8.3, 8.4, 8.5_

  - [ ]\* 1.2 Write property test for `GetMyBillEndpoint` — Property 11
    - **Property 11: My-bill endpoint returns the caller's own bill**
    - **Validates: Requirements 8.2**
    - Use FsCheck: for any authenticated student, assert `response.StudentId == callerIdentity`

  - [ ]\* 1.3 Write unit tests for `GetMyBillEndpoint`
    - Test: returns 400 when no active session and no `sessionId` param
    - Test: returns 404 when bill does not exist for the resolved student + session
    - Test: returns 200 with correct `StudentBillResponse` on happy path
    - _Requirements: 8.3, 8.4, 8.5_

- [ ] 2. Add ownership guards to existing endpoints
  - [ ] 2.1 Add ownership check to `GetStudentBillEndpoint` in `LMS-API/Endpoints/Fees/StudentBillEndpoints.cs`
    - After resolving `studentId` from route, read `callerId` from `HttpContext.Items["CurrentUserId"]`
    - If the caller has only the `Student` role (check `User.IsInRole("Student")` and not any of Admin/Finance/SuperAdmin/Registry), compare `studentId != callerId`; return 403 if mismatch
    - _Requirements: 7.1, 7.2, 7.3_

  - [ ] 2.2 Add ownership check to `GetPaymentHistoryEndpoint` in `LMS-API/Endpoints/Fees/FeePaymentEndpoints.cs`
    - Same pattern: resolve `callerId` from `HttpContext.Items["CurrentUserId"]`; if student-only caller and `studentId != callerId`, return 403
    - _Requirements: 7.4, 7.5_

  - [ ]\* 2.3 Write property test for ownership guards — Property 10
    - **Property 10: Student ownership — cross-student access is always forbidden**
    - **Validates: Requirements 7.2, 7.5**
    - Use FsCheck: for any two distinct student GUIDs, assert both endpoints return 403 when caller ID ≠ route ID

- [ ] 3. Checkpoint — Ensure all backend tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 4. Add `getMyBill()` to the frontend `FeeService`
  - [ ] 4.1 Add `getMyBill(sessionId?: string): Observable<StudentBill>` to `LMS-UI/src/app/services/fee.service.ts`
    - Build URL: `${this.apiUrl}/my-bill` with optional `?sessionId=` query param
    - Use `this.http.get<ApiResponse<StudentBill>>(url).pipe(map(res => res.data))`
    - _Requirements: 1.1, 8.1_

- [x] 5. Create `StudentFeePortal` component skeleton and route
  - [x] 5.1 Create `LMS-UI/src/app/components/Dashboard/Student/Fees/StudentFeePortal.component.ts`
    - Standalone Angular component exported as `StudentFeesComponent` (matches existing route import in `app.routes.ts`)
    - Declare signals: `bill`, `payments`, `loading`, `paying`, `toast`, `selectedFile`, `manualRef`, `activeSession`
    - Inject: `FeeService`, `AuthStore`, `AcademicSessionService`, `Router`, `ActivatedRoute`
    - Implement `ngOnInit()`: call `loadData()` then `handleCallbackParams()`
    - Implement `loadData()`: parallel `forkJoin` of `feeService.getMyBill()` and `feeService.getPaymentHistory(userId)`, set `loading` signal
    - Implement `showToast(message, type)`: set `toast` signal, auto-clear after 4 s via `setTimeout`
    - Render a minimal inline template with dark theme (`#1a1a1a` bg, `#f5c518` gold accent, white text): billing card, payment history table, payment action section, toast overlay
    - _Requirements: 1.1, 1.2, 9.1, 9.5_

  - [ ]\* 5.2 Write property test for bill rendering — Property 1
    - **Property 1: Bill fields are always rendered**
    - **Validates: Requirements 1.2, 10.2**
    - Use fast-check: for any valid `StudentBill` record, assert session name, totalAmount, amountPaid, and balance appear in the rendered template

- [ ] 6. Implement bill display logic and status states
  - [x] 6.1 Implement conditional rendering for bill status in `StudentFeePortal.component.ts`
    - `Paid` status: apply green visual state, hide payment action buttons
    - `Waived` status: show "Waived" badge, hide payment action buttons
    - `PartiallyPaid` status: show remaining balance as payable amount
    - `LateFeeApplied === true`: show late-fee warning banner with `lateFeeTotal`
    - No bill (404): show "Your fee bill has not been generated yet. Please contact the Finance office."
    - No active session (400): show "No active academic session found. Please contact the Registry."
    - Network error: call `showToast('Failed to load your fee bill. Please try again.', 'error')`
    - _Requirements: 1.2, 1.3, 1.4, 1.5, 1.6, 1.7, 1.8, 10.1, 10.2_

  - [ ]\* 6.2 Write property test for terminal status hiding payment actions — Property 2
    - **Property 2: Terminal bill statuses hide payment actions**
    - **Validates: Requirements 1.3, 1.4**
    - Use fast-check: for any bill with status `'Paid'` or `'Waived'`, assert payment buttons are absent from DOM

  - [ ]\* 6.3 Write property test for late fee banner — Property 3
    - **Property 3: Late fee banner appears when applied**
    - **Validates: Requirements 1.5**
    - Use fast-check: for any bill where `lateFeeApplied === true`, assert late-fee warning element containing `lateFeeTotal` is present

  - [ ]\* 6.4 Write property test for balance invariant — Property 12
    - **Property 12: Balance equals total minus paid**
    - **Validates: Requirements 1.2, 10.1, 10.3**
    - Use fast-check: for any `StudentBill`, assert `bill.balance === bill.totalAmount - bill.amountPaid`

- [ ] 7. Implement payment history table
  - [x] 7.1 Render payment history in `StudentFeePortal.component.ts`
    - Display each `FeePayment` row with: `paidAt` date, `paymentMethod`, `amount`, `status` badge
    - For `status === 'Rejected'`: show `rejectionReason` beneath the status badge
    - Empty state: "No transactions recorded yet."
    - History fetch error: call `showToast('Failed to load payment history.', 'error')`
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5_

  - [ ]\* 7.2 Write property test for payment history rendering — Property 4
    - **Property 4: All payment records are rendered with required fields**
    - **Validates: Requirements 2.1, 2.2**
    - Use fast-check: for any non-empty `FeePayment[]`, assert one row per payment with paidAt, paymentMethod, amount, and status present

  - [ ]\* 7.3 Write property test for rejected payment reason — Property 5
    - **Property 5: Rejected payments display rejection reason**
    - **Validates: Requirements 2.3**
    - Use fast-check: for any payment with `status === 'Rejected'`, assert `rejectionReason` text is present in the rendered row

- [ ] 8. Implement gateway callback handling
  - [x] 8.1 Implement `handleCallbackParams()` in `StudentFeePortal.component.ts`
    - Inject `ActivatedRoute`; read `queryParams` snapshot
    - If `reference` or `trxref` present → Paystack callback
    - If `tx_ref` or `transactionRef` present → Hydrogen callback
    - If `status === 'cancelled'` or `status === 'failed'` → `showToast('Payment was not completed. Please try again.', 'error')`
    - Otherwise (success or absent status) → `showToast('Payment received. Your bill will be updated shortly.', 'success')`, schedule `loadData()` after 3 s
    - Clean URL via `window.history.replaceState({}, '', location.pathname)` after processing
    - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5, 6.6_

  - [ ]\* 8.2 Write property test for callback classification — Property 9
    - **Property 9: Gateway callback parameters are correctly classified**
    - **Validates: Requirements 6.2, 6.3**
    - Use fast-check: for params containing `reference`/`trxref` assert Paystack classification; for `tx_ref`/`transactionRef` assert Hydrogen classification; assert mutual exclusivity for non-overlapping sets

- [ ] 9. Implement Paystack payment flow
  - [x] 9.1 Implement `payWithPaystack()` in `StudentFeePortal.component.ts`
    - Set `paying = true`, disable all payment controls
    - Call `feeService.initiateGatewayPayment({ gateway: 'Paystack', studentFeeRecordId: bill().id, amount: bill().balance, callbackUrl: location.origin + location.pathname, customerEmail: authStore.user().email, customerName: authStore.user().displayName })`
    - On success: redirect via `window.location.href = response.checkoutUrl`
    - On error: `showToast('Failed to initiate Paystack payment. Please try again.', 'error')`, set `paying = false`
    - _Requirements: 3.1, 3.2, 3.3, 3.4_

  - [ ]\* 9.2 Write property test for in-flight state disabling controls — Property 6
    - **Property 6: In-flight state disables all payment controls**
    - **Validates: Requirements 3.3, 4.3, 5.8, 9.7**
    - Use fast-check: when `paying === true`, assert all payment buttons have `disabled` attribute

- [ ] 10. Implement Hydrogen payment flow
  - [x] 10.1 Implement `payWithHydrogen()` in `StudentFeePortal.component.ts`
    - Same pattern as `payWithPaystack()` but `gateway: 'Hydrogen'`
    - On error: `showToast('Failed to initiate Hydrogen payment. Please try again.', 'error')`, set `paying = false`
    - _Requirements: 4.1, 4.2, 4.3, 4.4_

- [ ] 11. Implement manual receipt upload flow
  - [x] 11.1 Implement `submitManualPayment()` and file selection in `StudentFeePortal.component.ts`
    - File input: `accept=".pdf,.jpg,.jpeg,.png"`, on change set `selectedFile` signal and display filename
    - Validate: if `!selectedFile()` or `!manualRef().trim()` → do nothing (no API call)
    - Hide entire manual upload section when `bill().balance === 0`
    - On valid submit: set `paying = true`, build `FormData` with `studentFeeRecordId`, `amount` (balance), `referenceNumber`, and receipt file
    - Call `feeService.recordManualPayment(formData)`
    - On success: `showToast('Receipt submitted. The Finance team will verify your payment within 48 hours.', 'success')`, reload payment history, reset `selectedFile` and `manualRef`
    - On error: `showToast('Failed to submit receipt. Please check your connection and try again.', 'error')`
    - Always set `paying = false` after response
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 5.6, 5.7, 5.8, 5.9_

  - [ ]\* 11.2 Write property test for manual form validation — Property 7
    - **Property 7: Manual payment form requires both file and reference**
    - **Validates: Requirements 5.4**
    - Use fast-check: for any state where file is null or reference is empty/whitespace, assert `recordManualPayment` is never called

  - [ ]\* 11.3 Write property test for zero-balance hiding upload section — Property 8
    - **Property 8: Zero-balance hides manual upload section**
    - **Validates: Requirements 5.9**
    - Use fast-check: for any bill where `balance === 0`, assert manual upload section is absent from DOM

- [ ] 12. Final checkpoint — Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- The route `student/fees` already exists in `app.routes.ts` — the component must be exported as `StudentFeesComponent`
- `context.Items["CurrentUserId"]` is of type `Guid` (set by `UserProvisioningMiddleware`)
- Ownership check: a caller is "student-only" when `User.IsInRole("Student")` is true and none of `SuperAdmin`, `Admin`, `Finance`, `Registry` roles are present
- Property tests use fast-check (frontend) and FsCheck/xUnit (backend); each must include the comment tag `// Feature: student-fee-payment-portal, Property {N}: {text}`
