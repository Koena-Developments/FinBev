namespace PettyCash.Domain;

public static class PettyCashPolicy
{
    public const decimal MaxRequestAmount = 2000m;
    public const decimal FloatCapacity = 10000m;
    public const string FinanceDepartmentName = "Finance";

    /// <summary>An invoice or receipt must be uploaded within this many days of the cash being paid out.</summary>
    public const int ReceiptDueDays = 3;
}
