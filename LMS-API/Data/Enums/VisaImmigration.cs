namespace LMS.Api.Data.Enums;

public enum VisaStatus
{
    None = 0,
    Required = 1,
    Applied = 2,
    Approved = 3,
    Rejected = 4,
    Waived = 5
}

public enum VisaType
{
    None = 0,
    Student = 1,
    Exchange = 2,
    Temporary = 3,
    Dependent = 4
}

public enum Region
{
    Africa = 0,
    MiddleEast = 1,
    Asia = 2,
    Americas = 3,
    Europe = 4,
    Oceania = 5,
    Other = 6
}

public enum ImmigrationStatus
{
    NotApplicable = 0,
    Pending = 1,
    Approved = 2,
    Rejected = 3,
    Expired = 4
}
