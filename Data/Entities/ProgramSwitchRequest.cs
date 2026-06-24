using System;
using System.Text.Json.Serialization;
using LMS.Api.Data.Enums;

namespace LMS.Api.Data.Entities;

/// <summary>
/// Represents a student's request to switch from one academic program to another.
/// Requires multi-stage approval: HoD → Dean → Admin/Registrar.
/// No approval can proceed without a JAMB admission letter document being attached.
/// </summary>
public sealed class ProgramSwitchRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // ── Student ──────────────────────────────────────────────────────────────
    public Guid StudentId { get; set; }
    [JsonIgnore]
    public Student Student { get; set; } = null!;

    // ── Programs ──────────────────────────────────────────────────────────────
    public Guid FromProgramId { get; set; }
    [JsonIgnore]
    public AcademicProgram FromProgram { get; set; } = null!;

    public Guid ToProgramId { get; set; }
    [JsonIgnore]
    public AcademicProgram ToProgram { get; set; } = null!;

    // ── Request Details ───────────────────────────────────────────────────────
    public string Reason { get; set; } = string.Empty;
    public ProgramSwitchStatus Status { get; set; } = ProgramSwitchStatus.Draft;

    /// <summary>
    /// URL/path to the uploaded JAMB admission letter.
    /// Required before any approval stage can proceed.
    /// </summary>
    public string? JambDocumentUrl { get; set; }
    public string? JambDocumentFileName { get; set; }
    public DateTime? JambDocumentUploadedAt { get; set; }

    // ── HoD Approval ─────────────────────────────────────────────────────────
    public Guid? HoDReviewedById { get; set; }
    [JsonIgnore]
    public AppUser? HoDReviewedBy { get; set; }
    public DateTime? HoDReviewedAt { get; set; }
    public string? HoDNotes { get; set; }

    // ── Dean Approval ─────────────────────────────────────────────────────────
    public Guid? DeanReviewedById { get; set; }
    [JsonIgnore]
    public AppUser? DeanReviewedBy { get; set; }
    public DateTime? DeanReviewedAt { get; set; }
    public string? DeanNotes { get; set; }

    // ── Admin Completion ──────────────────────────────────────────────────────
    public Guid? AdminCompletedById { get; set; }
    [JsonIgnore]
    public AppUser? AdminCompletedBy { get; set; }
    public DateTime? AdminCompletedAt { get; set; }
    public string? AdminNotes { get; set; }

    // ── Rejection ─────────────────────────────────────────────────────────────
    public string? RejectionReason { get; set; }
    public Guid? RejectedById { get; set; }
    [JsonIgnore]
    public AppUser? RejectedBy { get; set; }
    public DateTime? RejectedAt { get; set; }

    // ── Audit ─────────────────────────────────────────────────────────────────
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
