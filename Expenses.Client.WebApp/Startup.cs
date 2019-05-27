using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Threading.Tasks;
using Expenses.Client.WebApp.Infrastructure;
using Expenses.Common;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.AzureAD.UI;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Newtonsoft.Json;

namespace Expenses.Client.WebApp
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // Inject an HTTP client for the back-end Web API.
            services.AddHttpClient(Constants.HttpClientNames.ExpensesApi, c =>
            {
                c.BaseAddress = new Uri(Configuration["App:ExpensesApi:BaseUrl"]);
            });

            // Create a token provider based on MSAL.
            var tokenProvider = new MsalTokenProvider(new MsalTokenProviderOptions
            {
                ScopePlaceholderMappings = new Dictionary<string, string>
                {
                    { Constants.Placeholders.ExpensesApiAppIdUri, Configuration["App:ExpensesApi:AppIdUri"] }
                },
                CallbackPath = Configuration["AzureAd:CallbackPath"] ?? string.Empty,
                ClientId = Configuration["AzureAd:ClientId"],
                ClientSecret = Configuration["AzureAd:ClientSecret"],
                TenantId = Configuration["AzureAd:TenantId"]
            });
            services.AddSingleton<MsalTokenProvider>(tokenProvider);

            // Don't map any standard OpenID Connect claims to Microsoft-specific claims.
            // See https://leastprivilege.com/2017/11/15/missing-claims-in-the-asp-net-core-2-openid-connect-handler/.
            JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

            // Add Azure AD authentication using OpenID Connect.
            services.AddAuthentication(AzureADDefaults.AuthenticationScheme)
                .AddAzureAD(options => Configuration.Bind("AzureAd", options));
            services.Configure<OpenIdConnectOptions>(AzureADDefaults.OpenIdScheme, options =>
            {
                // Don't remove any incoming claims.
                options.ClaimActions.Clear();

                // Use the Azure AD v2.0 endpoint.
                options.Authority += "/v2.0";

                // The Azure AD v2.0 endpoint returns the display name in the "preferred_username" claim.
                options.TokenValidationParameters.NameClaimType = Constants.ClaimTypes.PreferredUsername;

                // Azure AD returns the roles in the "roles" claims (if any).
                options.TokenValidationParameters.RoleClaimType = Constants.ClaimTypes.Roles;

                // Trigger a hybrid OpenID Connect + authorization code flow.
                options.ResponseType = OpenIdConnectResponseType.CodeIdToken;

                // Define the API scopes that are requested by default as part of the sign-in so that the user can consent to them up-front.
                var defaultApiScopes = new[] { tokenProvider.GetFullyQualifiedScope(Constants.Placeholders.ExpensesApiScopeIdentityRead) };

                // Request the scopes from the API as part of the authorization code flow.
                foreach (var apiScope in defaultApiScopes)
                {
                    options.Scope.Add(apiScope);
                }

                // Request a refresh token as part of the authorization code flow.
                options.Scope.Add(OpenIdConnectScope.OfflineAccess);

                // Handle events.
                var onMessageReceived = options.Events.OnMessageReceived;
                options.Events.OnMessageReceived = context =>
                {
                    // NOTE: You can inspect every message that comes in from the identity provider here.
                    if (onMessageReceived != null)
                    {
                        onMessageReceived(context);
                    }
                    return Task.CompletedTask;
                };

                var onRedirectToIdentityProvider = options.Events.OnRedirectToIdentityProvider;
                options.Events.OnRedirectToIdentityProvider = context =>
                {
                    // NOTE: You can make any changes to the request URL before the user is redirected to the identity provider here.
                    if (onRedirectToIdentityProvider != null)
                    {
                        onRedirectToIdentityProvider(context);
                    }

                    // Pass through additional parameters if requested.
                    context.ProtocolMessage.LoginHint = context.Properties.GetParameter<string>(OpenIdConnectParameterNames.LoginHint) ?? context.ProtocolMessage.LoginHint;
                    context.ProtocolMessage.DomainHint = context.Properties.GetParameter<string>(OpenIdConnectParameterNames.DomainHint) ?? context.ProtocolMessage.DomainHint;

                    return Task.CompletedTask;
                };

                var onAuthorizationCodeReceived = options.Events.OnAuthorizationCodeReceived;
                options.Events.OnAuthorizationCodeReceived = async context =>
                {
                    if (onAuthorizationCodeReceived != null)
                    {
                        await onAuthorizationCodeReceived(context);
                    }

                    // Use the MSAL token provider to redeem the authorizaation code for an ID token, access token and refresh token.
                    // These aren't used here directly (except the ID token) but they are added to the MSAL cache for later use.
                    var result = await tokenProvider.RedeemAuthorizationCodeAsync(context.HttpContext, context.ProtocolMessage.Code, defaultApiScopes);

                    // Remember the MSAL home account identifier so it can be stored in the claims later on.
                    context.Properties.SetParameter(Constants.ClaimTypes.AccountId, result.Account.HomeAccountId.Identifier);

                    // Signal to the OpenID Connect middleware that the authorization code is already redeemed and it should not be redeemed again.
                    // Pass through the ID token so that it can be validated and used as the identity that has signed in.
                    // Do not pass through the access token as we are taking control over the token acquisition and don't want ASP.NET Core to
                    // cache and reuse the access token itself.
                    context.HandleCodeRedemption(null, result.IdToken);
                };

                var onTokenResponseReceived = options.Events.OnTokenResponseReceived;
                options.Events.OnTokenResponseReceived = async context =>
                {
                    if (onTokenResponseReceived != null)
                    {
                        await onTokenResponseReceived(context);
                    }

                    // The authorization code has been redeemed, and the resulting ID token has been used to construct
                    // the user principal representing the identity that has signed in.
                    // As part of the authorization code redemption, the access token was stored in the MSAL token provider's
                    // cache and it can now be used to call a back-end Web API.
                    // We use it here to retrieve role information for the user, as defined on the back-end Web API, so that
                    // the roles the user has on the back-end can also be used to modify the UI the user will see (e.g. to disable
                    // certain actions the user is not allowed to perform anyway based on their role in the back-end Web API).
                    // NOTE: Technically, we could decode the access token for the back-end Web API and get  the role claims
                    // from there (as they are emitted as part of the token), but that would violate the principle of access
                    // tokens being only intended for the rightful audience.
                    // See http://www.cloudidentity.com/blog/2018/04/20/clients-shouldnt-peek-inside-access-tokens/.
                    var identity = (ClaimsIdentity)context.Principal.Identity;

                    // See if an account identifier was provided by a previous step.
                    var accountId = context.Properties.GetParameter<string>(Constants.ClaimTypes.AccountId);
                    if (accountId != null)
                    {
                        // Add the account identifier claim so it can be used to look up the user's tokens later.
                        identity.AddClaim(new Claim(Constants.ClaimTypes.AccountId, accountId));
                    }

                    try
                    {
                        // Get an access token from the MSAL token provider to call the back-end Web API with permissions to read identity information.
                        var token = await tokenProvider.GetTokenForUserAsync(context.HttpContext, context.Principal, new[] { Constants.Placeholders.ExpensesApiScopeIdentityRead });

                        // Use the access token to request the user's role information as seen by the back-end Web API.
                        var httpClientFactory = context.HttpContext.RequestServices.GetRequiredService<IHttpClientFactory>();
                        var client = httpClientFactory.CreateClient(Constants.HttpClientNames.ExpensesApi);
                        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
                        var response = await client.GetAsync("api/account/roles");
                        response.EnsureSuccessStatusCode();

                        // Add the roles that the user was granted in the Web API to the roles available for this client application.
                        // By adding it to the claims of the identity, they will be serialized into the authentication cookie
                        // and made available in all subsequent calls automatically.
                        var apiRolesValue = await response.Content.ReadAsStringAsync();
                        var apiRoles = JsonConvert.DeserializeObject<string[]>(apiRolesValue);
                        identity.AddClaims(apiRoles.Select(r => new Claim(Constants.ClaimTypes.Roles, r)));
                    }
                    catch (Exception exc)
                    {
                        var loggerFactory = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>();
                        var logger = loggerFactory.CreateLogger<Startup>();
                        logger.LogError(exc, "Could not determine user roles from back-end Web API: " + exc.Message);
                    }
                };

                var onTokenValidated = options.Events.OnTokenValidated;
                options.Events.OnTokenValidated = context =>
                {
                    if (onTokenValidated != null)
                    {
                        onTokenValidated(context);
                    }
                    var identity = (ClaimsIdentity)context.Principal.Identity;
                    //context.Properties.IsPersistent = true; // Optionally ensure the cookie is persistent across browser sessions.
                    return Task.CompletedTask;
                };

                var onRedirectToIdentityProviderForSignOut = options.Events.OnRedirectToIdentityProviderForSignOut;
                options.Events.OnRedirectToIdentityProviderForSignOut = async context =>
                {
                    if (onRedirectToIdentityProviderForSignOut != null)
                    {
                        await onRedirectToIdentityProviderForSignOut(context);
                    }

                    // Remove the user from the MSAL cache.
                    var user = context.HttpContext.User;
                    await tokenProvider.RemoveUserAsync(context.HttpContext, user);

                    // Try to avoid displaying the "pick an account" dialog to the user if we already know who they are.
                    context.ProtocolMessage.LoginHint = user.GetLoginHint();
                    context.ProtocolMessage.DomainHint = user.GetDomainHint();
                };
            });
            services.Configure<CookieAuthenticationOptions>(AzureADDefaults.CookieScheme, options =>
            {
                // Optionally set authentication cookie options here.
            });

            services.AddMvc()
                .SetCompatibilityVersion(CompatibilityVersion.Version_2_2)
                .AddMvcOptions(options =>
                {
                    options.Filters.Add(new MsalUiRequiredExceptionFilterAttribute());
                });
            services.AddRouting(options => { options.LowercaseUrls = true; });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseAuthentication();
            app.UseMvc();
        }
    }
}
