using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using Expenses.Api.Infrastructure;
using Expenses.Common;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.AzureAD.UI;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
                // Optionally, use the Azure AD v2.0 endpoint also on the back-end API for retrieving v2.0 OIDC metadata.
                // However, this changes the expected "issuer" claim (which is different between v1 and v2 endpoints and tokens)
                // and potentially other claims that the API may be expecting. In order for this to work, the API must
                // explicitly opt-in to being issued v2 tokens by setting the "accessTokenAcceptedVersion" attribute
                // in the API app registration manifest to "2". This cannot be set via PowerShell today, so only enable the
                // code below to switch to the v2.0 endpoint if you have manually updated the app manifest as well.
                // See https://docs.microsoft.com/en-us/azure/active-directory/develop/reference-app-manifest#accesstokenacceptedversion-attribute
                // and https://docs.microsoft.com/en-us/azure/active-directory/develop/access-tokens for more information.

                // options.Authority += "/v2.0";

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
                    // Require the "Expenses.Read" and/or "Expenses.ReadWrite" scope.
                    b.RequireClaim(Constants.ClaimTypes.Scope, Constants.Scopes.ExpensesRead, Constants.Scopes.ExpensesReadWrite);
                });
                // Define the "ReadAllExpenses" authorization policy.
                options.AddPolicy(Constants.AuthorizationPolicies.ReadAllExpenses, b =>
                {
                    b.RequireAssertion(context =>
                        // For applications, require the "Expenses.ReadWrite.All" role.
                        context.User.IsInRole(Constants.Roles.ExpensesReadWriteAll)
                        // For users, require the "ExpenseApprover" role and the "Expenses.Read.All" scope.
                        || (context.User.IsInRole(Constants.Roles.ExpenseApprover) && context.User.HasClaim(Constants.ClaimTypes.Scope, Constants.Scopes.ExpensesReadAll))
                    );
                });
                // Define the "ReadWriteMyExpenses" authorization policy.
                options.AddPolicy(Constants.AuthorizationPolicies.ReadWriteMyExpenses, b =>
                {
                    // Require the "Expenses.ReadWrite" scope.
                    b.RequireClaim(Constants.ClaimTypes.Scope, Constants.Scopes.ExpensesReadWrite);

                    // Require the "ExpenseSubmitter" role.
                    b.RequireRole(Constants.Roles.ExpenseSubmitter);
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

            services.AddControllers()
                .AddMvcOptions(options =>
                {
                    // Enforce a "baseline" policy at the minimum for all requests.
                    var baselinePolicy = new AuthorizationPolicyBuilder()
                        // An authenticated user (i.e. an incoming JWT bearer token) is always required.
                        .RequireAuthenticatedUser()
                        // A "scope" or "role" claim is also required, if not any application could simply request
                        // a valid access token to call into this API without being truly authorized.
                        .RequireAssertion(context => context.User.Claims.Any(c => c.Type == Constants.ClaimTypes.Scope || c.Type == Constants.ClaimTypes.Roles))
                        .Build();
                    options.Filters.Add(new AuthorizeFilter(baselinePolicy));
                });
            services.AddRouting(options => { options.LowercaseUrls = true; });

            // Inject other services.
            services.AddSingleton<IClaimsTransformation, AzureAdScopeClaimTransformation>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseCors();

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
