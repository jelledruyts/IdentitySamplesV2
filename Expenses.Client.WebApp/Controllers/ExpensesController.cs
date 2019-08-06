using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Expenses.Client.WebApp.Infrastructure;
using Expenses.Common;
using Expenses.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Expenses.Client.WebApp.Controllers
{
    public class ExpensesController : Controller
    {
        private readonly IHttpClientFactory httpClientFactory;
        private readonly MsalTokenProvider tokenProvider;

        public ExpensesController(IHttpClientFactory httpClientFactory, MsalTokenProvider tokenProvider)
        {
            this.httpClientFactory = httpClientFactory;
            this.tokenProvider = tokenProvider;
        }

        [Authorize(Constants.AuthorizationPolicies.ReadMyExpenses)]
        [InteractiveSignInRequiredExceptionFilter(Scopes = new[] { Constants.Placeholders.ExpensesApiScopeExpensesRead })]
        [Route("[controller]")]
        public async Task<IActionResult> Index()
        {
            // Get an access token from the MSAL token provider to call the back-end Web API with permissions to read expenses.
            var token = await this.tokenProvider.GetTokenForUserAsync(this.HttpContext, this.User, new[] { Constants.Placeholders.ExpensesApiScopeExpensesRead });

            // Get an HTTP client that is pre-configured with the back-end Web API root URL.
            var client = this.httpClientFactory.CreateClient(Constants.HttpClientNames.ExpensesApi);

            // Call the back-end Web API using a bearer access token retrieved from the token provider.
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

            // Request expenses from the back-end Web API.
            var response = await client.GetAsync("api/expenses");
            if (!response.IsSuccessStatusCode)
            {
                return await this.RedirectToError(response);
            }

            // Deserialize the response into an expense list.
            var expenses = await response.Content.ReadAsAsync<IList<Expense>>();

            return View(expenses);
        }

        [Authorize(Constants.AuthorizationPolicies.ReadWriteMyExpenses)]
        [Route("[controller]/[action]")]
        public IActionResult Create()
        {
            return View(new Expense());
        }

        [Authorize(Constants.AuthorizationPolicies.ReadWriteMyExpenses)]
        [InteractiveSignInRequiredExceptionFilter(Scopes = new[] { Constants.Placeholders.ExpensesApiScopeExpensesReadWrite })]
        [Route("[controller]/[action]")]
        [HttpPost]
        public async Task<IActionResult> Create(Expense expense)
        {
            if (ModelState.IsValid)
            {
                // Get an access token from the MSAL token provider to call the back-end Web API with permissions to write expenses.
                var token = await this.tokenProvider.GetTokenForUserAsync(this.HttpContext, this.User, new[] { Constants.Placeholders.ExpensesApiScopeExpensesReadWrite });

                // Get an HTTP client that is pre-configured with the back-end Web API root URL.
                var client = this.httpClientFactory.CreateClient(Constants.HttpClientNames.ExpensesApi);

                // Call the back-end Web API using a bearer access token retrieved from the token provider.
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

                var response = await client.PostAsJsonAsync("api/expenses", expense);
                if (!response.IsSuccessStatusCode)
                {
                    return await this.RedirectToError(response);
                }

                return RedirectToAction(nameof(Index));
            }
            return View(expense);
        }

        [Authorize(Constants.AuthorizationPolicies.ReadWriteMyExpenses)]
        [InteractiveSignInRequiredExceptionFilter(Scopes = new[] { Constants.Placeholders.ExpensesApiScopeExpensesReadWrite })]
        [Route("[controller]/[action]")]
        [HttpPost]
        public async Task<IActionResult> Delete(Guid id)
        {
            // Get an access token from the MSAL token provider to call the back-end Web API with permissions to write expenses.
            var token = await this.tokenProvider.GetTokenForUserAsync(this.HttpContext, this.User, new[] { Constants.Placeholders.ExpensesApiScopeExpensesReadWrite });

            // Get an HTTP client that is pre-configured with the back-end Web API root URL.
            var client = this.httpClientFactory.CreateClient(Constants.HttpClientNames.ExpensesApi);

            // Call the back-end Web API using a bearer access token retrieved from the token provider.
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

            var response = await client.DeleteAsync($"api/expenses/{id}");
            if (!response.IsSuccessStatusCode)
            {
                return await this.RedirectToError(response);
            }

            return RedirectToAction(nameof(Index));
        }

        [Authorize(Constants.AuthorizationPolicies.ApproveExpenses)]
        [InteractiveSignInRequiredExceptionFilter(Scopes = new[] { Constants.Placeholders.ExpensesApiScopeExpensesReadAll })]
        [Route("[controller]/[action]")]
        public async Task<IActionResult> Approve()
        {
            // Get an access token from the MSAL token provider to call the back-end Web API with permissions to read and write expenses.
            var token = await this.tokenProvider.GetTokenForUserAsync(this.HttpContext, this.User, new[] { Constants.Placeholders.ExpensesApiScopeExpensesReadAll });

            // Get an HTTP client that is pre-configured with the back-end Web API root URL.
            var client = this.httpClientFactory.CreateClient(Constants.HttpClientNames.ExpensesApi);

            // Call the back-end Web API using a bearer access token retrieved from the token provider.
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

            // Request expenses from the back-end Web API.
            var response = await client.GetAsync("api/expenses/all");
            if (!response.IsSuccessStatusCode)
            {
                return await this.RedirectToError(response);
            }

            // Deserialize the response into an expense list.
            var expenses = await response.Content.ReadAsAsync<IList<Expense>>();

            return View(expenses);
        }

        [Authorize(Constants.AuthorizationPolicies.ApproveExpenses)]
        [InteractiveSignInRequiredExceptionFilter(Scopes = new[] { Constants.Placeholders.ExpensesApiScopeExpensesReadWrite })]
        [Route("[controller]/[action]")]
        [HttpPost]
        public async Task<IActionResult> Approve(Expense value)
        {
            // Get an access token from the MSAL token provider to call the back-end Web API with permissions to write expenses.
            var token = await this.tokenProvider.GetTokenForUserAsync(this.HttpContext, this.User, new[] { Constants.Placeholders.ExpensesApiScopeExpensesReadWrite });

            // Get an HTTP client that is pre-configured with the back-end Web API root URL.
            var client = this.httpClientFactory.CreateClient(Constants.HttpClientNames.ExpensesApi);

            // Call the back-end Web API using a bearer access token retrieved from the token provider.
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

            value.Status = ExpenseStatus.Approved;
            var response = await client.PutAsJsonAsync($"api/expenses/{value.Id}", value);
            if (!response.IsSuccessStatusCode)
            {
                return await this.RedirectToError(response);
            }

            return RedirectToAction(nameof(Approve));
        }
    }
}