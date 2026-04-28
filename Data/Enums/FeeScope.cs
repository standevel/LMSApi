using System.Text.Json.Serialization;

namespace LMS.Api.Data.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FeeScope
{
    University = 0,
    Faculty = 1,
    Program = 2,
    Student = 3
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LateFeeType
{
    Fixed = 0,
    Percentage = 1
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FeeRecordStatus
{
    Outstanding = 0,
    PartiallyPaid = 1,
    Paid = 2,
    Waived = 3
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PaymentMethod
{
    Manual = 0,
    Paystack = 1,
    Hydrogen = 2
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PaymentStatus
{
    Pending = 0,
    Confirmed = 1,
    Rejected = 2,
    Failed = 3
}
