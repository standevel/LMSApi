# Requirements Document

## Introduction

The Student Fee Payment Portal is a feature within the Wigwe University LMS that gives enrolled, authenticated students a self-service interface to view their fee bill for the current academic session and settle outstanding balances via Paystack or Hydrogen payment gateways, or by uploading a manual bank-teller receipt. The portal surfaces real-time billing data from the existing `StudentFeeRecord` / `FeePayment` backend, handles gateway redirect callbacks, and provides full toast-based feedback for every user action. The UI follows the established dark theme (`#1a1a1a` background, `#f5c518` gold accent, white text) used across the Wigwe University LMS.

---

## Glossary

- **Portal**: The Student Fee Payment Portal Angular component at route `/dashboard/student/fees`.
- **Student**: An authenticated LMS user with the `Student` role, identified by their Azure AD / JWT `objectId`.
- **Bill**: A `StudentFeeRecord` entity representing the student's total fee obligation, amount paid, and outstanding balance for a specific academic session.
- **Session**: An `AcademicSession` entity; the active session is the one where `isActive === true`.
- **Payment**: A `FeePayment` entity recording a single payment attempt (gateway or manual).
- **Gateway**: An online payment provider — either Paystack or Hydrogen — integrated via `InitiateGatewayPaymentAsync`.
- **GatewayReference**: A unique transaction identifier returned by the payment gateway and stored on the `FeePayment` record.
- **CallbackUrl**: The URL the gateway redirects the student to after a payment attempt.
- **ManualPayment**: A payment submitted by uploading a bank-teller receipt PDF/image alongside a bank reference number, pending Finance team confirmation.
- **Toast**: A transient, non-blocking notification displayed at the bottom-right of the screen for success or error feedback.
- **FeeRecordStatus**: Enum — `Outstanding`, `PartiallyPaid`, `Paid`, `Waived`.
- **PaymentStatus**: Enum — `Pending`, `Confirmed`, `Rejected`, `Failed`.
- **Balance**: `Bill.TotalAmount − Bill.AmountPaid`; the amount still owed by the student.
- **LateFee**: An additional surcharge applied to a Bill when payment is not made by the due date.
- **BursaryEmail**: `bursary@wigweuniversity.edu.ng` — the official contact for payment discrepancies.

---

## Requirements

### Requirement 1: Display Current Session Fee Bill

**User Story:** As a student, I want to see my fee bill for the current academic session, so that I know exactly how much I owe and what I have already paid.

#### Acceptance Criteria

1. WHEN the Portal loads, THE Portal SHALL fetch the authenticated student's `objectId` from the auth service and the active `AcademicSession` from the session service, then call `GET /api/fees/bill/{studentId}/{sessionId}` to retrieve the Bill.
2. WHEN the Bill is successfully retrieved, THE Portal SHALL display the session name, total obligation (`TotalAmount`), total remitted (`AmountPaid`), and outstanding balance (`Balance`) on the billing card.
3. WHEN the Bill status is `Paid`, THE Portal SHALL render the billing card with a visually distinct "paid" state (e.g., green background) and hide the payment action button.
4. WHEN the Bill status is `Waived`, THE Portal SHALL display a "Waived" badge and hide the payment action button.
5. WHEN the Bill has `LateFeeApplied === true`, THE Portal SHALL display a late-fee warning banner showing the `LateFeeTotal` amount.
6. WHEN no active session exists, THE Portal SHALL display an informational message: "No active academic session found. Please contact the Registry."
7. WHEN the API returns a 404 (no bill generated yet), THE Portal SHALL display an informational message: "Your fee bill has not been generated yet. Please contact the Finance office."
8. IF the bill fetch request fails with a network or server error, THEN THE Portal SHALL display an error Toast: "Failed to load your fee bill. Please try again."

---

### Requirement 2: View Fee Payment History

**User Story:** As a student, I want to see a history of all my payment transactions, so that I can track what I have paid and verify the status of each payment.

#### Acceptance Criteria

1. WHEN the Portal loads, THE Portal SHALL call `GET /api/fees/payments/student/{studentId}` and display all returned `FeePayment` records in a transaction table.
2. THE Portal SHALL display for each payment: the date/time (`paidAt`), payment method (`paymentMethod`), amount, and verification status (`status`).
3. WHEN a payment has `status === 'Rejected'`, THE Portal SHALL display the `rejectionReason` beneath the status badge.
4. WHEN the payment history is empty, THE Portal SHALL display an empty-state message: "No transactions recorded yet."
5. IF the payment history fetch fails, THEN THE Portal SHALL display an error Toast: "Failed to load payment history."

