using System.Text.Json.Serialization;

namespace LMS.Api.Data.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HostelGenderType
{
    Male = 0,
    Female = 1,
    Coed = 2
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RoomStatus
{
    Available = 0,
    Occupied = 1,
    Maintenance = 2,
    Reserved = 3
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BedStatus
{
    Vacant = 0,
    Occupied = 1,
    Maintenance = 2
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AllocationStatus
{
    Pending = 0,
    Approved = 1,
    Allocated = 2,
    CheckedIn = 3,
    CheckedOut = 4,
    Cancelled = 5,
    Rejected = 6
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MaintenancePriority
{
    Low = 0,
    Medium = 1,
    High = 2,
    Emergency = 3
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MaintenanceStatus
{
    Open = 0,
    InProgress = 1,
    Resolved = 2,
    Closed = 3
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ExeatStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
    CheckedOut = 3,
    Returned = 4,
    Overdue = 5
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HostelDeviceType
{
    Laptop = 0,
    Desktop = 1,
    Television = 2,
    Refrigerator = 3,
    Microwave = 4,
    GamingConsole = 5,
    Tablet = 6,
    Phone = 7,
    SoundSystem = 8,
    Other = 9
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HostelDeviceStatus
{
    PendingVerification = 0,
    Verified = 1,
    Rejected = 2,
    Decommissioned = 3
}

