using System;
using System.Collections.Generic;
using System.Linq;
using Expenses.Common;
using Expenses.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Expenses.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ExpensesController : ControllerBase
    {
        private readonly List<Expense> database = new List<Expense>();

        [Authorize(Constants.AuthorizationPolicies.ReadMyExpenses)]
        [HttpGet]
        public ActionResult<IEnumerable<Expense>> Get()
        {
            return this.database.Where(e => e.CreatedUserId == this.User.GetUserId()).ToList();
        }

        [Authorize(Constants.AuthorizationPolicies.ReadMyExpenses)]
        [HttpGet("{id}")]
        public ActionResult<Expense> Get(Guid id)
        {
            var expense = this.database.Where(e => e.Id == id).SingleOrDefault();
            if (expense == null)
            {
                return NotFound();
            }
            if (expense.CreatedUserId != this.User.GetUserId())
            {
                return Unauthorized();
            }
            return expense;
        }

        [Authorize(Constants.AuthorizationPolicies.ReadWriteMyExpenses)]
        [HttpPost]
        public void Post([FromBody]Expense value)
        {
            // Don't trust user input, always set critical values in the API.
            value.Id = Guid.NewGuid();
            value.CreatedDate = DateTimeOffset.UtcNow;
            value.CreatedUserId = this.User.GetUserId();
            value.CreatedUserDisplayName = this.User.Identity.Name;
            this.database.Add(value);
        }

        [Authorize(Constants.AuthorizationPolicies.ReadWriteMyExpenses)]
        [HttpPut("{id}")]
        public ActionResult Put(Guid id, [FromBody]Expense value)
        {
            var expense = this.database.Where(e => e.Id == id).SingleOrDefault();
            if (expense == null)
            {
                return NotFound();
            }
            if (expense.CreatedUserId != this.User.GetUserId())
            {
                return Unauthorized();
            }
            // Don't trust user input, only apply certain values on updates.
            expense.Purpose = value.Purpose;
            expense.Amount = value.Amount;
            return Ok();
        }

        [Authorize(Constants.AuthorizationPolicies.ReadWriteMyExpenses)]
        [HttpDelete("{id}")]
        public ActionResult Delete(Guid id)
        {
            var expense = this.database.Where(e => e.Id == id).SingleOrDefault();
            if (expense == null)
            {
                return NotFound();
            }
            if (expense.CreatedUserId != this.User.GetUserId())
            {
                return Unauthorized();
            }
            this.database.Remove(expense);
            return Ok();
        }
    }
}
