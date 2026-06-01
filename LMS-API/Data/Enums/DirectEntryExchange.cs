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
    Outgoing = 1,
    Incoming = 2
}

public enum ExchangeStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
    Completed = 3,
    Expired = 4
}

/// <summary>
/// Grade values for direct entry qualifications, covering A-Level, IJMB, BTEC,
/// HND/ND, IB, Cambridge Advanced, and other common direct entry systems.
/// </summary>
public enum DirectEntryGrade
{
    None = 0,

    // A-Level grades
    AStar = 1,
    A = 2,
    B = 3,
    C = 4,
    D = 5,
    E = 6,
    U = 7,

    // HND/ND grades
    FirstClass = 8,
    SecondClassUpper = 9,
    SecondClassLower = 10,
    ThirdClass = 11,
    Pass = 12,

    // BTEC grades
    DistinctionStar = 13,
    Distinction = 14,
    Merit = 15,
    PassBTEC = 16,

    // IB grades (numeric 1-7)
    IB7 = 17,
    IB6 = 18,
    IB5 = 19,
    IB4 = 20,
    IB3 = 21,
    IB2 = 22,
    IB1 = 23,

    // Cambridge Advanced / Other
    APlus = 24,
    BPlus = 25,
    CPlus = 26,
    DPlus = 27,
    EPlus = 28,

    // IJMB specific
    A1 = 29,
    B2 = 30,
    B3 = 31,
    C4 = 32,
    C5 = 33,
    C6 = 34,
    D7 = 35,
    E8 = 36,
    F = 37,

    Other = 99
}
