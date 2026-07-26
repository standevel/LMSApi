using LMS.Api.Data.Enums;

namespace LMS.Api.Contracts;

public record HostelBlockResponse(
    Guid Id,
    string Name,
    string Code,
    HostelGenderType GenderType,
    string? CampusLocation,
    int TotalFloors,
    string? WardenName,
    string? WardenPhone,
    string? WardenEmail,
    bool IsActive,
    int TotalRooms,
    int TotalCapacity,
    int OccupiedBeds
);

public record CreateHostelBlockRequest(
    string Name,
    string Code,
    HostelGenderType GenderType,
    string? CampusLocation,
    int TotalFloors,
    string? WardenName,
    string? WardenPhone,
    string? WardenEmail
);

public record UpdateHostelBlockRequest(
    string Name,
    string Code,
    HostelGenderType GenderType,
    string? CampusLocation,
    int TotalFloors,
    string? WardenName,
    string? WardenPhone,
    string? WardenEmail,
    bool IsActive
);

public record HostelRoomResponse(
    Guid Id,
    Guid HostelBlockId,
    string BlockName,
    string RoomNumber,
    int FloorLevel,
    string RoomType,
    int Capacity,
    decimal SemesterFeeRate,
    List<string> Amenities,
    RoomStatus Status,
    bool IsActive,
    int VacantBeds,
    List<HostelBedResponse> Beds
);

public record CreateHostelRoomRequest(
    Guid HostelBlockId,
    string RoomNumber,
    int FloorLevel,
    string RoomType,
    int Capacity,
    decimal SemesterFeeRate,
    List<string> Amenities
);

public record UpdateHostelRoomRequest(
    string RoomNumber,
    int FloorLevel,
    string RoomType,
    int Capacity,
    decimal SemesterFeeRate,
    List<string> Amenities,
    RoomStatus Status,
    bool IsActive
);

public record HostelBedResponse(
    Guid Id,
    Guid HostelRoomId,
    string BedLabel,
    BedStatus Status,
    Guid? CurrentStudentId,
    string? CurrentStudentName,
    string? CurrentMatricNumber
);

public record ApplyHostelRequest(
    Guid AcademicSessionId,
    Guid? PreferredBlockId,
    string? PreferredRoomType,
    string? SpecialNeeds
);

public record AssignBedRequest(
    Guid AllocationId,
    Guid HostelBedId
);

public record HostelAllocationResponse(
    Guid Id,
    Guid StudentId,
    string StudentName,
    string MatricNumber,
    string StudentGender,
    Guid AcademicSessionId,
    string SessionName,
    Guid? HostelBedId,
    string? BedLabel,
    Guid? RoomId,
    string? RoomNumber,
    Guid? BlockId,
    string? BlockName,
    Guid? PreferredBlockId,
    string? PreferredBlockName,
    string? PreferredRoomType,
    string? SpecialNeeds,
    AllocationStatus Status,
    DateTime ApplicationDate,
    DateTime? AllocatedAt,
    DateTime? CheckedInAt,
    DateTime? CheckedOutAt,
    Guid? FeeRecordId,
    decimal? FeeAmount,
    string? Notes
);

public record HostelMaintenanceRequestResponse(
    Guid Id,
    Guid HostelBlockId,
    string BlockName,
    Guid? HostelRoomId,
    string? RoomNumber,
    Guid ReportedByUserId,
    string ReportedByName,
    string Category,
    string Title,
    string Description,
    MaintenancePriority Priority,
    MaintenanceStatus Status,
    string? AssignedTo,
    string? ResolutionNotes,
    DateTime ReportedAt,
    DateTime? ResolvedAt
);

public record CreateMaintenanceRequestDto(
    Guid HostelBlockId,
    Guid? HostelRoomId,
    string Category,
    string Title,
    string Description,
    MaintenancePriority Priority
);

public record UpdateMaintenanceStatusDto(
    MaintenanceStatus Status,
    string? AssignedTo,
    string? ResolutionNotes
);

public record HostelExeatResponse(
    Guid Id,
    Guid StudentId,
    string StudentName,
    string MatricNumber,
    Guid? HostelAllocationId,
    string? RoomAndBlockInfo,
    DateTime DepartureTime,
    DateTime ExpectedReturnTime,
    DateTime? ActualReturnTime,
    string Destination,
    string Reason,
    ExeatStatus Status,
    Guid? ApprovedByUserId,
    string? ApprovedByName,
    string? WardenRemarks,
    bool ParentApproved,
    DateTime RequestedAt,
    DateTime? DecidedAt
);

public record ApplyExeatRequest(
    DateTime DepartureTime,
    DateTime ExpectedReturnTime,
    string Destination,
    string Reason
);

public record ApproveExeatRequest(
    bool Approve,
    string? WardenRemarks
);

public record HostelDeviceResponse(
    Guid Id,
    Guid StudentId,
    string StudentName,
    string MatricNumber,
    Guid? HostelAllocationId,
    string? RoomAndBlockInfo,
    HostelDeviceType DeviceType,
    string Brand,
    string ModelNameNumber,
    string SerialNumber,
    string? MacAddressOrImei,
    string? ColorAndDescription,
    string? ProofOfOwnershipUrl,
    HostelDeviceStatus Status,
    DateTime RegisteredAt,
    Guid? VerifiedByUserId,
    string? VerifiedByName,
    DateTime? VerifiedAt,
    string? VerificationNotes,
    bool IsActive
);

public record RegisterHostelDeviceRequest(
    HostelDeviceType DeviceType,
    string Brand,
    string ModelNameNumber,
    string SerialNumber,
    string? MacAddressOrImei,
    string? ColorAndDescription,
    string? ProofOfOwnershipUrl
);

public record VerifyHostelDeviceRequest(
    HostelDeviceStatus Status,
    string? VerificationNotes
);

public record HostelStatsResponse(
    int TotalBlocks,
    int TotalRooms,
    int TotalCapacity,
    int OccupiedBeds,
    int VacantBeds,
    int MaintenanceRooms,
    int PendingApplications,
    int OpenMaintenanceIssues,
    int ActiveExeats,
    int TotalDevicesCount,
    int PendingDeviceVerificationsCount
);

