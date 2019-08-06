namespace Expenses.Common.Models
{
    public enum ExpenseStatus
    {
        // The expense was submitted for approval.
        Submitted,
        // The expense was approved for payout.
        Approved,
        // The expense was paid out.
        Paid
    }
}