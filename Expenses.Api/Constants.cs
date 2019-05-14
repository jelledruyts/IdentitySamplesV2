namespace Expenses.Api
{
    public static class Constants
    {
        public static class Scopes
        {
            public const string ExpensesRead = "Expenses.Read"; // This scope can be consented to by any user, as it allows access to their own data.
            public const string ExpensesReadWrite = "Expenses.ReadWrite"; // This scope can be consented to by any user, as it allows access to their own data.
            public const string ExpensesReadAll = "Expenses.Read.All"; // This scope can only be consented to by admins, as it allows access to all user's data.
        }

        public static class Roles
        {
            public const string ExpenseSubmitter = "Expense Submitter"; // This role can be granted to users only.
            public const string ExpenseApprover = "Expense Approver"; // This role can be granted to users only.
            public const string ExpenseReadWriteAll = "Expense.ReadWrite.All"; // This role can be granted to applications only (as an application permission).
        }

        public static class ClaimTypes
        {
            public const string ObjectId = "oid";
            public const string Roles = "roles";
            public const string Scope = "scp";
        }

        public static class AuthorizationPolicies
        {
            public const string ReadMyExpenses = nameof(ReadMyExpenses);
            public const string ReadWriteMyExpenses = nameof(ReadWriteMyExpenses);
        }
    }
}