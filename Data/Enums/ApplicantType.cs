namespace LMS.Api.Data.Enums;

public enum ApplicantType
{
    UTME = 1,        // Regular JAMB admission
    DirectEntry = 2, // A-Level, IB, etc. for year 2+
    Transfer = 3,    // Credit transfer from other universities
    International = 4 // Non-Nigerian applicants
}
