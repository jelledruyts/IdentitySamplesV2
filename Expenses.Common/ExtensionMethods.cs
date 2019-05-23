using System.Security.Claims;

namespace Expenses.Common
{
    public static class ExtensionMethods
    {
        public static string GetUserId(this ClaimsPrincipal user)
        {
            return user.FindFirst(Constants.ClaimTypes.ObjectId)?.Value;
        }

        public static string GetAccountId(this ClaimsPrincipal user)
        {
            return user.FindFirst(Constants.ClaimTypes.AccountId)?.Value;
        }
    }
}