---

### Requirement 3: Initiate Gateway Payment via Paystack

**User Story:** As a student with an outstanding balance, I want to pay via Paystack, so that my payment is processed instantly and my bill is updated automatically.

#### Acceptance Criteria

1. WHEN the student clicks the Paystack payment button, THE Portal SHALL call `POST /api/fees/payments/initiate` with `gateway: "Paystack"`, the `studentFeeRecordId`, the full outstanding `balance` as the amount, the student's email, name, and a `callbackUrl` pointing to the current page URL (path only, no query params).
2. WHEN the initiation response contains a `checkoutUrl`, THE Portal SHALL redirect the browser to that URL.
3. WHILE a gateway initiation request is in-flight, THE Portal SHALL disable both gateway buttons and the manual upload submit button to prevent duplicate submissions.
4. IF the gateway initiation request fails, THEN THE Portal SHALL display an error Toast: "Failed to initiate Paystack payment. Please try again." and re-enable the payment buttons.
5. WHEN the student is redirected back to the Portal with a `reference` or `trxref` query parameter, THE Portal SHALL display a success Toast: "Payment received. Your bill will be updated shortly." and reload the bill and payment history after a 3-second delay.
6. WHEN the student is redirected back with a `status=cancelled` or `status=failed` query parameter, THE Portal SHALL display an error Toast: "Payment was not completed. Please try again."
7. AFTER processing the callback query parameters, THE Portal SHALL remove them from the browser URL without triggering a page reload.

---

### Requirement 4: Initiate Gateway Payment via Hydrogen

**User Story:** As a student with an outstanding balance, I want to pay via Hydrogen Pay, so that I have an alternative instant payment channel.

#### Acceptance Criteria

1. WHEN the student clicks the Hydrogen Pay button, THE Portal SHALL call `POST /api/fees/payments/initiate` with `gateway: "Hydrogen"`, the `studentFeeRecordId`, the full outstanding `balance`, the student's email, name, and a `callbackUrl` pointing to the current page URL (path only, no query params).
2. WHEN the initiation response contains a `checkoutUrl`, THE Portal SHALL redirect the browser to that URL.
3. WHILE a gateway initiation request is in-flight, THE Portal SHALL disable both gateway buttons and the manual upload submit button.
4. IF the gateway initiation request fails, THEN THE Portal SHALL display an error Toast: "Failed to initiate Hydrogen payment. Please try again." and re-enable the payment buttons.
5. WHEN the student is redirected back with a `tx_ref` or `transactionRef` query parameter, THE Portal SHALL display a success Toast: "Payment received. Your bill will be updated shortly." and reload the bill and payment history after a 3-second delay.
6. WHEN the student is redirected back with a `status=cancelled` or `status=failed` query parameter, THE Portal SHALL display an error Toast: "Payment was not completed. Please try again."

---

### Requirement 5: Submit Manual Bank-Teller Payment

**User Story:** As a student, I want to upload a bank-teller receipt and reference number, so that the Finance team can verify and confirm my offline payment.

#### Acceptance Criteria

1. THE Portal SHALL provide a file input that accepts PDF, JPEG, and PNG files only (enforced via `accept` attribute).
2. WHEN the student selects a file, THE Portal SHALL display the selected filename as confirmation.
3. THE Portal SHALL provide a text input for the bank reference number.
4. WHEN the student clicks "Submit", THE Portal SHALL validate that both a file and a non-empty reference number are provided before submitting.
5. WHEN both inputs are valid, THE Portal SHALL call `POST /api/fees/payments/manual` as a `multipart/form-data` request containing `studentFeeRecordId`, `amount` (the current balance), `referenceNumber`, and the receipt file.
6. WHEN the manual payment is submitted successfully, THE Portal SHALL display a success Toast: "Receipt submitted. The Finance team will verify your payment within 48 hours." and reload the payment history.
7. IF the manual payment submission fails, THEN THE Portal SHALL display an error Toast: "Failed to submit receipt. Please check your connection and try again."
8. WHILE the manual payment submission is in-flight, THE Portal SHALL disable the submit button to prevent duplicate submissions.
9. WHEN the student has a `balance === 0` (fully paid or waived), THE Portal SHALL hide the manual payment upload section entirely.

---

### Requirement 6: Gateway Payment Callback Handling

**User Story:** As a student returning from a payment gateway, I want the portal to correctly interpret the redirect result, so that I receive accurate feedback about whether my payment succeeded or failed.

