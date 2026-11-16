namespace PettyCash.Domain.Enums;

public enum RequestStatus
{
    Draft = 0,
    Submitted = 1,
    Approved = 2,
    Rejected = 3,
    Cancelled = 4,
    PendingFinanceApproval = 5,
    PendingDisbursement = 6,
    Disbursed = 7
}
