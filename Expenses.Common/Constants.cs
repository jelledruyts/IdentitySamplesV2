namespace Expenses.Common
{
    public static class Constants
    {
        public static class Scopes
        {
            public const string IdentityRead = "Identity.Read"; // This scope can be consented to by any user, as it allows access to their own data.
            public const string ExpensesRead = "Expenses.Read"; // This scope can be consented to by any user, as it allows access to their own data.
            public const string ExpensesReadWrite = "Expenses.ReadWrite"; // This scope can be consented to by any user, as it allows access to their own data.
            public const string ExpensesReadAll = "Expenses.Read.All"; // This scope can only be consented to by admins, as it allows access to all user's data.
            public const string Default = ".default"; // This scope is used to request all statically declared scopes, including those of downstream API's that have the "knownClientApplications" set to the current App ID (see https://docs.microsoft.com/en-us/azure/active-directory/develop/v2-oauth2-on-behalf-of-flow#gaining-consent-for-the-middle-tier-application).
        }

        public static class Roles
        {
            public const string ExpenseSubmitter = "ExpenseSubmitter"; // This role can be granted to users only.
            public const string ExpenseApprover = "ExpenseApprover"; // This role can be granted to users only.
            public const string ExpensesReadWriteAll = "Expenses.ReadWrite.All"; // This role can be granted to applications only (as an application permission).
        }

        public static class ClaimTypes
        {
            public const string PreferredUsername = "preferred_username";
            public const string ObjectId = "oid";
            public const string TenantId = "tid";
            public const string Name = "name";
            public const string Roles = "roles";
            public const string Scope = "scp";
            public const string AccountId = "aid";
        }

        public static class HttpClientNames
        {
            public const string ExpensesApi = nameof(ExpensesApi);
        }

        public static class Placeholders
        {
            public const string ExpensesApiAppIdUri = "%ExpensesApi%";
            public const string ExpensesApiScopeIdentityRead = ExpensesApiAppIdUri + "/" + Scopes.IdentityRead;
            public const string ExpensesApiScopeExpensesRead = ExpensesApiAppIdUri + "/" + Scopes.ExpensesRead;
            public const string ExpensesApiScopeExpensesReadWrite = ExpensesApiAppIdUri + "/" + Scopes.ExpensesReadWrite;
            public const string ExpensesApiScopeExpensesReadAll = ExpensesApiAppIdUri + "/" + Scopes.ExpensesReadAll;
            public const string ExpensesApiScopeDefault = ExpensesApiAppIdUri + "/" + Scopes.Default;
        }

        public static class AuthorizationPolicies
        {
            public const string ReadMyIdentity = nameof(ReadMyIdentity);
            public const string ReadMyExpenses = nameof(ReadMyExpenses);
            public const string ReadAllExpenses = nameof(ReadAllExpenses);
            public const string ReadWriteMyExpenses = nameof(ReadWriteMyExpenses);
            public const string ApproveExpenses = nameof(ApproveExpenses);
        }
    }
}