#### Acceptance Criteria

1. WHEN the Portal initialises, THE Portal SHALL inspect the current URL query parameters for gateway callback indicators (`reference`, `trxref`, `tx_ref`, `transactionRef`, `status`).
2. WHEN a `reference` or `trxref` parameter is present (Paystack), THE Portal SHALL treat the callback as a Paystack return.
3. WHEN a `tx_ref` or `transactionRef` parameter is present (Hydrogen), THE Portal SHALL treat the callback as a Hydrogen return.
4. WHEN the callback `status` parameter is `success` or is absent (Paystack default), THE Portal SHALL show a success Toast and schedule a data reload after 3 seconds.
5. WHEN the callback `status` parameter is `cancelled` or `failed`, THE Portal SHALL show an error Toast without reloading data.
6. THE Portal SHALL clean the callback query parameters from the URL using `window.history.replaceState` after processing.

---

### Requirement 7: Backend — Student Bill Endpoint Access for Students

**User Story:** As a student, I want the API to allow me to retrieve my own bill, so that the portal can display my fee data securely.

#### Acceptance Criteria

1. THE `GET /api/fees/bill/{studentId}/{sessionId}` endpoint SHALL permit access to users with the `Student` role (already configured).
2. WHEN a student requests a bill for a `studentId` that does not match their own authenticated identity, THE endpoint SHALL return HTTP 403 Forbidden.
3. WHEN a student requests a bill that does not exist, THE endpoint SHALL return HTTP 404 with a descriptive error message.
4. THE `GET /api/fees/payments/student/{studentId}` endpoint SHALL permit access to users with the `Student` role (already configured).
5. WHEN a student requests payment history for a `studentId` that does not match their own authenticated identity, THE endpoint SHALL return HTTP 403 Forbidden.

---

### Requirement 8: Backend — Student-Scoped Bill Endpoint

**User Story:** As a student, I want a dedicated endpoint to retrieve my own bill without needing to supply my student ID in the URL, so that the portal can fetch my bill securely using only my authentication token.

#### Acceptance Criteria

1. THE System SHALL expose `GET /api/fees/my-bill?sessionId={sessionId}` accessible to users with the `Student` role.
2. WHEN a student calls this endpoint, THE endpoint SHALL resolve the student's identity from the authenticated JWT claims and return the corresponding `StudentBillResponse`.
3. WHEN no bill exists for the student in the given session, THE endpoint SHALL return HTTP 404.
4. WHEN no `sessionId` query parameter is provided, THE endpoint SHALL use the currently active academic session.
5. IF no active session exists and no `sessionId` is provided, THEN THE endpoint SHALL return HTTP 400 with message: "No active session found."

---

### Requirement 9: UI Styling and Interaction Standards

**User Story:** As a student, I want the fee portal to match the Wigwe University dark theme and provide clear interactive feedback, so that the experience feels consistent and professional.

#### Acceptance Criteria

1. THE Portal SHALL use the dark theme background (`#1a1a1a`), gold accent (`#f5c518`), and white text consistent with the existing LMS dashboard.
2. THE Portal SHALL apply `cursor-pointer` styling to all interactive buttons.
3. THE Portal SHALL display a Toast notification for every user-initiated action — both on success and on error.
4. WHEN a Toast is displayed, THE Portal SHALL automatically dismiss it after 4 seconds.
5. THE Portal SHALL display a loading skeleton or spinner while the bill and payment history are being fetched.
6. THE Portal SHALL be responsive, adapting the layout for mobile viewports (single-column) and desktop viewports (multi-column grid).
7. WHEN a button action is in-flight (loading state), THE Portal SHALL show a visual loading indicator on the button (e.g., spinner or "..." text) and disable it to prevent double-clicks.

---

### Requirement 10: Partial Payment Support

**User Story:** As a student with a partially paid bill, I want to pay the remaining balance, so that I can clear my outstanding obligation in multiple transactions.

#### Acceptance Criteria

1. WHEN the Bill status is `PartiallyPaid`, THE Portal SHALL display the remaining `balance` as the payable amount and allow the student to initiate a gateway or manual payment for that amount.
2. THE Portal SHALL display both `TotalAmount` and `AmountPaid` alongside the `balance` so the student can see the full payment history at a glance.
3. WHEN a partial payment is confirmed by the Finance team or gateway webhook, THE Portal SHALL reflect the updated `AmountPaid` and `Balance` on the next data load.
