using System;
using System.IdentityModel.Tokens.Jwt;
using Expenses.Api.Infrastructure;
using Expenses.Common;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.AzureAD.UI;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
            services.AddHttpClient();

            // Don't map any standard OpenID Connect claims to Microsoft-specific claims.
            // See https://leastprivilege.com/2017/11/15/missing-claims-in-the-asp-net-core-2-openid-connect-handler/
            JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

            // Use JWT Bearer tokens for authentication/authorization.
            services.AddAuthentication(AzureADDefaults.JwtBearerAuthenticationScheme)
                .AddAzureADBearer(options => Configuration.Bind("AzureAd", options));
            services.Configure<JwtBearerOptions>(AzureADDefaults.JwtBearerAuthenticationScheme, options =>
            {
                // The Azure AD v2.0 endpoint returns the display name in the "name" claim for access tokens.
                options.TokenValidationParameters.NameClaimType = Constants.ClaimTypes.Name;

                // Azure AD returns the roles in the "roles" claims (if any).
                options.TokenValidationParameters.RoleClaimType = Constants.ClaimTypes.Roles;

                // Set the valid audiences for the incoming JWT bearer token.
                options.TokenValidationParameters.ValidAudiences = new string[]
                {
                     Configuration["AzureAd:ClientId"],
                     Configuration["App:AppIdUri"] // Azure AD v2.0 issues access tokens with the App ID URI as the audience
                };

                // Store the incoming tokens for later use (i.e. for the On-Behalf-Of flow).
                options.SaveToken = true;
            });

            // Define authorization policies that can be applied to API's.
            services.AddAuthorization(options =>
            {
                // Define the "ReadMyIdentity" authorization policy.
                options.AddPolicy(Constants.AuthorizationPolicies.ReadMyIdentity, b =>
                {
                    // Require the "Identity.Read" scope.
                    b.RequireClaim(Constants.ClaimTypes.Scope, Constants.Scopes.IdentityRead);
                });
                // Define the "ReadMyExpenses" authorization policy.
                options.AddPolicy(Constants.AuthorizationPolicies.ReadMyExpenses, b =>
                {
                    // Require the "Expense.Read" and/or "Expense.ReadWrite" scope.
                    b.RequireClaim(Constants.ClaimTypes.Scope, Constants.Scopes.ExpensesRead, Constants.Scopes.ExpensesReadWrite);
                });
                // Define the "ReadAllExpenses" authorization policy.
                options.AddPolicy(Constants.AuthorizationPolicies.ReadAllExpenses, b =>
                {
                    // Require the "Expense.Read.All" scope.
                    b.RequireClaim(Constants.ClaimTypes.Scope, Constants.Scopes.ExpensesReadAll);

                    // Require the "ExpenseApprover" role.
                    b.RequireRole(Constants.Roles.ExpenseApprover);
                });
                // Define the "ReadWriteMyExpenses" authorization policy.
                options.AddPolicy(Constants.AuthorizationPolicies.ReadWriteMyExpenses, b =>
                {
                    // Require the "Expense.ReadWrite" scope.
                    b.RequireClaim(Constants.ClaimTypes.Scope, Constants.Scopes.ExpensesReadWrite);

                    // Require the "ExpenseSubmitter" role.
                    b.RequireRole(Constants.Roles.ExpenseSubmitter);
                });
                // Define the "ApproveExpenses" authorization policy.
                options.AddPolicy(Constants.AuthorizationPolicies.ApproveExpenses, b =>
                {
                    // Require the "ExpenseApprover" role.
                    b.RequireRole(Constants.Roles.ExpenseApprover);
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
