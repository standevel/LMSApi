using System.Text.Json.Serialization;

namespace LMS.Api.Data.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TranscriptStatus
{
    Pending = 1,
    Processing = 2,
    Ready = 3,
    Delivered = 4,
    Cancelled = 5
}
