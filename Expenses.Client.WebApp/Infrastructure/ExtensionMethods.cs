using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace Expenses.Client.WebApp.Infrastructure
{
    public static class ExtensionMethods
    {
        public static async Task<ActionResult> RedirectToError(this Controller controller, HttpResponseMessage response)
        {
            var message = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(message))
            {
                message = $"{response.StatusCode.ToString()} {response.ReasonPhrase}";
            }
            return new RedirectToActionResult("Error", "Home", new { Message = message });
        }
    }
}