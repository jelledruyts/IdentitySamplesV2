using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Expenses.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace Expenses.Api.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class AccountController : ControllerBase
    {
        [HttpGet]
        public ActionResult<IdentityInfo> Identity()
        {
            // Return identity information as seen from this application.
            return new IdentityInfo
            {
                Source = "Access Token",
                Application = "Expense API",
                IsAuthenticated = this.User.Identity.IsAuthenticated,
                Name = this.User.Identity.Name,
                AuthenticationType = this.User.Identity.AuthenticationType,
                Claims = this.User.Claims.Select(c => new ClaimInfo { Type = c.Type, Value = c.Value }).ToList()
            };
        }

        [HttpGet]
        public ActionResult<IEnumerable<string>> Roles()
        {
            var roleClaimType = ((ClaimsIdentity)this.User.Identity).RoleClaimType;
            return this.User.FindAll(roleClaimType).Select(c => c.Value).ToList();
        }
    }
}