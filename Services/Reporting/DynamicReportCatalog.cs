using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using LMS.Api.Contracts;
using LMS.Api.Security;

namespace LMS.Api.Services.Reporting;

public static class DynamicReportCatalog
{
    private static readonly List<string> StringOperators = ["equals", "notEquals", "contains", "startsWith", "in", "isNull", "isNotNull"];
    private static readonly List<string> NumberOperators = ["equals", "notEquals", "greaterThan", "lessThan", "greaterThanOrEqual", "lessThanOrEqual", "between", "isNull", "isNotNull"];
    private static readonly List<string> DateOperators = ["equals", "greaterThan", "lessThan", "between", "isNull", "isNotNull"];
    private static readonly List<string> BooleanOperators = ["equals"];

    private static readonly Dictionary<string, List<string>> DatasetRolePermissions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["students"] = [LmsRoles.SuperAdmin, LmsRoles.Admin, LmsRoles.ViceChancellor, LmsRoles.Dean, LmsRoles.HOD, LmsRoles.Lecturer, LmsRoles.Adviser, LmsRoles.Registrar, LmsRoles.AcademicAdmin, LmsRoles.Finance],
        ["course_enrollments"] = [LmsRoles.SuperAdmin, LmsRoles.Admin, LmsRoles.ViceChancellor, LmsRoles.Dean, LmsRoles.HOD, LmsRoles.Lecturer, LmsRoles.Adviser, LmsRoles.Registrar, LmsRoles.AcademicAdmin],
        ["financial_records"] = [LmsRoles.SuperAdmin, LmsRoles.Admin, LmsRoles.ViceChancellor, LmsRoles.Finance],
        ["gradebook_results"] = [LmsRoles.SuperAdmin, LmsRoles.Admin, LmsRoles.ViceChancellor, LmsRoles.Dean, LmsRoles.HOD, LmsRoles.Lecturer, LmsRoles.AcademicAdmin, LmsRoles.Registrar],
        ["admissions_registry"] = [LmsRoles.SuperAdmin, LmsRoles.Admin, LmsRoles.ViceChancellor, LmsRoles.Registrar, LmsRoles.AdmissionOfficer],
        ["hostel_accommodation"] = [LmsRoles.SuperAdmin, LmsRoles.Admin, LmsRoles.ViceChancellor, LmsRoles.HostelWarden, LmsRoles.StudentWelfare, LmsRoles.Dean],
        ["timetable_attendance"] = [LmsRoles.SuperAdmin, LmsRoles.Admin, LmsRoles.ViceChancellor, LmsRoles.Dean, LmsRoles.HOD, LmsRoles.Lecturer, LmsRoles.AcademicAdmin]
    };

    public static List<ReportDatasetMetadataDto> GetAllDatasets() =>
    [
        // ==================== 1. STUDENTS & ACADEMICS ====================
        new ReportDatasetMetadataDto(
            Id: "students",
            DisplayName: "Students & Academic Performance",
            Description: "Comprehensive student profiles joined with degree programs, departments, academic standing, cumulative GPA, fee balances, and hostels.",
            Icon: "graduation-cap",
            Category: "Academic",
            DefaultSelectedFields: ["student.matricNumber", "student.fullName", "program.programName", "level.levelName", "standing.cumulativeGpa", "standing.standingType", "fees.outstandingBalance"],
            Tables:
            [
                new ReportTableMetadataDto(
                    Id: "student",
                    DisplayName: "Student Profile",
                    Description: "Core demographic and enrollment records",
                    Fields:
                    [
                        new("student.matricNumber", "student", "Matric Number", "string", true, true, StringOperators),
                        new("student.fullName", "student", "Full Name", "string", true, true, StringOperators),
                        new("student.firstName", "student", "First Name", "string", true, true, StringOperators),
                        new("student.lastName", "student", "Last Name", "string", true, true, StringOperators),
                        new("student.officialEmail", "student", "Official / Institutional Email", "string", true, true, StringOperators),
                        new("student.personalEmail", "student", "Personal / Unofficial Email", "string", true, true, StringOperators),
                        new("student.email", "student", "Primary Email", "string", true, true, StringOperators),
                        new("student.gender", "student", "Gender", "string", true, true, StringOperators, [new("Male", "Male"), new("Female", "Female")]),
                        new("student.phoneNumber", "student", "Phone Number", "string", true, true, StringOperators),
                        new("student.dateOfBirth", "student", "Date of Birth", "date", true, true, DateOperators),
                        new("student.jambNumber", "student", "JAMB Reg Number", "string", true, true, StringOperators),
                        new("student.jambScore", "student", "JAMB Score", "number", true, true, NumberOperators),
                        new("student.parentName", "student", "Parent / Guardian Name", "string", true, true, StringOperators),
                        new("student.parentPhone", "student", "Parent / Guardian Phone", "string", true, true, StringOperators),
                        new("student.parentEmail", "student", "Parent / Guardian Email", "string", true, true, StringOperators),
                        new("student.stateOfOrigin", "student", "State of Origin", "string", true, true, StringOperators),
                        new("student.nationality", "student", "Nationality", "string", true, true, StringOperators),
                        new("student.status", "student", "Student Status", "badge", true, true, StringOperators, [new("Active", "Active"), new("Suspended", "Suspended"), new("Graduated", "Graduated"), new("Withdrawn", "Withdrawn")]),
                        new("student.admissionDate", "student", "Admission Date", "date", true, true, DateOperators),
                        new("student.studyMode", "student", "Study Mode", "string", true, true, StringOperators, [new("Full-Time", "Full-Time"), new("Part-Time", "Part-Time")])
                    ]
                ),
                new ReportTableMetadataDto(
                    Id: "parent",
                    DisplayName: "Parent & Guardian Contacts",
                    Description: "Parent and guardian names, telephone numbers, and email addresses",
                    Fields:
                    [
                        new("parent.guardianName", "parent", "Parent / Guardian Name", "string", true, true, StringOperators),
                        new("parent.guardianPhone", "parent", "Parent / Guardian Phone", "string", true, true, StringOperators),
                        new("parent.guardianEmail", "parent", "Parent / Guardian Email", "string", true, true, StringOperators)
                    ]
                ),
                new ReportTableMetadataDto(
                    Id: "program",
                    DisplayName: "Academic Program",
                    Description: "Degree program and curriculum information",
                    Fields:
                    [
                        new("program.programName", "program", "Program Name", "string", true, true, StringOperators),
                        new("program.programCode", "program", "Program Code", "string", true, true, StringOperators),
                        new("program.degreeType", "program", "Degree Type", "string", true, true, StringOperators, [new("BSc", "BSc"), new("BA", "BA"), new("BEng", "BEng"), new("LLB", "LLB"), new("MSc", "MSc")])
                    ]
                ),
                new ReportTableMetadataDto(
                    Id: "department",
                    DisplayName: "Department",
                    Description: "Academic department assignment",
                    Fields:
                    [
                        new("department.departmentName", "department", "Department Name", "string", true, true, StringOperators),
                        new("department.departmentCode", "department", "Department Code", "string", true, true, StringOperators)
                    ]
                ),
                new ReportTableMetadataDto(
                    Id: "faculty",
                    DisplayName: "College / Faculty",
                    Description: "Parent faculty or college assignment",
                    Fields:
                    [
                        new("faculty.facultyName", "faculty", "College / Faculty Name", "string", true, true, StringOperators),
                        new("faculty.facultyCode", "faculty", "Faculty Code", "string", true, true, StringOperators)
                    ]
                ),
                new ReportTableMetadataDto(
                    Id: "level",
                    DisplayName: "Academic Level",
                    Description: "Student current academic level",
                    Fields:
                    [
                        new("level.levelName", "level", "Level", "string", true, true, StringOperators, [new("100 Level", "100"), new("200 Level", "200"), new("300 Level", "300"), new("400 Level", "400"), new("500 Level", "500")])
                    ]
                ),
                new ReportTableMetadataDto(
                    Id: "standing",
                    DisplayName: "Academic Standing & GPA",
                    Description: "Calculated academic metrics, GPA and probation markers",
                    Fields:
                    [
                        new("standing.cumulativeGpa", "standing", "Cumulative GPA", "number", true, true, NumberOperators),
                        new("standing.standingType", "standing", "Academic Standing", "badge", true, true, StringOperators, [new("Good Standing", "Good Standing"), new("Dean's List", "Dean's List"), new("Probation", "Probation"), new("Suspended", "Suspended")]),
                        new("standing.creditsAttempted", "standing", "Credits Attempted", "number", true, true, NumberOperators),
                        new("standing.creditsEarned", "standing", "Credits Earned", "number", true, true, NumberOperators)
                    ]
                ),
                new ReportTableMetadataDto(
                    Id: "fees",
                    DisplayName: "Fee Ledger & Balance",
                    Description: "Student tuition ledger, payments, and outstanding arrears",
                    Fields:
                    [
                        new("fees.totalBilled", "fees", "Total Billed", "currency", true, true, NumberOperators),
                        new("fees.amountPaid", "fees", "Amount Paid", "currency", true, true, NumberOperators),
                        new("fees.outstandingBalance", "fees", "Outstanding Balance", "currency", true, true, NumberOperators),
                        new("fees.paymentStatus", "fees", "Payment Status", "badge", true, true, StringOperators, [new("Paid", "Paid"), new("Partial", "Partial"), new("Unpaid", "Unpaid")])
                    ]
                ),
                new ReportTableMetadataDto(
                    Id: "hostel",
                    DisplayName: "Hostel Allocation",
                    Description: "Assigned residential accommodation details",
                    Fields:
                    [
                        new("hostel.blockName", "hostel", "Hostel Hall / Block", "string", true, true, StringOperators),
                        new("hostel.roomNumber", "hostel", "Room Number", "string", true, true, StringOperators),
                        new("hostel.bedNumber", "hostel", "Bed Space", "string", true, true, StringOperators)
                    ]
                )
            ]
        ),

        // ==================== 2. COURSE ENROLLMENTS ====================
        new ReportDatasetMetadataDto(
            Id: "course_enrollments",
            DisplayName: "Course Enrollments & Attendance",
            Description: "Course registrations joined with students, course offerings, assigned lecturers, grades, and attendance metrics.",
            Icon: "book-open",
            Category: "Academic",
            DefaultSelectedFields: ["student.matricNumber", "student.fullName", "course.courseCode", "course.courseTitle", "courseOffering.sessionName", "courseOffering.semester", "lecturer.lecturerName", "grades.totalScore", "grades.gradeLetter", "attendance.attendancePercentage"],
            Tables:
            [
                new ReportTableMetadataDto(
                    Id: "student",
                    DisplayName: "Enrolled Student",
                    Description: "Student registered in the course offering",
                    Fields:
                    [
                        new("student.matricNumber", "student", "Matric Number", "string", true, true, StringOperators),
                        new("student.fullName", "student", "Student Name", "string", true, true, StringOperators),
                        new("student.email", "student", "Email", "string", true, true, StringOperators),
                        new("student.program", "student", "Program", "string", true, true, StringOperators),
                        new("student.level", "student", "Level", "string", true, true, StringOperators)
                    ]
                ),
                new ReportTableMetadataDto(
                    Id: "course",
                    DisplayName: "Course Information",
                    Description: "Course curriculum and catalog details",
                    Fields:
                    [
                        new("course.courseCode", "course", "Course Code", "string", true, true, StringOperators),
                        new("course.courseTitle", "course", "Course Title", "string", true, true, StringOperators),
                        new("course.creditUnits", "course", "Credit Units", "number", true, true, NumberOperators)
                    ]
                ),
                new ReportTableMetadataDto(
                    Id: "courseOffering",
                    DisplayName: "Course Offering",
                    Description: "Specific session offering and semester",
                    Fields:
                    [
                        new("courseOffering.sessionName", "courseOffering", "Academic Session", "string", true, true, StringOperators),
                        new("courseOffering.semester", "courseOffering", "Semester", "string", true, true, StringOperators, [new("First Semester", "1"), new("Second Semester", "2")]),
                        new("courseOffering.section", "courseOffering", "Section", "string", true, true, StringOperators)
                    ]
                ),
                new ReportTableMetadataDto(
                    Id: "lecturer",
                    DisplayName: "Instructing Lecturer",
                    Description: "Assigned course lecturer details",
                    Fields:
                    [
                        new("lecturer.lecturerName", "lecturer", "Lecturer Name", "string", true, true, StringOperators),
                        new("lecturer.lecturerEmail", "lecturer", "Lecturer Email", "string", true, true, StringOperators)
                    ]
                ),
                new ReportTableMetadataDto(
                    Id: "grades",
                    DisplayName: "Course Grades & Assessment",
                    Description: "Student assessment and final scores for this offering",
                    Fields:
                    [
                        new("grades.continuousAssessment", "grades", "CA Score (30%)", "number", true, true, NumberOperators),
                        new("grades.examScore", "grades", "Exam Score (70%)", "number", true, true, NumberOperators),
                        new("grades.totalScore", "grades", "Total Score (100%)", "number", true, true, NumberOperators),
                        new("grades.gradeLetter", "grades", "Grade Letter", "badge", true, true, StringOperators, [new("A", "A"), new("B", "B"), new("C", "C"), new("D", "D"), new("E", "E"), new("F", "F")]),
                        new("grades.gradePoint", "grades", "Grade Point", "number", true, true, NumberOperators)
                    ]
                ),
                new ReportTableMetadataDto(
                    Id: "attendance",
                    DisplayName: "Session Attendance",
                    Description: "Physical and virtual lecture attendance stats",
                    Fields:
                    [
                        new("attendance.sessionsAttended", "attendance", "Sessions Attended", "number", true, true, NumberOperators),
                        new("attendance.totalSessions", "attendance", "Total Sessions", "number", true, true, NumberOperators),
                        new("attendance.attendancePercentage", "attendance", "Attendance %", "number", true, true, NumberOperators)
                    ]
                )
            ]
        ),

        // ==================== 3. FINANCIAL RECORDS ====================
        new ReportDatasetMetadataDto(
            Id: "financial_records",
            DisplayName: "Financials, Fees & Bursary",
            Description: "Fee payment receipts, transaction reconciliations, student billing records, and scholarship discounts.",
            Icon: "landmark",
            Category: "Finance",
            DefaultSelectedFields: ["payment.reference", "payment.paymentDate", "student.matricNumber", "student.fullName", "payment.amount", "payment.paymentMethod", "payment.status", "feeTemplate.categoryName"],
            Tables:
            [
                new ReportTableMetadataDto(
                    Id: "payment",
                    DisplayName: "Payment Transaction",
                    Description: "Receipt and transaction ledger details",
                    Fields:
                    [
                        new("payment.reference", "payment", "Transaction Reference", "string", true, true, StringOperators),
                        new("payment.amount", "payment", "Amount (₦)", "currency", true, true, NumberOperators),
                        new("payment.paymentDate", "payment", "Payment Date", "date", true, true, DateOperators),
                        new("payment.paymentMethod", "payment", "Payment Method", "string", true, true, StringOperators, [new("Paystack", "1"), new("Hydrogen", "2"), new("Bank Transfer", "3"), new("Card", "4")]),
                        new("payment.status", "payment", "Transaction Status", "badge", true, true, StringOperators, [new("Confirmed", "Confirmed"), new("Pending", "Pending"), new("Failed", "Failed"), new("Reconciled", "Reconciled")]),
                        new("payment.receiptNumber", "payment", "Receipt Number", "string", true, true, StringOperators)
                    ]
                ),
                new ReportTableMetadataDto(
                    Id: "student",
                    DisplayName: "Paying Student",
                    Description: "Student associated with the fee transaction",
                    Fields:
                    [
                        new("student.matricNumber", "student", "Matric Number", "string", true, true, StringOperators),
                        new("student.fullName", "student", "Student Name", "string", true, true, StringOperators),
                        new("student.programName", "student", "Program", "string", true, true, StringOperators),
                        new("student.levelName", "student", "Level", "string", true, true, StringOperators),
                        new("student.facultyName", "student", "Faculty", "string", true, true, StringOperators)
                    ]
                ),
                new ReportTableMetadataDto(
                    Id: "feeTemplate",
                    DisplayName: "Fee Schedule / Template",
                    Description: "Fee categorization and billing schedule",
                    Fields:
                    [
                        new("feeTemplate.templateName", "feeTemplate", "Fee Template", "string", true, true, StringOperators),
                        new("feeTemplate.categoryName", "feeTemplate", "Fee Category", "string", true, true, StringOperators, [new("Tuition", "Tuition"), new("Hostel", "Hostel"), new("Medical", "Medical"), new("Acceptance", "Acceptance"), new("Sundry", "Sundry")])
                    ]
                ),
                new ReportTableMetadataDto(
                    Id: "scholarship",
                    DisplayName: "Scholarship & Sponsor",
                    Description: "Applied scholarship awards and discounts",
                    Fields:
                    [
                        new("scholarship.scholarshipName", "scholarship", "Scholarship Name", "string", true, true, StringOperators),
                        new("scholarship.sponsorName", "scholarship", "Sponsor Organization", "string", true, true, StringOperators),
                        new("scholarship.discountAmount", "scholarship", "Discount Amount (₦)", "currency", true, true, NumberOperators)
                    ]
                )
            ]
        ),

        // ==================== 4. GRADEBOOK RESULTS ====================
        new ReportDatasetMetadataDto(
            Id: "gradebook_results",
            DisplayName: "Assessments & Gradebook Results",
            Description: "Course grades, Senate published results, grade points, approval chains, and lecturer grading records.",
            Icon: "award",
            Category: "Academic",
            DefaultSelectedFields: ["student.matricNumber", "student.fullName", "course.courseCode", "course.courseTitle", "session.sessionName", "result.totalScore", "result.gradeLetter", "result.gradePoint", "result.publishedStatus"],
            Tables:
            [
                new ReportTableMetadataDto(
                    Id: "result",
                    DisplayName: "Final Result & Standing",
                    Description: "Consolidated student course grade and publication status",
                    Fields:
                    [
                        new("result.totalScore", "result", "Total Score", "number", true, true, NumberOperators),
                        new("result.gradeLetter", "result", "Grade Letter", "badge", true, true, StringOperators, [new("A", "A"), new("B", "B"), new("C", "C"), new("D", "D"), new("E", "E"), new("F", "F")]),
                        new("result.gradePoint", "result", "Grade Point", "number", true, true, NumberOperators),
                        new("result.isPassed", "result", "Pass / Fail", "badge", true, true, BooleanOperators, [new("Passed", "true"), new("Failed", "false")]),
                        new("result.publishedStatus", "result", "Senate Approval Status", "badge", true, true, StringOperators, [new("Draft", "Draft"), new("HOD Approved", "HOD Approved"), new("Dean Approved", "Dean Approved"), new("Senate Published", "Senate Published")])
                    ]
                ),
                new ReportTableMetadataDto(
                    Id: "student",
                    DisplayName: "Candidate Student",
                    Description: "Candidate student details",
                    Fields:
                    [
                        new("student.matricNumber", "student", "Matric Number", "string", true, true, StringOperators),
                        new("student.fullName", "student", "Student Name", "string", true, true, StringOperators),
                        new("student.levelName", "student", "Level", "string", true, true, StringOperators),
                        new("student.programName", "student", "Program", "string", true, true, StringOperators)
                    ]
                ),
                new ReportTableMetadataDto(
                    Id: "course",
                    DisplayName: "Evaluated Course",
                    Description: "Course evaluated",
                    Fields:
                    [
                        new("course.courseCode", "course", "Course Code", "string", true, true, StringOperators),
                        new("course.courseTitle", "course", "Course Title", "string", true, true, StringOperators)
                    ]
                ),
                new ReportTableMetadataDto(
                    Id: "session",
                    DisplayName: "Academic Session",
                    Description: "Academic session and semester",
                    Fields:
                    [
                        new("session.sessionName", "session", "Session", "string", true, true, StringOperators),
                        new("session.semester", "session", "Semester", "string", true, true, StringOperators)
                    ]
                ),
                new ReportTableMetadataDto(
                    Id: "lecturer",
                    DisplayName: "Grading Lecturer",
                    Description: "Lecturer who submitted grades",
                    Fields:
                    [
                        new("lecturer.lecturerName", "lecturer", "Lecturer Name", "string", true, true, StringOperators)
                    ]
                )
            ]
        ),

        // ==================== 5. ADMISSIONS & REGISTRY ====================
        new ReportDatasetMetadataDto(
            Id: "admissions_registry",
            DisplayName: "Admissions & Registry Pipeline",
            Description: "Applicant records, JAMB scores, program choices, admission decisions, and matriculation allocations.",
            Icon: "user-check",
            Category: "Registry",
            DefaultSelectedFields: ["application.applicationNumber", "application.applicantName", "application.jambScore", "program.firstChoiceProgram", "program.admittedProgram", "application.status", "decision.acceptanceFeePaid", "decision.matricNumberAssigned"],
            Tables:
            [
                new ReportTableMetadataDto(
                    Id: "application",
                    DisplayName: "Admission Application",
                    Description: "Applicant bio-data, contact, and submission status",
                    Fields:
                    [
                        new("application.applicationNumber", "application", "Application Number", "string", true, true, StringOperators),
                        new("application.applicantName", "application", "Applicant Name", "string", true, true, StringOperators),
                        new("application.email", "application", "Email", "string", true, true, StringOperators),
                        new("application.phoneNumber", "application", "Phone Number", "string", true, true, StringOperators),
                        new("application.gender", "application", "Gender", "string", true, true, StringOperators, [new("Male", "Male"), new("Female", "Female")]),
                        new("application.stateOfOrigin", "application", "State of Origin", "string", true, true, StringOperators),
                        new("application.jambRegNo", "application", "JAMB Reg No", "string", true, true, StringOperators),
                        new("application.jambScore", "application", "JAMB Score", "number", true, true, NumberOperators),
                        new("application.dateOfBirth", "application", "Date of Birth", "date", true, true, DateOperators),
                        new("application.parentName", "application", "Parent / Guardian Name", "string", true, true, StringOperators),
                        new("application.parentPhone", "application", "Parent / Guardian Phone", "string", true, true, StringOperators),
                        new("application.parentEmail", "application", "Parent / Guardian Email", "string", true, true, StringOperators),
                        new("application.status", "application", "Application Status", "badge", true, true, StringOperators, [new("Submitted", "Submitted"), new("Under Review", "Under Review"), new("Admitted", "Admitted"), new("Rejected", "Rejected"), new("Enrolled", "Enrolled")]),
                        new("application.submissionDate", "application", "Submission Date", "date", true, true, DateOperators)
                    ]
                ),
                new ReportTableMetadataDto(
                    Id: "program",
                    DisplayName: "Program & Faculty Selection",
                    Description: "Academic program preferences and allocations",
                    Fields:
                    [
                        new("program.firstChoiceProgram", "program", "First Choice Program", "string", true, true, StringOperators),
                        new("program.admittedProgram", "program", "Admitted Program", "string", true, true, StringOperators),
                        new("program.departmentName", "program", "Department", "string", true, true, StringOperators),
                        new("program.facultyName", "program", "Faculty / College", "string", true, true, StringOperators)
                    ]
                ),
                new ReportTableMetadataDto(
                    Id: "decision",
                    DisplayName: "Admission Decisions & Matriculation",
                    Description: "Offer letters, payments, and matriculation",
                    Fields:
                    [
                        new("decision.offerAccepted", "decision", "Offer Accepted", "badge", true, true, BooleanOperators, [new("Accepted", "true"), new("Pending", "false")]),
                        new("decision.acceptanceFeePaid", "decision", "Acceptance Fee Paid", "badge", true, true, BooleanOperators, [new("Paid", "true"), new("Unpaid", "false")]),
                        new("decision.matricNumberAssigned", "decision", "Matric Number Assigned", "string", true, true, StringOperators)
                    ]
                )
            ]
        ),

        // ==================== 6. HOSTEL & ACCOMMODATION ====================
        new ReportDatasetMetadataDto(
            Id: "hostel_accommodation",
            DisplayName: "Hostel & Accommodation Audit",
            Description: "Hostel halls, room bed space allocations, student residential directory, and exeat records.",
            Icon: "bed",
            Category: "Operations",
            DefaultSelectedFields: ["hostel.blockName", "hostel.roomNumber", "hostel.bedNumber", "student.matricNumber", "student.fullName", "student.gender", "allocation.allocationDate", "allocation.status"],
            Tables:
            [
                new ReportTableMetadataDto(
                    Id: "allocation",
                    DisplayName: "Hostel Allocation Record",
                    Description: "Session room assignment status",
                    Fields:
                    [
                        new("allocation.allocationDate", "allocation", "Allocation Date", "date", true, true, DateOperators),
                        new("allocation.status", "allocation", "Allocation Status", "badge", true, true, StringOperators, [new("Allocated", "Allocated"), new("Checked In", "Checked In"), new("Checked Out", "Checked Out"), new("Revoked", "Revoked")]),
                        new("allocation.sessionName", "allocation", "Academic Session", "string", true, true, StringOperators)
                    ]
                ),
                new ReportTableMetadataDto(
                    Id: "hostel",
                    DisplayName: "Hostel Facility",
                    Description: "Hostel block, room, and capacity specifications",
                    Fields:
                    [
                        new("hostel.blockName", "hostel", "Hostel Hall / Block", "string", true, true, StringOperators),
                        new("hostel.genderType", "hostel", "Hostel Gender", "string", true, true, StringOperators, [new("Male", "Male"), new("Female", "Female")]),
                        new("hostel.roomNumber", "hostel", "Room Number", "string", true, true, StringOperators),
                        new("hostel.bedNumber", "hostel", "Bed Space", "string", true, true, StringOperators),
                        new("hostel.roomCapacity", "hostel", "Room Capacity", "number", true, true, NumberOperators)
                    ]
                ),
                new ReportTableMetadataDto(
                    Id: "student",
                    DisplayName: "Accommodated Student",
                    Description: "Student resident information",
                    Fields:
                    [
                        new("student.matricNumber", "student", "Matric Number", "string", true, true, StringOperators),
                        new("student.fullName", "student", "Student Name", "string", true, true, StringOperators),
                        new("student.gender", "student", "Gender", "string", true, true, StringOperators),
                        new("student.levelName", "student", "Level", "string", true, true, StringOperators),
                        new("student.programName", "student", "Program", "string", true, true, StringOperators),
                        new("student.phoneNumber", "student", "Emergency / Phone", "string", true, true, StringOperators)
                    ]
                ),
                new ReportTableMetadataDto(
                    Id: "exeat",
                    DisplayName: "Exeat Tracking",
                    Description: "Campus leave passes and exeat logs",
                    Fields:
                    [
                        new("exeat.totalExeats", "exeat", "Total Exeats Taken", "number", true, true, NumberOperators),
                        new("exeat.activeExeatStatus", "exeat", "Current Exeat Status", "badge", true, true, StringOperators, [new("On Campus", "On Campus"), new("Off Campus", "Off Campus")])
                    ]
                )
            ]
        ),

        // ==================== 7. TIMETABLE & ATTENDANCE ====================
        new ReportDatasetMetadataDto(
            Id: "timetable_attendance",
            DisplayName: "Timetable, Sessions & Attendance",
            Description: "Class lecture sessions, timetable slots, venues, instructor attendance, and student presence rates.",
            Icon: "calendar-days",
            Category: "Operations",
            DefaultSelectedFields: ["session.sessionDate", "session.startTime", "course.courseCode", "course.courseTitle", "session.venue", "lecturer.lecturerName", "attendanceSummary.enrolledCount", "attendanceSummary.presentCount", "attendanceSummary.attendanceRate"],
            Tables:
            [
                new ReportTableMetadataDto(
                    Id: "session",
                    DisplayName: "Lecture Session",
                    Description: "Session scheduling, venue, and delivery mode",
                    Fields:
                    [
                        new("session.sessionDate", "session", "Session Date", "date", true, true, DateOperators),
                        new("session.startTime", "session", "Start Time", "string", true, true, StringOperators),
                        new("session.endTime", "session", "End Time", "string", true, true, StringOperators),
                        new("session.venue", "session", "Lecture Venue", "string", true, true, StringOperators),
                        new("session.sessionType", "session", "Session Type", "badge", true, true, StringOperators, [new("In-Person", "In-Person"), new("Virtual", "Virtual"), new("Hybrid", "Hybrid")]),
                        new("session.status", "session", "Session Status", "badge", true, true, StringOperators, [new("Scheduled", "Scheduled"), new("Completed", "Completed"), new("Cancelled", "Cancelled")])
                    ]
                ),
                new ReportTableMetadataDto(
                    Id: "course",
                    DisplayName: "Course Details",
                    Description: "Course code and title",
                    Fields:
                    [
                        new("course.courseCode", "course", "Course Code", "string", true, true, StringOperators),
                        new("course.courseTitle", "course", "Course Title", "string", true, true, StringOperators)
                    ]
                ),
                new ReportTableMetadataDto(
                    Id: "lecturer",
                    DisplayName: "Lecturer / Instructor",
                    Description: "Assigned course lecturer",
                    Fields:
                    [
                        new("lecturer.lecturerName", "lecturer", "Lecturer Name", "string", true, true, StringOperators),
                        new("lecturer.lecturerEmail", "lecturer", "Lecturer Email", "string", true, true, StringOperators)
                    ]
                ),
                new ReportTableMetadataDto(
                    Id: "attendanceSummary",
                    DisplayName: "Attendance Summary",
                    Description: "Aggregated student presence and attendance percentages",
                    Fields:
                    [
                        new("attendanceSummary.enrolledCount", "attendanceSummary", "Enrolled Students", "number", true, true, NumberOperators),
                        new("attendanceSummary.presentCount", "attendanceSummary", "Students Present", "number", true, true, NumberOperators),
                        new("attendanceSummary.absentCount", "attendanceSummary", "Students Absent", "number", true, true, NumberOperators),
                        new("attendanceSummary.attendanceRate", "attendanceSummary", "Attendance Rate %", "number", true, true, NumberOperators)
                    ]
                )
            ]
        )
    ];

    public static bool CanUserAccessDataset(ClaimsPrincipal user, string datasetId)
    {
        if (user.IsInRole(LmsRoles.Student))
            return false; // STUDENTS STRICTLY EXCLUDED

        if (user.IsInRole(LmsRoles.SuperAdmin) || user.IsInRole(LmsRoles.Admin) || user.IsInRole(LmsRoles.ViceChancellor))
            return true; // Full access

        if (!DatasetRolePermissions.TryGetValue(datasetId, out var permittedRoles))
            return false;

        return permittedRoles.Any(user.IsInRole);
    }

    public static List<ReportDatasetMetadataDto> GetDatasetsForUser(ClaimsPrincipal user)
    {
        if (user.IsInRole(LmsRoles.Student))
            return []; // Empty for students

        return GetAllDatasets()
            .Where(d => CanUserAccessDataset(user, d.Id))
            .ToList();
    }
}
