using System;
using System.Net.Http;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;

namespace Expenses.Common
{
    public static class ExtensionMethods
    {
        private const string JsonMediaType = "application/json";
        private static readonly JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        public static string GetUserId(this ClaimsPrincipal user)
        {
            return user.FindFirst(Constants.ClaimTypes.ObjectId)?.Value;
        }

        public static string GetAccountId(this ClaimsPrincipal user)
        {
            return user.FindFirst(Constants.ClaimTypes.AccountId)?.Value;
        }

        public static string GetTenantId(this ClaimsPrincipal user)
        {
            return user.FindFirst(Constants.ClaimTypes.TenantId)?.Value;
        }

        public static string GetLoginHint(this ClaimsPrincipal user)
        {
            return user.FindFirst(Constants.ClaimTypes.PreferredUsername)?.Value;
        }

        public static string GetDomainHint(this ClaimsPrincipal user)
        {
            // This is the well-known Tenant ID for Microsoft Accounts (MSA).
            const string msaTenantId = "9188040d-6c67-4c5b-b112-36a304b66dad";
            var tenantId = user.GetTenantId();
            return string.IsNullOrWhiteSpace(tenantId) ? null : string.Equals(tenantId, msaTenantId, StringComparison.OrdinalIgnoreCase) ? "consumers" : "organizations";
        }

        public static async Task<T> ReadAsAsync<T>(this HttpContent content)
        {
            var contentString = await content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<T>(contentString, jsonSerializerOptions);
        }

        public static Task<HttpResponseMessage> PostAsJsonAsync(this HttpClient client, string requestUri, object value)
        {
            return client.PostAsync(requestUri, new StringContent(JsonSerializer.Serialize(value, value.GetType(), jsonSerializerOptions), null, JsonMediaType));
        }

        public static Task<HttpResponseMessage> PutAsJsonAsync(this HttpClient client, string requestUri, object value)
        {
            return client.PutAsync(requestUri, new StringContent(JsonSerializer.Serialize(value, value.GetType(), jsonSerializerOptions), null, JsonMediaType));
        }
    }
}