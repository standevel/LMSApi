using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LMS.Api.Contracts;
using LMS.Api.Data.Entities;

namespace LMS.Api.Services;

public interface ILectureSessionService
{
    Task<SessionGenerationResult> GenerateSessionsFromTimetableAsync(
        List<Guid> timetableSlotIds,
        DateOnly endDate,
        Guid userId);

    Task<BulkSessionGenerationResult> GenerateBulkSessionsForSemesterAsync(
        Guid academicSessionId,
        DateOnly endDate,
        Guid userId);

    Task<LectureSession> CreateManualSessionAsync(
        CreateManualSessionRequest request,
        Guid userId);

    Task<List<ConflictWarning>> DetectConflictsAsync(
        DateOnly date,
        TimeOnly startTime,
        TimeOnly endTime,
        List<Guid> lecturerIds,
        Guid? venueId);

    Task<List<LectureTimetableSlot>> GetTimetableSlotsForOfferingAsync(
        Guid courseOfferingId);

    Task<List<CourseOfferingWithSlotCount>> GetCourseOfferingsWithTimetableSlotsAsync(
        Guid academicSessionId);
}
