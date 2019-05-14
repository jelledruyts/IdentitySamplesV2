using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.AzureAD.UI;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
                options.TokenValidationParameters.NameClaimType = "preferred_username";
                // Azure AD returns the roles in the "roles" claims (if any).
                options.TokenValidationParameters.RoleClaimType = Constants.ClaimTypes.Roles;

                // Trigger a hybrid OpenID Connect + authorization code flow.
                options.ResponseType = OpenIdConnectResponseType.CodeIdToken;

                // Request a refresh token as part of the authorization code flow.
                options.Scope.Add(OpenIdConnectScope.OfflineAccess);

                // Request that this app can act on behalf of the user (delegated permissions) for certain scopes.
                var expensesApiAppIdUri = Configuration.GetValue<string>("App:ExpensesApi:AppIdUri");
                options.Scope.Add($"{expensesApiAppIdUri}/{Constants.Scopes.ExpensesRead}");
                options.Scope.Add($"{expensesApiAppIdUri}/{Constants.Scopes.ExpensesReadWrite}");

                // Handle events.
                var onMessageReceived = options.Events.OnMessageReceived;
                options.Events.OnMessageReceived = context =>
                {
                    if (onMessageReceived != null)
                    {
                        onMessageReceived(context);
                    }
                    // NOTE: You can inspect every message that comes in from the identity provider here.
                    return Task.CompletedTask;
                };

                var onRedirectToIdentityProvider = options.Events.OnRedirectToIdentityProvider;
                options.Events.OnRedirectToIdentityProvider = context =>
                {
                    if (onRedirectToIdentityProvider != null)
                    {
                        onRedirectToIdentityProvider(context);
                    }
                    return Task.CompletedTask;
                };

                var onAuthorizationCodeReceived = options.Events.OnAuthorizationCodeReceived;
                options.Events.OnAuthorizationCodeReceived = context =>
                {
                    if (onAuthorizationCodeReceived != null)
                    {
                        onAuthorizationCodeReceived(context);
                    }
                    return Task.CompletedTask;
                };

                var onTokenResponseReceived = options.Events.OnTokenResponseReceived;
                options.Events.OnTokenResponseReceived = async context =>
                {
                    if (onTokenResponseReceived != null)
                    {
                        await onTokenResponseReceived(context);
                    }
                    // Normally, the access and refresh tokens that resulted from the authorization code flow would be
                    // stored in a cache like ADAL/MSAL's user cache.
                    // To simplify here, we're adding them as extra claims in the user's claims identity
                    // (which is ultimately encrypted and serialized into the authentication cookie).
                    var identity = (ClaimsIdentity)context.Principal.Identity;
                    identity.AddClaim(new Claim(Constants.ClaimTypes.AccessToken, context.TokenEndpointResponse.AccessToken));
                    identity.AddClaim(new Claim(Constants.ClaimTypes.RefreshToken, context.TokenEndpointResponse.RefreshToken));

                    try
                    {
                        // Request role information as seen by the back-end Web API.
                        var httpClientFactory = context.HttpContext.RequestServices.GetRequiredService<IHttpClientFactory>();
                        var client = httpClientFactory.CreateClient(Constants.HttpClientNames.ExpensesApi);
                        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", context.TokenEndpointResponse.AccessToken);
                        var response = await client.GetAsync("api/account/roles");
                        response.EnsureSuccessStatusCode();

                        // Add the roles the user has in the Web API to the roles for this application.
                        // NOTE: Technically we could decode the access token and get the role claims from there,
                        // but that would violate the principle of access tokens being only intended for the rightful audience.
                        // See http://www.cloudidentity.com/blog/2018/04/20/clients-shouldnt-peek-inside-access-tokens/.
                        var apiRolesValue = await response.Content.ReadAsStringAsync();
                        var apiRoles = JsonConvert.DeserializeObject<string[]>(apiRolesValue);
                        identity.AddClaims(apiRoles.Select(r => new Claim(Constants.ClaimTypes.Roles, r)));
                    }
                    catch (Exception)
                    {
                        // TODO: Log exception
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
            });
            services.Configure<CookieAuthenticationOptions>(AzureADDefaults.CookieScheme, options =>
            {
                // Optionally set authentication cookie options here.
            });

            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_2);
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
