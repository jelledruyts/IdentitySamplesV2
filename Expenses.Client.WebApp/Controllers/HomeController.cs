using System.Diagnostics;
using Expenses.Client.WebApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Expenses.Client.WebApp.Controllers
{
    public class HomeController : Controller
    {
        [Route("")]
        public IActionResult Index()
        {
            return View();
        }

        [AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        [Route("[action]")]
        public IActionResult Error(string message)
        {
            return View(new ErrorViewModel { Message = message ?? "An error occurred while processing your request.", RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}