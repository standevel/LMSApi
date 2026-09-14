using System;

namespace LMS.Api.Data.Entities;

/// <summary>
/// Global configuration for course registration settings
/// </summary>
public sealed class SystemRegistrationConfiguration
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    /// <summary>
    /// Course registration strategy: Single or Bulk
    /// </summary>
    public string Strategy { get; set; } = "Single";
    
    /// <summary>
    /// Whether to dynamically calculate and enforce minimum credit hours from the curriculum
    /// </summary>
    public bool EnforceMinCredits { get; set; } = true;

    /// <summary>
    /// Whether students and batch enrollment can register courses across both First and Second semesters simultaneously in an academic session
    /// </summary>
    public bool AllowMultiSemesterRegistration { get; set; } = false;

    /// <summary>
    /// Whether program transfers strictly require a new JAMB admission letter before approval.
    /// When false, transfers can proceed under institutional approval without JAMB documentation.
    /// </summary>
    public bool RequireJambForProgramTransfer { get; set; } = true;

    /// <summary>
    /// Global template for student matric numbers (defaults to "WU/{PROGRAM}/{YYYY}/{SEQ}")
    /// </summary>
    public string MatricNumberFormat { get; set; } = "WU/{PROGRAM}/{YYYY}/{SEQ}";
    
    /// <summary>
    /// Master switch to enable or disable automatic course registration
    /// </summary>
    public bool EnableAutoRegistration { get; set; } = false;

    /// <summary>
    /// Automatically register courses when a student enrolls in a program/level
    /// </summary>
    public bool AutoRegisterOnEnrollment { get; set; } = false;

    /// <summary>
    /// Automatically register eligible courses for active students when session registration starts
    /// </summary>
    public bool AutoRegisterOnRegistrationStart { get; set; } = false;

    /// <summary>
    /// Course categories to auto-register: "Compulsory" or "AllCurriculum"
    /// </summary>
    public string AutoRegisterCourseCategories { get; set; } = "Compulsory";

    /// <summary>
    /// Whether to automatically include failed/carryover courses from previous sessions
    /// </summary>
    public bool AutoRegisterCarryovers { get; set; } = true;

    /// <summary>
    /// Handling for credit limits: "Strict" (cap at level limit) or "AllowUpToMax"
    /// </summary>
    public string AutoRegisterCreditLimitHandling { get; set; } = "Strict";

    /// <summary>
    /// Target student level scope: "All" or "100LevelOnly"
    /// </summary>
    public string AutoRegisterTargetLevels { get; set; } = "All";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Guid? UpdatedById { get; set; }
}
