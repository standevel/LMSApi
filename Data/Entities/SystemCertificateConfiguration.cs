using System;

namespace LMS.Api.Data.Entities;

public sealed class SystemCertificateConfiguration
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public bool ChargeForCertificates { get; set; } = true;
    public decimal OfficialCertificateFee { get; set; } = 10000.00m;
    public string SignatoryName { get; set; } = "Prof. Vice Chancellor";
    public string SignatoryPosition { get; set; } = "Vice Chancellor";
    public string? SignatorySignatureBase64 { get; set; } // Scanned signature image of VC
    public string RegistrarName { get; set; } = "Dr. Registrar";
    public string RegistrarPosition { get; set; } = "Registrar";
    public string? RegistrarSignatureBase64 { get; set; } // Scanned signature image of Registrar
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Guid? UpdatedById { get; set; }
}
