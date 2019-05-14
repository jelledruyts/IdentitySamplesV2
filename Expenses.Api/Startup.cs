using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Threading.Tasks;
using Expenses.Api.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.AzureAD.UI;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Expenses.Api
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
            // Don't map any standard OpenID Connect claims to Microsoft-specific claims.
            // See https://leastprivilege.com/2017/11/15/missing-claims-in-the-asp-net-core-2-openid-connect-handler/
            JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

            // Use JWT Bearer tokens for authentication/authorization.
            services.AddAuthentication(AzureADDefaults.JwtBearerAuthenticationScheme)
                .AddAzureADBearer(options => Configuration.Bind("AzureAd", options));
            services.Configure<JwtBearerOptions>(AzureADDefaults.JwtBearerAuthenticationScheme, options =>
            {
                // Azure AD returns the roles in the "roles" claims (if any).
                options.TokenValidationParameters.RoleClaimType = Constants.ClaimTypes.Roles;

                // Set the valid audiences for the incoming JWT bearer token.
                options.TokenValidationParameters.ValidAudiences = new string[]
                {
                     Configuration.GetValue<string>("AzureAd:ClientId"),
                     Configuration.GetValue<string>("App:AppIdUri") // Azure AD v2.0 issues access tokens with the App ID URI as the audience
                };
            });

            // Define authorization policies that can be applied to API's.
            services.AddAuthorization(options =>
            {
                options.AddPolicy(Constants.AuthorizationPolicies.ReadMyExpenses, b =>
                {
                    // Require the "Expense.Read" and/or "Expense.ReadWrite" scope.
                    b.RequireClaim(Constants.ClaimTypes.Scope, Constants.Scopes.ExpensesRead, Constants.Scopes.ExpensesReadWrite);
                });
                options.AddPolicy(Constants.AuthorizationPolicies.ReadWriteMyExpenses, b =>
                {
                    // Require the "Expense.ReadWrite" scope.
                    b.RequireClaim(Constants.ClaimTypes.Scope, Constants.Scopes.ExpensesReadWrite);
                });
            });

            // Allow CORS for any origin for this sample API.
            services.AddCors(options =>
            {
                options.AddDefaultPolicy(builder =>
                {
                    builder.AllowAnyOrigin();
                    builder.AllowAnyHeader();
                });
            });

            services.AddMvc()
                .SetCompatibilityVersion(CompatibilityVersion.Version_2_2)
                .AddMvcOptions(options =>
                {
                    // Enforce a "baseline" policy at the minimum for all requests.
                    var baselinePolicy = new AuthorizationPolicyBuilder()
                        // An authenticated user (i.e. an incoming JWT bearer token) is always required.
                        .RequireAuthenticatedUser()
                        // A "scope" claim is also required, if not any application could simply request
                        // a valid access token to call into this API without being authorized.
                        .RequireClaim(Constants.ClaimTypes.Scope)
                        .Build();
                    options.Filters.Add(new AuthorizeFilter(baselinePolicy));
                });
            services.AddRouting(options => { options.LowercaseUrls = true; });

            // Inject other services.
            services.AddSingleton<IClaimsTransformation, AzureAdScopeClaimTransformation>();
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
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseCors();
            app.UseHttpsRedirection();
            app.UseAuthentication();
            app.UseMvc();
        }
    }
}
