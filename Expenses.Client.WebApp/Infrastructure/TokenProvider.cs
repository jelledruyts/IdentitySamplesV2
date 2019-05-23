using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Expenses.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Identity.Client;

namespace Expenses.Client.WebApp.Infrastructure
{
    public class TokenProvider
    {
        private readonly TokenProviderOptions options;

        public TokenProvider(TokenProviderOptions options)
        {
            this.options = options;
        }

        public string GetApiScope(string scopeName)
        {
            return $"{this.options.ExpensesApiAppIdUri}/{scopeName}";
        }

        public async Task<AuthenticationResult> RedeemAuthorizationCodeAsync(HttpContext httpContext, string authorizationCode, IEnumerable<string> scopes)
        {
            var confidentialClientApplication = GetConfidentialClientApplication(httpContext, httpContext.User);
            return await confidentialClientApplication.AcquireTokenByAuthorizationCode(scopes, authorizationCode).ExecuteAsync();
        }

        public async Task<AuthenticationResult> GetTokenForUserAsync(HttpContext httpContext, ClaimsPrincipal user, IEnumerable<string> scopes)
        {
            var confidentialClientApplication = GetConfidentialClientApplication(httpContext, user);
            var userAccount = await confidentialClientApplication.GetAccountAsync(user.GetAccountId());
            return await confidentialClientApplication.AcquireTokenSilent(scopes, userAccount).ExecuteAsync();
        }

        private IConfidentialClientApplication GetConfidentialClientApplication(HttpContext httpContext, ClaimsPrincipal user)
        {
            var redirectUri = UriHelper.BuildAbsolute(httpContext.Request.Scheme, httpContext.Request.Host, httpContext.Request.PathBase, this.options.CallbackPath);
            var confidentialClientApplication = ConfidentialClientApplicationBuilder.CreateWithApplicationOptions(new ConfidentialClientApplicationOptions
            {
                ClientId = this.options.ClientId,
                ClientSecret = this.options.ClientSecret,
                TenantId = this.options.TenantId,
                RedirectUri = redirectUri
            }).Build();
            new AppTokenCacheWrapper(confidentialClientApplication.AppTokenCache);
            new UserTokenCacheWrapper(confidentialClientApplication.UserTokenCache, user);
            return confidentialClientApplication;
        }

        private class AppTokenCacheWrapper
        {
            private static byte[] appTokenCache;

            public AppTokenCacheWrapper(ITokenCache appTokenCache)
            {
                appTokenCache.SetBeforeAccess(AppTokenCacheBeforeAccessNotification);
                appTokenCache.SetBeforeWrite(AppTokenCacheBeforeWriteNotification);
                appTokenCache.SetAfterAccess(AppTokenCacheAfterAccessNotification);
            }

            private void AppTokenCacheBeforeAccessNotification(TokenCacheNotificationArgs args)
            {
                args.TokenCache.DeserializeMsalV3(appTokenCache);
            }

            private void AppTokenCacheBeforeWriteNotification(TokenCacheNotificationArgs args)
            {
            }

            private void AppTokenCacheAfterAccessNotification(TokenCacheNotificationArgs args)
            {
                if (args.HasStateChanged)
                {
                    appTokenCache = args.TokenCache.SerializeMsalV3();
                }
            }
        }

        private class UserTokenCacheWrapper
        {
            private string userKey;
            private static readonly IDictionary<string, byte[]> userTokenCache = new Dictionary<string, byte[]>();

            public UserTokenCacheWrapper(ITokenCache userTokenCache, ClaimsPrincipal user)
            {
                this.userKey = user.GetAccountId();
                userTokenCache.SetBeforeAccess(UserTokenCacheBeforeAccessNotification);
                userTokenCache.SetBeforeWrite(UserTokenCacheBeforeWriteNotification);
                userTokenCache.SetAfterAccess(UserTokenCacheAfterAccessNotification);
            }

            private void UserTokenCacheBeforeAccessNotification(TokenCacheNotificationArgs args)
            {
                if (userTokenCache.ContainsKey(GetUserKey(args)))
                {
                    args.TokenCache.DeserializeMsalV3(userTokenCache[GetUserKey(args)]);
                }
            }

            private void UserTokenCacheBeforeWriteNotification(TokenCacheNotificationArgs args)
            {
            }

            private void UserTokenCacheAfterAccessNotification(TokenCacheNotificationArgs args)
            {
                if (args.HasStateChanged)
                {
                    userTokenCache[GetUserKey(args)] = args.TokenCache.SerializeMsalV3();
                }
            }

            private string GetUserKey(TokenCacheNotificationArgs args)
            {
                if (this.userKey == null)
                {
                    var msalAccountId = args.Account?.HomeAccountId?.Identifier;
                    if (!string.IsNullOrEmpty(msalAccountId))
                    {
                        this.userKey = msalAccountId;
                    }
                }
                return this.userKey;
            }
        }
    }
}