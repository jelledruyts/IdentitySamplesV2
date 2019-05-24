using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Expenses.Common.Models;
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
            return IdentityInfo.FromPrincipal(this.User, "Access Token", "Expense API");
        }

        [HttpGet]
        public ActionResult<IEnumerable<string>> Roles()
        {
            var roleClaimType = ((ClaimsIdentity)this.User.Identity).RoleClaimType;
            return this.User.FindAll(roleClaimType).Select(c => c.Value).ToList();
        }
    }
}