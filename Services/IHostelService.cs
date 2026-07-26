using LMS.Api.Contracts;
using LMS.Api.Data.Enums;

namespace LMS.Api.Services;

public interface IHostelService
{
    // Blocks
    Task<IEnumerable<HostelBlockResponse>> GetBlocksAsync(bool? activeOnly = null);
    Task<HostelBlockResponse> GetBlockByIdAsync(Guid id);
    Task<HostelBlockResponse> CreateBlockAsync(CreateHostelBlockRequest req, Guid currentUserId);
    Task<HostelBlockResponse> UpdateBlockAsync(Guid id, UpdateHostelBlockRequest req, Guid currentUserId);
    Task<bool> DeleteBlockAsync(Guid id);

    // Rooms & Beds
    Task<IEnumerable<HostelRoomResponse>> GetRoomsAsync(Guid? blockId = null, RoomStatus? status = null);
    Task<HostelRoomResponse> GetRoomByIdAsync(Guid id);
    Task<HostelRoomResponse> CreateRoomAsync(CreateHostelRoomRequest req, Guid currentUserId);
    Task<HostelRoomResponse> UpdateRoomAsync(Guid id, UpdateHostelRoomRequest req, Guid currentUserId);

    // Allocations
    Task<IEnumerable<HostelAllocationResponse>> GetAllocationsAsync(Guid? sessionId = null, AllocationStatus? status = null, Guid? studentId = null);
    Task<HostelAllocationResponse?> GetStudentActiveAllocationAsync(Guid studentId);
    Task<HostelAllocationResponse> ApplyForHostelAsync(Guid studentId, ApplyHostelRequest req);
    Task<HostelAllocationResponse> AssignBedAsync(AssignBedRequest req, Guid currentUserId);
    Task<int> AutoAllocateAsync(Guid sessionId, Guid currentUserId);
    Task<HostelAllocationResponse> CheckInAsync(Guid allocationId, Guid currentUserId);
    Task<HostelAllocationResponse> CheckOutAsync(Guid allocationId, Guid currentUserId);
    Task<HostelAllocationResponse> CancelAllocationAsync(Guid allocationId, Guid currentUserId);

    // Maintenance
    Task<IEnumerable<HostelMaintenanceRequestResponse>> GetMaintenanceRequestsAsync(Guid? blockId = null, MaintenanceStatus? status = null);
    Task<HostelMaintenanceRequestResponse> CreateMaintenanceRequestAsync(Guid currentUserId, CreateMaintenanceRequestDto req);
    Task<HostelMaintenanceRequestResponse> UpdateMaintenanceStatusAsync(Guid id, UpdateMaintenanceStatusDto req);

    // Exeats
    Task<IEnumerable<HostelExeatResponse>> GetExeatRequestsAsync(Guid? studentId = null, ExeatStatus? status = null);
    Task<HostelExeatResponse> ApplyExeatAsync(Guid studentId, ApplyExeatRequest req);
    Task<HostelExeatResponse> ApproveExeatAsync(Guid exeatId, ApproveExeatRequest req, Guid wardenUserId);
    Task<HostelExeatResponse> MarkExeatReturnAsync(Guid exeatId, Guid wardenUserId);

    // Devices
    Task<IEnumerable<HostelDeviceResponse>> GetRegisteredDevicesAsync(Guid? studentId = null, Guid? blockId = null, HostelDeviceStatus? status = null, string? search = null);
    Task<IEnumerable<HostelDeviceResponse>> GetStudentDevicesAsync(Guid studentId);
    Task<HostelDeviceResponse> RegisterDeviceAsync(Guid studentId, RegisterHostelDeviceRequest req);
    Task<HostelDeviceResponse> VerifyDeviceAsync(Guid deviceId, VerifyHostelDeviceRequest req, Guid wardenUserId);
    Task<HostelDeviceResponse> DecommissionDeviceAsync(Guid deviceId, Guid currentUserId);

    // Stats
    Task<HostelStatsResponse> GetHostelStatsAsync();
}

