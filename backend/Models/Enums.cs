namespace Veriqo.Api.Models;

public static class Roles
{
    public const string Customer = "Customer";
    public const string Admin = "Admin";
}

public enum ApplicationStatus
{
    Draft,
    Submitted,
    UnderReview,
    ActionRequired,
    Approved,
    Rejected
}

public enum DocumentType
{
    GovernmentId,
    AddressProof,
    SalarySlip,
    BankStatement,
    EmploymentLetter
}

public enum DocumentStatus
{
    Pending,
    Verified,
    Rejected
}

public enum RiskLevel
{
    Low,
    Medium,
    High
}
