namespace Expenses.Client.WebApp
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
            public const string ExpenseSubmitter = "ExpenseSubmitter"; // This role can be granted to users only.
            public const string ExpenseApprover = "ExpenseApprover"; // This role can be granted to users only.
        }

        public static class ClaimTypes
        {
            public const string ObjectId = "oid";
            public const string Roles = "roles";
            public const string AccessToken = "access_token"; // TODO: Remove when using MSAL
            public const string RefreshToken = "refresh_token"; // TODO: Remove when using MSAL
        }

        public static class HttpClientNames
        {
            public const string ExpensesApi = nameof(ExpensesApi);
        }
    }
}