using System;

namespace LMS.Api.Data.Entities;

public sealed class LetterTemplate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string TemplateType { get; set; } = string.Empty; // e.g., Undergraduate, Postgraduate
    
    // Branding
    public string HeaderTitle { get; set; } = "WIGWE UNIVERSITY";
    public string HeaderSubtitle { get; set; } = "Office of Academic Admissions";
    public string HeaderContact { get; set; } = "Rivers State, Nigeria • www.wigweuniversity.edu.ng";
    public string HeaderDate { get; set; } = string.Empty;
    public string LogoBase64 { get; set; } = string.Empty;
    public string SignatureBase64 { get; set; } = string.Empty;
    
    // Content Sections (Stored as JSON)
    public string SectionsJson { get; set; } = "[]";
    
    public bool IsDefault { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
