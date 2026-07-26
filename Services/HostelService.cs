using LMS.Api.Contracts;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using LMS.Api.Data.Enums;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Services;

public class HostelService(LmsDbContext db, ILogger<HostelService> logger) : IHostelService
{
    // ==================== BLOCKS ====================

    public async Task<IEnumerable<HostelBlockResponse>> GetBlocksAsync(bool? activeOnly = null)
    {
        var query = db.HostelBlocks
            .Include(b => b.Rooms)
                .ThenInclude(r => r.Beds)
            .AsNoTracking();

        if (activeOnly == true)
        {
            query = query.Where(b => b.IsActive);
        }

        var blocks = await query.ToListAsync();

        return blocks.Select(MapBlockToResponse);
    }

    public async Task<HostelBlockResponse> GetBlockByIdAsync(Guid id)
    {
        var block = await db.HostelBlocks
            .Include(b => b.Rooms)
                .ThenInclude(r => r.Beds)
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == id)
            ?? throw new KeyNotFoundException("Hostel block not found.");

        return MapBlockToResponse(block);
    }

    public async Task<HostelBlockResponse> CreateBlockAsync(CreateHostelBlockRequest req, Guid currentUserId)
    {
        var block = new HostelBlock
        {
            Name = req.Name,
            Code = req.Code.ToUpper(),
            GenderType = req.GenderType,
            CampusLocation = req.CampusLocation,
            TotalFloors = req.TotalFloors,
            WardenName = req.WardenName,
            WardenPhone = req.WardenPhone,
            WardenEmail = req.WardenEmail,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = currentUserId
        };

        db.HostelBlocks.Add(block);
        await db.SaveChangesAsync();

        return await GetBlockByIdAsync(block.Id);
    }

    public async Task<HostelBlockResponse> UpdateBlockAsync(Guid id, UpdateHostelBlockRequest req, Guid currentUserId)
    {
        var block = await db.HostelBlocks.FindAsync(id)
            ?? throw new KeyNotFoundException("Hostel block not found.");

        block.Name = req.Name;
        block.Code = req.Code.ToUpper();
        block.GenderType = req.GenderType;
        block.CampusLocation = req.CampusLocation;
        block.TotalFloors = req.TotalFloors;
        block.WardenName = req.WardenName;
        block.WardenPhone = req.WardenPhone;
        block.WardenEmail = req.WardenEmail;
        block.IsActive = req.IsActive;
        block.UpdatedAt = DateTime.UtcNow;
        block.UpdatedBy = currentUserId;

        await db.SaveChangesAsync();
        return await GetBlockByIdAsync(id);
    }

    public async Task<bool> DeleteBlockAsync(Guid id)
    {
        var block = await db.HostelBlocks
            .Include(b => b.Rooms)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (block == null) return false;

        if (block.Rooms.Any())
        {
            // Soft delete/deactivate if has rooms
            block.IsActive = false;
        }
        else
        {
            db.HostelBlocks.Remove(block);
        }

        await db.SaveChangesAsync();
        return true;
    }

    // ==================== ROOMS & BEDS ====================

    public async Task<IEnumerable<HostelRoomResponse>> GetRoomsAsync(Guid? blockId = null, RoomStatus? status = null)
    {
        var query = db.HostelRooms
            .Include(r => r.HostelBlock)
            .Include(r => r.Beds)
                .ThenInclude(b => b.CurrentStudent)
            .AsNoTracking();

        if (blockId.HasValue)
        {
            query = query.Where(r => r.HostelBlockId == blockId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(r => r.Status == status.Value);
        }

        var rooms = await query.ToListAsync();
        return rooms.Select(MapRoomToResponse);
    }

    public async Task<HostelRoomResponse> GetRoomByIdAsync(Guid id)
    {
        var room = await db.HostelRooms
            .Include(r => r.HostelBlock)
            .Include(r => r.Beds)
                .ThenInclude(b => b.CurrentStudent)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id)
            ?? throw new KeyNotFoundException("Hostel room not found.");

        return MapRoomToResponse(room);
    }

    public async Task<HostelRoomResponse> CreateRoomAsync(CreateHostelRoomRequest req, Guid currentUserId)
    {
        var block = await db.HostelBlocks.FindAsync(req.HostelBlockId)
            ?? throw new KeyNotFoundException("Hostel block not found.");

        var room = new HostelRoom
        {
            HostelBlockId = req.HostelBlockId,
            RoomNumber = req.RoomNumber,
            FloorLevel = req.FloorLevel,
            RoomType = req.RoomType,
            Capacity = req.Capacity,
            SemesterFeeRate = req.SemesterFeeRate,
            AmenitiesJson = System.Text.Json.JsonSerializer.Serialize(req.Amenities ?? []),
            Status = RoomStatus.Available,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = currentUserId
        };

        db.HostelRooms.Add(room);
        await db.SaveChangesAsync();

        // Create beds based on capacity
        for (int i = 1; i <= req.Capacity; i++)
        {
            char bedLetter = (char)('A' + i - 1);
            db.HostelBeds.Add(new HostelBed
            {
                HostelRoomId = room.Id,
                BedLabel = $"Bed {bedLetter}",
                Status = BedStatus.Vacant
            });
        }

        await db.SaveChangesAsync();

        return await GetRoomByIdAsync(room.Id);
    }

    public async Task<HostelRoomResponse> UpdateRoomAsync(Guid id, UpdateHostelRoomRequest req, Guid currentUserId)
    {
        var room = await db.HostelRooms
            .Include(r => r.Beds)
            .FirstOrDefaultAsync(r => r.Id == id)
            ?? throw new KeyNotFoundException("Hostel room not found.");

        room.RoomNumber = req.RoomNumber;
        room.FloorLevel = req.FloorLevel;
        room.RoomType = req.RoomType;
        room.SemesterFeeRate = req.SemesterFeeRate;
        room.AmenitiesJson = System.Text.Json.JsonSerializer.Serialize(req.Amenities ?? []);
        room.Status = req.Status;
        room.IsActive = req.IsActive;

        // If capacity increased, add beds
        if (req.Capacity > room.Capacity)
        {
            int currentCount = room.Beds.Count;
            for (int i = currentCount + 1; i <= req.Capacity; i++)
            {
                char bedLetter = (char)('A' + i - 1);
                db.HostelBeds.Add(new HostelBed
                {
                    HostelRoomId = room.Id,
                    BedLabel = $"Bed {bedLetter}",
                    Status = BedStatus.Vacant
                });
            }
            room.Capacity = req.Capacity;
        }
        else if (req.Capacity < room.Capacity)
        {
            int occupiedCount = room.Beds.Count(b => b.Status == BedStatus.Occupied);
            if (req.Capacity < occupiedCount)
            {
                throw new InvalidOperationException($"Cannot reduce capacity to {req.Capacity} because {occupiedCount} beds are currently occupied.");
            }
            room.Capacity = req.Capacity;
        }

        await db.SaveChangesAsync();
        return await GetRoomByIdAsync(id);
    }

    // ==================== ALLOCATIONS ====================

    public async Task<IEnumerable<HostelAllocationResponse>> GetAllocationsAsync(Guid? sessionId = null, AllocationStatus? status = null, Guid? studentId = null)
    {
        var query = db.HostelAllocations
            .Include(a => a.Student)
            .Include(a => a.AcademicSession)
            .Include(a => a.HostelBed)
                .ThenInclude(b => b!.HostelRoom)
                    .ThenInclude(r => r!.HostelBlock)
            .Include(a => a.PreferredBlock)
            .Include(a => a.FeeRecord)
            .AsNoTracking();

        if (sessionId.HasValue)
        {
            query = query.Where(a => a.AcademicSessionId == sessionId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(a => a.Status == status.Value);
        }

        if (studentId.HasValue)
        {
            query = query.Where(a => a.StudentId == studentId.Value);
        }

        var allocations = await query.OrderByDescending(a => a.ApplicationDate).ToListAsync();
        return allocations.Select(MapAllocationToResponse);
    }

    public async Task<HostelAllocationResponse?> GetStudentActiveAllocationAsync(Guid studentId)
    {
        var allocation = await db.HostelAllocations
            .Include(a => a.Student)
            .Include(a => a.AcademicSession)
            .Include(a => a.HostelBed)
                .ThenInclude(b => b!.HostelRoom)
                    .ThenInclude(r => r!.HostelBlock)
            .Include(a => a.PreferredBlock)
            .Include(a => a.FeeRecord)
            .AsNoTracking()
            .Where(a => a.StudentId == studentId && 
                        (a.Status == AllocationStatus.Allocated || a.Status == AllocationStatus.CheckedIn || a.Status == AllocationStatus.Pending || a.Status == AllocationStatus.Approved))
            .OrderByDescending(a => a.ApplicationDate)
            .FirstOrDefaultAsync();

        return allocation == null ? null : MapAllocationToResponse(allocation);
    }

    public async Task<HostelAllocationResponse> ApplyForHostelAsync(Guid studentId, ApplyHostelRequest req)
    {
        var student = await db.Students.FindAsync(studentId)
            ?? throw new KeyNotFoundException("Student record not found.");

        // Check if student already has a pending or active allocation for this session
        var existing = await db.HostelAllocations
            .FirstOrDefaultAsync(a => a.StudentId == studentId && 
                                      a.AcademicSessionId == req.AcademicSessionId && 
                                      a.Status != AllocationStatus.Cancelled && 
                                      a.Status != AllocationStatus.Rejected && 
                                      a.Status != AllocationStatus.CheckedOut);

        if (existing != null)
        {
            throw new InvalidOperationException("You already have an active hostel application or allocation for this academic session.");
        }

        var allocation = new HostelAllocation
        {
            StudentId = studentId,
            AcademicSessionId = req.AcademicSessionId,
            PreferredBlockId = req.PreferredBlockId,
            PreferredRoomType = req.PreferredRoomType,
            SpecialNeeds = req.SpecialNeeds,
            Status = AllocationStatus.Pending,
            ApplicationDate = DateTime.UtcNow
        };

        db.HostelAllocations.Add(allocation);
        await db.SaveChangesAsync();

        return (await GetAllocationsAsync(studentId: studentId, status: AllocationStatus.Pending)).First();
    }

    public async Task<HostelAllocationResponse> AssignBedAsync(AssignBedRequest req, Guid currentUserId)
    {
        var allocation = await db.HostelAllocations
            .Include(a => a.Student)
            .FirstOrDefaultAsync(a => a.Id == req.AllocationId)
            ?? throw new KeyNotFoundException("Hostel allocation application not found.");

        var bed = await db.HostelBeds
            .Include(b => b.HostelRoom)
                .ThenInclude(r => r!.HostelBlock)
            .FirstOrDefaultAsync(b => b.Id == req.HostelBedId)
            ?? throw new KeyNotFoundException("Selected hostel bed not found.");

        if (bed.Status != BedStatus.Vacant)
        {
            throw new InvalidOperationException($"Bed {bed.BedLabel} in Room {bed.HostelRoom?.RoomNumber} is not vacant.");
        }

        // Validate gender
        var studentGender = allocation.Student?.Gender ?? "";
        var blockGender = bed.HostelRoom?.HostelBlock?.GenderType;

        if (blockGender == HostelGenderType.Male && !studentGender.StartsWith("M", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Cannot assign female student to a male hostel block.");
        }
        if (blockGender == HostelGenderType.Female && !studentGender.StartsWith("F", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Cannot assign male student to a female hostel block.");
        }

        // Free up old bed if re-assigning
        if (allocation.HostelBedId.HasValue && allocation.HostelBedId.Value != req.HostelBedId)
        {
            var oldBed = await db.HostelBeds.FindAsync(allocation.HostelBedId.Value);
            if (oldBed != null)
            {
                oldBed.Status = BedStatus.Vacant;
                oldBed.CurrentStudentId = null;
            }
        }

        // Assign bed
        bed.Status = BedStatus.Occupied;
        bed.CurrentStudentId = allocation.StudentId;

        allocation.HostelBedId = req.HostelBedId;
        allocation.Status = AllocationStatus.Allocated;
        allocation.AllocatedAt = DateTime.UtcNow;

        // Ensure room status reflects occupancy
        var room = bed.HostelRoom;
        if (room != null)
        {
            var occupiedBedsCount = await db.HostelBeds.CountAsync(b => b.HostelRoomId == room.Id && b.Status == BedStatus.Occupied);
            if (occupiedBedsCount + 1 >= room.Capacity)
            {
                room.Status = RoomStatus.Occupied;
            }
        }

        // Generate / link fee record if room rate is > 0
        if (room != null && room.SemesterFeeRate > 0)
        {
            var feeRecord = await db.StudentFeeRecords
                .FirstOrDefaultAsync(f => f.StudentId == allocation.StudentId && f.SessionId == allocation.AcademicSessionId);

            if (feeRecord != null)
            {
                allocation.FeeRecordId = feeRecord.Id;
            }
        }

        await db.SaveChangesAsync();
        return (await GetAllocationsAsync(studentId: allocation.StudentId)).First(a => a.Id == allocation.Id);
    }

    public async Task<int> AutoAllocateAsync(Guid sessionId, Guid currentUserId)
    {
        var pendingAllocations = await db.HostelAllocations
            .Include(a => a.Student)
            .Where(a => a.AcademicSessionId == sessionId && a.Status == AllocationStatus.Pending)
            .OrderBy(a => a.ApplicationDate)
            .ToListAsync();

        int allocatedCount = 0;

        foreach (var allocation in pendingAllocations)
        {
            var studentGender = allocation.Student?.Gender ?? "";
            bool isMale = studentGender.StartsWith("M", StringComparison.OrdinalIgnoreCase);
            bool isFemale = studentGender.StartsWith("F", StringComparison.OrdinalIgnoreCase);

            var vacantBedsQuery = db.HostelBeds
                .Include(b => b.HostelRoom)
                    .ThenInclude(r => r!.HostelBlock)
                .Where(b => b.Status == BedStatus.Vacant && b.HostelRoom!.IsActive && b.HostelRoom.HostelBlock!.IsActive);

            if (isMale)
            {
                vacantBedsQuery = vacantBedsQuery.Where(b => b.HostelRoom!.HostelBlock!.GenderType == HostelGenderType.Male || b.HostelRoom.HostelBlock.GenderType == HostelGenderType.Coed);
            }
            else if (isFemale)
            {
                vacantBedsQuery = vacantBedsQuery.Where(b => b.HostelRoom!.HostelBlock!.GenderType == HostelGenderType.Female || b.HostelRoom.HostelBlock.GenderType == HostelGenderType.Coed);
            }

            // Prioritize preferred block if specified
            if (allocation.PreferredBlockId.HasValue)
            {
                var prefBed = await vacantBedsQuery.FirstOrDefaultAsync(b => b.HostelRoom!.HostelBlockId == allocation.PreferredBlockId.Value);
                if (prefBed != null)
                {
                    await AssignBedAsync(new AssignBedRequest(allocation.Id, prefBed.Id), currentUserId);
                    allocatedCount++;
                    continue;
                }
            }

            // Otherwise take any available vacant bed
            var anyBed = await vacantBedsQuery.FirstOrDefaultAsync();
            if (anyBed != null)
            {
                await AssignBedAsync(new AssignBedRequest(allocation.Id, anyBed.Id), currentUserId);
                allocatedCount++;
            }
        }

        return allocatedCount;
    }

    public async Task<HostelAllocationResponse> CheckInAsync(Guid allocationId, Guid currentUserId)
    {
        var allocation = await db.HostelAllocations.FindAsync(allocationId)
            ?? throw new KeyNotFoundException("Hostel allocation not found.");

        if (allocation.Status != AllocationStatus.Allocated)
        {
            throw new InvalidOperationException("Allocation must be in 'Allocated' state before check-in.");
        }

        allocation.Status = AllocationStatus.CheckedIn;
        allocation.CheckedInAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return (await GetAllocationsAsync(studentId: allocation.StudentId)).First(a => a.Id == allocation.Id);
    }

    public async Task<HostelAllocationResponse> CheckOutAsync(Guid allocationId, Guid currentUserId)
    {
        var allocation = await db.HostelAllocations.FindAsync(allocationId)
            ?? throw new KeyNotFoundException("Hostel allocation not found.");

        if (allocation.HostelBedId.HasValue)
        {
            var bed = await db.HostelBeds
                .Include(b => b.HostelRoom)
                .FirstOrDefaultAsync(b => b.Id == allocation.HostelBedId.Value);

            if (bed != null)
            {
                bed.Status = BedStatus.Vacant;
                bed.CurrentStudentId = null;

                if (bed.HostelRoom != null && bed.HostelRoom.Status == RoomStatus.Occupied)
                {
                    bed.HostelRoom.Status = RoomStatus.Available;
                }
            }
        }

        allocation.Status = AllocationStatus.CheckedOut;
        allocation.CheckedOutAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return (await GetAllocationsAsync(studentId: allocation.StudentId)).First(a => a.Id == allocation.Id);
    }

    public async Task<HostelAllocationResponse> CancelAllocationAsync(Guid allocationId, Guid currentUserId)
    {
        var allocation = await db.HostelAllocations.FindAsync(allocationId)
            ?? throw new KeyNotFoundException("Hostel allocation not found.");

        if (allocation.HostelBedId.HasValue)
        {
            var bed = await db.HostelBeds.FindAsync(allocation.HostelBedId.Value);
            if (bed != null)
            {
                bed.Status = BedStatus.Vacant;
                bed.CurrentStudentId = null;
            }
        }

        allocation.Status = AllocationStatus.Cancelled;
        await db.SaveChangesAsync();

        return (await GetAllocationsAsync(studentId: allocation.StudentId)).First(a => a.Id == allocation.Id);
    }

    // ==================== MAINTENANCE ====================

    public async Task<IEnumerable<HostelMaintenanceRequestResponse>> GetMaintenanceRequestsAsync(Guid? blockId = null, MaintenanceStatus? status = null)
    {
        var query = db.HostelMaintenanceRequests
            .Include(m => m.HostelBlock)
            .Include(m => m.HostelRoom)
            .Include(m => m.ReportedByUser)
            .AsNoTracking();

        if (blockId.HasValue)
        {
            query = query.Where(m => m.HostelBlockId == blockId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(m => m.Status == status.Value);
        }

        var requests = await query.OrderByDescending(m => m.ReportedAt).ToListAsync();

        return requests.Select(m => new HostelMaintenanceRequestResponse(
            m.Id,
            m.HostelBlockId,
            m.HostelBlock?.Name ?? "",
            m.HostelRoomId,
            m.HostelRoom?.RoomNumber,
            m.ReportedByUserId,
            m.ReportedByUser?.DisplayName ?? m.ReportedByUser?.Username ?? "Unknown",
            m.Category,
            m.Title,
            m.Description,
            m.Priority,
            m.Status,
            m.AssignedTo,
            m.ResolutionNotes,
            m.ReportedAt,
            m.ResolvedAt
        ));
    }

    public async Task<HostelMaintenanceRequestResponse> CreateMaintenanceRequestAsync(Guid currentUserId, CreateMaintenanceRequestDto req)
    {
        var block = await db.HostelBlocks.FindAsync(req.HostelBlockId)
            ?? throw new KeyNotFoundException("Hostel block not found.");

        var request = new HostelMaintenanceRequest
        {
            HostelBlockId = req.HostelBlockId,
            HostelRoomId = req.HostelRoomId,
            ReportedByUserId = currentUserId,
            Category = req.Category,
            Title = req.Title,
            Description = req.Description,
            Priority = req.Priority,
            Status = MaintenanceStatus.Open,
            ReportedAt = DateTime.UtcNow
        };

        db.HostelMaintenanceRequests.Add(request);
        await db.SaveChangesAsync();

        return (await GetMaintenanceRequestsAsync(status: MaintenanceStatus.Open)).First(m => m.Id == request.Id);
    }

    public async Task<HostelMaintenanceRequestResponse> UpdateMaintenanceStatusAsync(Guid id, UpdateMaintenanceStatusDto req)
    {
        var request = await db.HostelMaintenanceRequests.FindAsync(id)
            ?? throw new KeyNotFoundException("Maintenance request not found.");

        request.Status = req.Status;
        if (!string.IsNullOrWhiteSpace(req.AssignedTo)) request.AssignedTo = req.AssignedTo;
        if (!string.IsNullOrWhiteSpace(req.ResolutionNotes)) request.ResolutionNotes = req.ResolutionNotes;

        if (req.Status == MaintenanceStatus.Resolved || req.Status == MaintenanceStatus.Closed)
        {
            request.ResolvedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();
        return (await GetMaintenanceRequestsAsync()).First(m => m.Id == id);
    }

    // ==================== EXEATS ====================

    public async Task<IEnumerable<HostelExeatResponse>> GetExeatRequestsAsync(Guid? studentId = null, ExeatStatus? status = null)
    {
        var query = db.HostelExeats
            .Include(e => e.Student)
            .Include(e => e.HostelAllocation)
                .ThenInclude(a => a!.HostelBed)
                    .ThenInclude(b => b!.HostelRoom)
                        .ThenInclude(r => r!.HostelBlock)
            .Include(e => e.ApprovedByUser)
            .AsNoTracking();

        if (studentId.HasValue)
        {
            query = query.Where(e => e.StudentId == studentId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(e => e.Status == status.Value);
        }

        var exeats = await query.OrderByDescending(e => e.RequestedAt).ToListAsync();

        return exeats.Select(e =>
        {
            var studentName = $"{e.Student?.FirstName} {e.Student?.LastName}".Trim();
            var matric = e.Student?.StudentNumber ?? "";
            var bed = e.HostelAllocation?.HostelBed;
            var roomInfo = bed != null ? $"Room {bed.HostelRoom?.RoomNumber} ({bed.HostelRoom?.HostelBlock?.Name})" : "Unassigned";

            return new HostelExeatResponse(
                e.Id,
                e.StudentId,
                studentName,
                matric,
                e.HostelAllocationId,
                roomInfo,
                e.DepartureTime,
                e.ExpectedReturnTime,
                e.ActualReturnTime,
                e.Destination,
                e.Reason,
                e.Status,
                e.ApprovedByUserId,
                e.ApprovedByUser?.DisplayName ?? e.ApprovedByUser?.Username,
                e.WardenRemarks,
                e.ParentApproved,
                e.RequestedAt,
                e.DecidedAt
            );
        });
    }

    public async Task<HostelExeatResponse> ApplyExeatAsync(Guid studentId, ApplyExeatRequest req)
    {
        var activeAllocation = await db.HostelAllocations
            .FirstOrDefaultAsync(a => a.StudentId == studentId && (a.Status == AllocationStatus.Allocated || a.Status == AllocationStatus.CheckedIn));

        var exeat = new HostelExeat
        {
            StudentId = studentId,
            HostelAllocationId = activeAllocation?.Id,
            DepartureTime = req.DepartureTime,
            ExpectedReturnTime = req.ExpectedReturnTime,
            Destination = req.Destination,
            Reason = req.Reason,
            Status = ExeatStatus.Pending,
            RequestedAt = DateTime.UtcNow
        };

        db.HostelExeats.Add(exeat);
        await db.SaveChangesAsync();

        return (await GetExeatRequestsAsync(studentId: studentId)).First(e => e.Id == exeat.Id);
    }

    public async Task<HostelExeatResponse> ApproveExeatAsync(Guid exeatId, ApproveExeatRequest req, Guid wardenUserId)
    {
        var exeat = await db.HostelExeats.FindAsync(exeatId)
            ?? throw new KeyNotFoundException("Exeat request not found.");

        exeat.Status = req.Approve ? ExeatStatus.Approved : ExeatStatus.Rejected;
        exeat.ApprovedByUserId = wardenUserId;
        exeat.WardenRemarks = req.WardenRemarks;
        exeat.DecidedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return (await GetExeatRequestsAsync()).First(e => e.Id == exeatId);
    }

    public async Task<HostelExeatResponse> MarkExeatReturnAsync(Guid exeatId, Guid wardenUserId)
    {
        var exeat = await db.HostelExeats.FindAsync(exeatId)
            ?? throw new KeyNotFoundException("Exeat request not found.");

        exeat.Status = ExeatStatus.Returned;
        exeat.ActualReturnTime = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return (await GetExeatRequestsAsync()).First(e => e.Id == exeatId);
    }

    // ==================== STATS ====================

    public async Task<HostelStatsResponse> GetHostelStatsAsync()
    {
        var totalBlocks = await db.HostelBlocks.CountAsync(b => b.IsActive);
        var totalRooms = await db.HostelRooms.CountAsync(r => r.IsActive);
        var totalCapacity = await db.HostelRooms.Where(r => r.IsActive).SumAsync(r => r.Capacity);
        var occupiedBeds = await db.HostelBeds.CountAsync(b => b.Status == BedStatus.Occupied);
        var vacantBeds = await db.HostelBeds.CountAsync(b => b.Status == BedStatus.Vacant);
        var maintenanceRooms = await db.HostelRooms.CountAsync(r => r.Status == RoomStatus.Maintenance);
        var pendingApplications = await db.HostelAllocations.CountAsync(a => a.Status == AllocationStatus.Pending);
        var openMaintenance = await db.HostelMaintenanceRequests.CountAsync(m => m.Status == MaintenanceStatus.Open || m.Status == MaintenanceStatus.InProgress);
        var activeExeats = await db.HostelExeats.CountAsync(e => e.Status == ExeatStatus.Approved || e.Status == ExeatStatus.CheckedOut);
        var totalDevices = await db.HostelDevices.CountAsync(d => d.IsActive);
        var pendingVerifications = await db.HostelDevices.CountAsync(d => d.Status == HostelDeviceStatus.PendingVerification && d.IsActive);

        return new HostelStatsResponse(
            totalBlocks,
            totalRooms,
            totalCapacity,
            occupiedBeds,
            vacantBeds,
            maintenanceRooms,
            pendingApplications,
            openMaintenance,
            activeExeats,
            totalDevices,
            pendingVerifications
        );
    }

    // ==================== DEVICES ====================

    public async Task<IEnumerable<HostelDeviceResponse>> GetRegisteredDevicesAsync(Guid? studentId = null, Guid? blockId = null, HostelDeviceStatus? status = null, string? search = null)
    {
        var query = db.HostelDevices
            .Include(d => d.Student)
            .Include(d => d.HostelAllocation)
                .ThenInclude(a => a!.HostelBed)
                .ThenInclude(b => b!.HostelRoom)
                .ThenInclude(r => r!.HostelBlock)
            .Include(d => d.VerifiedByUser)
            .AsNoTracking();

        if (studentId.HasValue)
        {
            query = query.Where(d => d.StudentId == studentId.Value);
        }

        if (blockId.HasValue)
        {
            query = query.Where(d => d.HostelAllocation != null && 
                                     d.HostelAllocation.HostelBed != null && 
                                     d.HostelAllocation.HostelBed.HostelRoom != null && 
                                     d.HostelAllocation.HostelBed.HostelRoom.HostelBlockId == blockId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(d => d.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(d => d.SerialNumber.ToLower().Contains(s) ||
                                     d.Brand.ToLower().Contains(s) ||
                                     d.ModelNameNumber.ToLower().Contains(s) ||
                                     (d.Student != null && (d.Student.FirstName.ToLower().Contains(s) || d.Student.LastName.ToLower().Contains(s) || d.Student.StudentNumber.ToLower().Contains(s))));
        }

        var devices = await query.OrderByDescending(d => d.RegisteredAt).ToListAsync();
        return devices.Select(MapDeviceToResponse);
    }

    public async Task<IEnumerable<HostelDeviceResponse>> GetStudentDevicesAsync(Guid studentId)
    {
        return await GetRegisteredDevicesAsync(studentId: studentId);
    }

    public async Task<HostelDeviceResponse> RegisterDeviceAsync(Guid studentId, RegisterHostelDeviceRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.SerialNumber))
        {
            throw new InvalidOperationException("Serial number is required for device registration.");
        }

        var activeAllocation = await db.HostelAllocations
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.StudentId == studentId && 
                                     (a.Status == AllocationStatus.Allocated || a.Status == AllocationStatus.CheckedIn));

        var existing = await db.HostelDevices
            .AnyAsync(d => d.SerialNumber.ToLower() == req.SerialNumber.Trim().ToLower() && d.IsActive && d.Status != HostelDeviceStatus.Decommissioned);
        if (existing)
        {
            throw new InvalidOperationException($"A device with serial number '{req.SerialNumber}' is already registered in the hostel system.");
        }

        var device = new HostelDevice
        {
            StudentId = studentId,
            HostelAllocationId = activeAllocation?.Id,
            DeviceType = req.DeviceType,
            Brand = req.Brand.Trim(),
            ModelNameNumber = req.ModelNameNumber.Trim(),
            SerialNumber = req.SerialNumber.Trim(),
            MacAddressOrImei = req.MacAddressOrImei?.Trim(),
            ColorAndDescription = req.ColorAndDescription?.Trim(),
            ProofOfOwnershipUrl = req.ProofOfOwnershipUrl?.Trim(),
            Status = HostelDeviceStatus.PendingVerification,
            RegisteredAt = DateTime.UtcNow,
            IsActive = true
        };

        db.HostelDevices.Add(device);
        await db.SaveChangesAsync();

        return (await GetRegisteredDevicesAsync(studentId: studentId)).First(d => d.Id == device.Id);
    }

    public async Task<HostelDeviceResponse> VerifyDeviceAsync(Guid deviceId, VerifyHostelDeviceRequest req, Guid wardenUserId)
    {
        var device = await db.HostelDevices.FindAsync(deviceId)
            ?? throw new KeyNotFoundException("Hostel device registration not found.");

        device.Status = req.Status;
        device.VerifiedByUserId = wardenUserId;
        device.VerifiedAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(req.VerificationNotes))
        {
            device.VerificationNotes = req.VerificationNotes.Trim();
        }

        await db.SaveChangesAsync();

        var list = await GetRegisteredDevicesAsync(studentId: device.StudentId);
        return list.First(d => d.Id == deviceId);
    }

    public async Task<HostelDeviceResponse> DecommissionDeviceAsync(Guid deviceId, Guid currentUserId)
    {
        var device = await db.HostelDevices.FindAsync(deviceId)
            ?? throw new KeyNotFoundException("Hostel device registration not found.");

        device.Status = HostelDeviceStatus.Decommissioned;
        device.IsActive = false;
        await db.SaveChangesAsync();

        var list = await GetRegisteredDevicesAsync(studentId: device.StudentId);
        return list.First(d => d.Id == deviceId);
    }

    private static HostelDeviceResponse MapDeviceToResponse(HostelDevice d)
    {
        var studentName = d.Student != null ? $"{d.Student.FirstName} {d.Student.LastName}".Trim() : "Unknown";
        var matric = d.Student?.StudentNumber ?? "";

        string? roomAndBlockInfo = null;
        var room = d.HostelAllocation?.HostelBed?.HostelRoom;
        var block = room?.HostelBlock;
        if (block != null || room != null)
        {
            roomAndBlockInfo = $"{block?.Name ?? "Block"} - Room {room?.RoomNumber ?? "N/A"}";
        }

        var verifierName = d.VerifiedByUser?.DisplayName ?? d.VerifiedByUser?.Username;

        return new HostelDeviceResponse(
            d.Id,
            d.StudentId,
            studentName,
            matric,
            d.HostelAllocationId,
            roomAndBlockInfo,
            d.DeviceType,
            d.Brand,
            d.ModelNameNumber,
            d.SerialNumber,
            d.MacAddressOrImei,
            d.ColorAndDescription,
            d.ProofOfOwnershipUrl,
            d.Status,
            d.RegisteredAt,
            d.VerifiedByUserId,
            verifierName,
            d.VerifiedAt,
            d.VerificationNotes,
            d.IsActive
        );
    }


    // ==================== MAPPER HELPERS ====================

    private static HostelBlockResponse MapBlockToResponse(HostelBlock b)
    {
        int roomCount = b.Rooms?.Count ?? 0;
        int totalCap = b.Rooms?.Sum(r => r.Capacity) ?? 0;
        int occBeds = b.Rooms?.SelectMany(r => r.Beds).Count(bed => bed.Status == BedStatus.Occupied) ?? 0;

        return new HostelBlockResponse(
            b.Id,
            b.Name,
            b.Code,
            b.GenderType,
            b.CampusLocation,
            b.TotalFloors,
            b.WardenName,
            b.WardenPhone,
            b.WardenEmail,
            b.IsActive,
            roomCount,
            totalCap,
            occBeds
        );
    }

    private static HostelRoomResponse MapRoomToResponse(HostelRoom r)
    {
        List<string> amenities = [];
        try
        {
            if (!string.IsNullOrWhiteSpace(r.AmenitiesJson))
            {
                amenities = System.Text.Json.JsonSerializer.Deserialize<List<string>>(r.AmenitiesJson) ?? [];
            }
        }
        catch { }

        var beds = r.Beds?.Select(b => new HostelBedResponse(
            b.Id,
            b.HostelRoomId,
            b.BedLabel,
            b.Status,
            b.CurrentStudentId,
            b.CurrentStudent != null ? $"{b.CurrentStudent.FirstName} {b.CurrentStudent.LastName}".Trim() : null,
            b.CurrentStudent?.StudentNumber
        )).ToList() ?? [];

        int vacantBeds = beds.Count(b => b.Status == BedStatus.Vacant);

        return new HostelRoomResponse(
            r.Id,
            r.HostelBlockId,
            r.HostelBlock?.Name ?? "",
            r.RoomNumber,
            r.FloorLevel,
            r.RoomType,
            r.Capacity,
            r.SemesterFeeRate,
            amenities,
            r.Status,
            r.IsActive,
            vacantBeds,
            beds
        );
    }

    private static HostelAllocationResponse MapAllocationToResponse(HostelAllocation a)
    {
        var studentName = $"{a.Student?.FirstName} {a.Student?.LastName}".Trim();
        var matric = a.Student?.StudentNumber ?? "";
        var gender = a.Student?.Gender ?? "";
        var sessionName = a.AcademicSession?.Name ?? "";
        var bedLabel = a.HostelBed?.BedLabel;
        var room = a.HostelBed?.HostelRoom;
        var block = room?.HostelBlock;

        return new HostelAllocationResponse(
            a.Id,
            a.StudentId,
            studentName,
            matric,
            gender,
            a.AcademicSessionId,
            sessionName,
            a.HostelBedId,
            bedLabel,
            room?.Id,
            room?.RoomNumber,
            block?.Id,
            block?.Name,
            a.PreferredBlockId,
            a.PreferredBlock?.Name,
            a.PreferredRoomType,
            a.SpecialNeeds,
            a.Status,
            a.ApplicationDate,
            a.AllocatedAt,
            a.CheckedInAt,
            a.CheckedOutAt,
            a.FeeRecordId,
            room?.SemesterFeeRate,
            a.Notes
        );
    }
}
