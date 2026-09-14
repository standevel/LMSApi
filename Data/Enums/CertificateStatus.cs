using System.Text.Json.Serialization;

namespace LMS.Api.Data.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CertificateStatus
{
    Pending = 1,
    Processing = 2,
    Ready = 3,
    Rejected = 4
}
