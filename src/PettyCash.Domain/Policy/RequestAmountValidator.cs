namespace PettyCash.Domain.Policy;

public static class RequestAmountValidator
{
    public static bool IsValid(decimal amount, out string? error)
    {
        if (amount <= 0)
        {
            error = "Amount must be greater than zero.";
            return false;
        }

        if (amount > PettyCashPolicy.MaxRequestAmount)
        {
            error = $"Amount cannot exceed R{PettyCashPolicy.MaxRequestAmount:N2}.";
            return false;
        }

        error = null;
        return true;
    }
}
