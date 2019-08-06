using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Expenses.Common.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Identity.Client;
using Newtonsoft.Json;

namespace Expenses.Client.PayoutProcessor
{
    class Program
    {
        static void Main(string[] args)
        {
            RunAsync(args).Wait();
        }

        private static async Task RunAsync(string[] args)
        {
            // Load configuration.
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false)
                .AddUserSecrets<Program>(optional: true)
                .AddEnvironmentVariables()
                .AddCommandLine(args)
                .Build();
            var expensesApiBaseUrl = configuration.GetValue<string>("App:ExpensesApi:BaseUrl");
            var expensesApiAppIdUri = configuration.GetValue<string>("App:ExpensesApi:AppIdUri");
            var confidentialClientApplicationOptions = new ConfidentialClientApplicationOptions();
            configuration.Bind("AzureAd", confidentialClientApplicationOptions);

            // Retrieve an access token to call the back-end Web API using client credentials
            // representing this application (rather than a user).
            var confidentialClientApplication = ConfidentialClientApplicationBuilder.CreateWithApplicationOptions(confidentialClientApplicationOptions).Build();
            var scopes = new[] { $"{expensesApiAppIdUri}/.default" }; // The client credentials flow ALWAYS uses the "/.default" scope.
            var token = await confidentialClientApplication.AcquireTokenForClient(scopes).ExecuteAsync();

            // Put the access token on the authorization header by default.
            var client = new HttpClient();
            client.BaseAddress = new Uri(expensesApiBaseUrl);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

            while (true)
            {
                try
                {
                    Console.WriteLine("A - Process expense payouts");
                    Console.Write("Type your choice and press Enter: ");
                    var choice = Console.ReadLine();
                    if (string.Equals(choice, "A", StringComparison.InvariantCultureIgnoreCase))
                    {
                        // Request all expenses from the back-end Web API.
                        var getAllResponse = await client.GetAsync("api/expenses/all");
                        getAllResponse.EnsureSuccessStatusCode();
                        var allExpensesValue = await getAllResponse.Content.ReadAsStringAsync();
                        var allExpenses = JsonConvert.DeserializeObject<List<Expense>>(allExpensesValue);

                        // Process all approved expenses by marking them paid and updating them in the back-end Web API.
                        var approvedExpenses = allExpenses.Where(e => e.Status == ExpenseStatus.Approved).ToList();
                        Console.WriteLine($"Paying out {approvedExpenses.Count} of {allExpenses.Count} expenses.");
                        foreach (var approvedExpense in approvedExpenses)
                        {
                            Console.WriteLine($"- Processing expense \"{approvedExpense.Id}\" for user \"{approvedExpense.CreatedUserDisplayName}\"");
                            approvedExpense.Status = ExpenseStatus.Paid;
                            var approvedExpenseValue = JsonConvert.SerializeObject(approvedExpense);
                            var updateResponse = await client.PutAsync($"api/expenses/{approvedExpense.Id}", new StringContent(approvedExpenseValue, Encoding.UTF8, "application/json"));
                            updateResponse.EnsureSuccessStatusCode();
                        }
                    }
                    else
                    {
                        break;
                    }
                }
                catch (Exception exc)
                {
                    Console.WriteLine(exc.ToString());
                }
            }
        }
    }
}