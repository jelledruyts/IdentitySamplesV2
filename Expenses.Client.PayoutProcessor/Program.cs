using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography.X509Certificates;
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
            var clientCertificateName = configuration.GetValue<string>("AzureAd:ClientCertificateName");
            var confidentialClientApplicationOptions = new ConfidentialClientApplicationOptions();
            configuration.Bind("AzureAd", confidentialClientApplicationOptions);
            var scopes = new[] { $"{expensesApiAppIdUri}/.default" }; // The client credentials flow ALWAYS uses the "/.default" scope.

            while (true)
            {
                try
                {
                    Console.WriteLine("A - Process expense payouts, using a client secret");
                    Console.WriteLine("B - Process expense payouts, using a client certificate");
                    Console.Write("Type your choice and press Enter: ");
                    var choice = Console.ReadLine();
                    if (string.Equals(choice, "A", StringComparison.InvariantCultureIgnoreCase))
                    {
                        // Retrieve an access token to call the back-end Web API using a client secret
                        // representing this application (rather than a user).
                        var confidentialClientApplication = GetConfidentialClientApplicationUsingClientSecret(confidentialClientApplicationOptions);
                        var token = await confidentialClientApplication.AcquireTokenForClient(scopes).ExecuteAsync();
                        await ProcessExpensesAsync(expensesApiBaseUrl, token.AccessToken);
                    }
                    else if (string.Equals(choice, "B", StringComparison.InvariantCultureIgnoreCase))
                    {
                        // Retrieve an access token to call the back-end Web API using a client certificate
                        // representing this application (rather than a user).
                        var confidentialClientApplication = GetConfidentialClientApplicationUsingClientCertificate(confidentialClientApplicationOptions, clientCertificateName);
                        var token = await confidentialClientApplication.AcquireTokenForClient(scopes).ExecuteAsync();
                        await ProcessExpensesAsync(expensesApiBaseUrl, token.AccessToken);
                    }
                    else
                    {
                        break;
                    }
                    Console.WriteLine();
                }
                catch (Exception exc)
                {
                    Console.WriteLine(exc.ToString());
                }
            }
        }

        private static IConfidentialClientApplication GetConfidentialClientApplicationUsingClientSecret(ConfidentialClientApplicationOptions options)
        {
            return ConfidentialClientApplicationBuilder.CreateWithApplicationOptions(options).Build();
        }

        private static IConfidentialClientApplication GetConfidentialClientApplicationUsingClientCertificate(ConfidentialClientApplicationOptions options, string clientCertificateName)
        {
            using (var store = new X509Store(StoreLocation.CurrentUser))
            {
                store.Open(OpenFlags.ReadOnly);
                var certificate = store.Certificates.Find(X509FindType.FindBySubjectName, clientCertificateName, false).Cast<X509Certificate2>().FirstOrDefault();
                if (certificate == null)
                {
                    throw new InvalidOperationException($"The certificate with subject name \"{clientCertificateName}\" could not be found, please create it first.");
                }
                return ConfidentialClientApplicationBuilder.Create(options.ClientId)
                    .WithTenantId(options.TenantId)
                    .WithCertificate(certificate)
                    .Build();
            }
        }

        private static async Task ProcessExpensesAsync(string expensesApiBaseUrl, string accessToken)
        {
            // Put the access token on the authorization header by default.
            var client = new HttpClient();
            client.BaseAddress = new Uri(expensesApiBaseUrl);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

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
    }
}