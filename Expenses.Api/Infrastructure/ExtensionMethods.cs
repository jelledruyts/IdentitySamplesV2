using System.Security.Claims;

namespace Expenses.Api.Infrastructure
{
    public static class ExtensionMethods
    {
        public static string GetUserId(this ClaimsPrincipal user)
        {
            return user.FindFirst(Constants.ClaimTypes.ObjectId).Value;
        }
    }
}