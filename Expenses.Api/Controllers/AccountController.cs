using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Expenses.Common;
using Expenses.Common.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Identity.Client;

namespace Expenses.Api.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class AccountController : ControllerBase
    {
        private readonly IConfiguration configuration;
        private readonly IHttpClientFactory httpClientFactory;

        public AccountController(IConfiguration configuration, IHttpClientFactory httpClientFactory)
        {
            this.configuration = configuration;
            this.httpClientFactory = httpClientFactory;
        }

        [Authorize(Constants.AuthorizationPolicies.ReadMyIdentity)]
        [HttpGet]
        public async Task<ActionResult<IdentityInfo>> Identity()
        {
            // Get identity information from related applications (i.e. the Microsoft Graph).
            var relatedApplicationIdentities = new List<IdentityInfo>();
            try
            {
                // Call the Microsoft Graph on behalf of the end user to retrieve additional information about the user.
                // Retrieve the original access token that was sent as the incoming request.
                // NOTE: this requires "SaveToken" to be set to "true", see Startup.cs.
                var originalToken = await this.HttpContext.GetTokenAsync("access_token");

                if (!string.IsNullOrWhiteSpace(originalToken))
                {
                    var options = new ConfidentialClientApplicationOptions();
                    this.configuration.Bind("AzureAd", options);
                    var confidentialClientApplication = ConfidentialClientApplicationBuilder.CreateWithApplicationOptions(options).Build();

                    // Use the OAuth 2.0 On-Behalf-Of flow to get an access token for the Microsoft Graph.
                    var onBehalfOfToken = await confidentialClientApplication.AcquireTokenOnBehalfOf(new[] { "https://graph.microsoft.com/User.Read" }, new UserAssertion(originalToken)).ExecuteAsync();

                    // Call the "me" endpoint to get user details as seen by the Microsoft Graph.
                    var client = this.httpClientFactory.CreateClient();
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", onBehalfOfToken.AccessToken);
                    var response = await client.GetAsync("https://graph.microsoft.com/v1.0/me");

                    // Deserialize and construct an identity from the Microsoft Graph response.
                    var userInfoValue = await response.Content.ReadAsStringAsync();
                    var userProperties = JsonDocument.Parse(userInfoValue).RootElement.EnumerateObject().Where(p => !p.Name.StartsWith('@')).Select(p => new ClaimInfo { Type = p.Name, Value = p.Value.ToString() }).ToArray();
                    relatedApplicationIdentities.Add(new IdentityInfo
                    {
                        Source = "User API",
                        Application = "Microsoft Graph",
                        IsAuthenticated = true,
                        Name = userProperties.FirstOrDefault(p => string.Equals(p.Type, "displayName", StringComparison.OrdinalIgnoreCase))?.Value,
                        AuthenticationType = "OAuth 2.0 On-Behalf-Of Flow",
                        Claims = userProperties
                    });
                }
            }
            catch (Exception exc)
            {
                relatedApplicationIdentities.Add(IdentityInfo.FromException(exc, "Expense API"));
            }

            // Return identity information as seen from this application, including related applications.
            return IdentityInfo.FromPrincipal(this.User, "Access Token", "Expense API", relatedApplicationIdentities);
        }

        [Authorize(Constants.AuthorizationPolicies.ReadMyIdentity)]
        [HttpGet]
        public ActionResult<IEnumerable<string>> Roles()
        {
            var roleClaimType = ((ClaimsIdentity)this.User.Identity).RoleClaimType;
            return this.User.FindAll(roleClaimType).Select(c => c.Value).ToList();
        }
    }
}