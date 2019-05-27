using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Expenses.Client.WebApp.Infrastructure;
using Expenses.Common;
using Expenses.Common.Models;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace Expenses.Client.WebApp.Controllers
{
    [Route("[controller]/[action]")]
    public class AccountController : Controller
    {
        private readonly IHttpClientFactory httpClientFactory;
        private readonly MsalTokenProvider tokenProvider;

        public AccountController(IHttpClientFactory httpClientFactory, MsalTokenProvider tokenProvider)
        {
            this.httpClientFactory = httpClientFactory;
            this.tokenProvider = tokenProvider;
        }

        public async Task<IActionResult> Identity()
        {
            var relatedApplicationIdentities = new List<IdentityInfo>();
            if (this.User.Identity.IsAuthenticated)
            {
                // Get an access token from the MSAL token provider to call the back-end Web API with permissions to read identity information.
                var token = await this.tokenProvider.GetTokenForUserAsync(this.HttpContext, this.User, new[] { Constants.Placeholders.ExpensesApiScopeIdentityRead });
                try
                {
                    // Get an HTTP client that is pre-configured with the back-end Web API root URL.
                    var client = this.httpClientFactory.CreateClient(Constants.HttpClientNames.ExpensesApi);

                    // Call the back-end Web API using a bearer access token retrieved from the token provider.
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

                    // Request identity information as seen by the back-end Web API.
                    var response = await client.GetAsync("api/account/identity");
                    response.EnsureSuccessStatusCode();

                    // Deserialize the response into an IdentityInfo instance.
                    var apiIdentityInfoValue = await response.Content.ReadAsStringAsync();
                    var apiIdentityInfo = JsonConvert.DeserializeObject<IdentityInfo>(apiIdentityInfoValue);
                    relatedApplicationIdentities.Add(apiIdentityInfo);
                }
                catch (Exception exc)
                {
                    relatedApplicationIdentities.Add(IdentityInfo.FromException(exc, "Expense API"));
                }
            }

            // Return identity information as seen from this application, including related applications.
            var identityInfo = IdentityInfo.FromPrincipal(this.User, "ID Token", "Expense Web App", relatedApplicationIdentities);
            return View(identityInfo);
        }
    }
}