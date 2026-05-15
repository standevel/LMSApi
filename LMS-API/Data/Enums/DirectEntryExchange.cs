namespace LMS.Api.Data.Enums;

public enum DirectEntryQualification
{
    None = 0,
    ALevel = 1,
    IJMB = 2,
    BTEC = 3,
    HND = 4,
    ND = 5,
    Diploma = 6,
    IB = 7,
    CambridgeAdvanced = 8,
    AdvancedAdvanced = 9,
    Other = 99
}

public enum ExchangeProgramType
{
    None = 0,
    Outgoing = 1,  // Student going to partner institution
    Incoming = 2   // Student coming from partner institution
}

public enum ExchangeStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
    Completed = 3,
    Expired = 4
}

public enum AcademicStanding
{
    Unknown = 0,
    GoodStanding = 1,
    Probation = 2,
    Suspended = 3
}
