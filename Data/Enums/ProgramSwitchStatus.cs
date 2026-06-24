namespace LMS.Api.Data.Enums;

public enum ProgramSwitchStatus
{
    /// <summary>Request submitted but JAMB document not yet uploaded.</summary>
    Draft = 1,

    /// <summary>JAMB document uploaded; awaiting Head of Department review.</summary>
    PendingHoDReview = 2,

    /// <summary>HoD approved; awaiting Dean review.</summary>
    PendingDeanReview = 3,

    /// <summary>Dean approved; awaiting Admin/Registrar to complete the switch.</summary>
    PendingAdminAction = 4,

    /// <summary>Program switch fully completed by Admin.</summary>
    Completed = 5,

    /// <summary>Rejected by Head of Department.</summary>
    RejectedByHoD = 6,

    /// <summary>Rejected by Dean.</summary>
    RejectedByDean = 7,

    /// <summary>Rejected by Admin/Registrar.</summary>
    RejectedByAdmin = 8
}
