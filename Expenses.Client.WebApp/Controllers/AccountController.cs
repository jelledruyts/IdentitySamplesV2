using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Expenses.Client.WebApp.Models;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace Expenses.Client.WebApp.Controllers
{
    [Route("[controller]/[action]")]
    public class AccountController : Controller
    {
        private readonly IHttpClientFactory httpClientFactory;

        public AccountController(IHttpClientFactory httpClientFactory)
        {
            this.httpClientFactory = httpClientFactory;
        }

        public async Task<IActionResult> Identity()
        {
            var relatedApplicationIdentities = new List<IdentityInfo>();
            try
            {
                // Request identity information as seen by the back-end Web API.
                var client = this.httpClientFactory.CreateClient(Constants.HttpClientNames.ExpensesApi);
                // Fetch the access token from the current user's claims to avoid the complexity of an external token cache (see Startup.cs).
                var accessTokenClaim = this.User.Claims.SingleOrDefault(c => c.Type == Constants.ClaimTypes.AccessToken);
                if (accessTokenClaim != null)
                {
                    // Call the back-end Web API using the bearer access token.
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessTokenClaim.Value);
                    var response = await client.GetAsync("api/account/identity");
                    response.EnsureSuccessStatusCode();

                    // Deserialize the response into an IdentityInfo instance.
                    var apiIdentityInfoValue = await response.Content.ReadAsStringAsync();
                    var apiIdentityInfo = JsonConvert.DeserializeObject<IdentityInfo>(apiIdentityInfoValue);
                    relatedApplicationIdentities.Add(apiIdentityInfo);
                }
            }
            catch (Exception exc)
            {
                relatedApplicationIdentities.Add(new IdentityInfo
                {
                    Source = "Exception",
                    Application = "Expense API",
                    IsAuthenticated = false,
                    Claims = new[] { new ClaimInfo { Type = "ExceptionMessage", Value = exc.Message }, new ClaimInfo { Type = "ExceptionDetail", Value = exc.ToString() } }
                });
            }
            // Return identity information as seen from this application, including related applications.
            var identityInfo = new IdentityInfo
            {
                Source = "ID Token",
                Application = "Expense Web App",
                IsAuthenticated = this.User.Identity.IsAuthenticated,
                Name = this.User.Identity.Name,
                AuthenticationType = this.User.Identity.AuthenticationType,
                Claims = this.User.Claims.Select(c => new ClaimInfo { Type = c.Type, Value = c.Value }).ToList(),
                RelatedApplicationIdentities = relatedApplicationIdentities
            };
            return View(identityInfo);
        }
    }
}