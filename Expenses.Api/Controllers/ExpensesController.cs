using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
        private static readonly List<Expense> database = new List<Expense>();
        private readonly IAuthorizationService authorizationService;

        public ExpensesController(IAuthorizationService authorizationService)
        {
            this.authorizationService = authorizationService;
        }

        [Authorize(Constants.AuthorizationPolicies.ReadMyExpenses)]
        [HttpGet]
        public ActionResult<IEnumerable<Expense>> Get()
        {
            return database.Where(e => e.CreatedUserId == this.User.GetUserId()).ToList();
        }

        [Authorize(Constants.AuthorizationPolicies.ReadAllExpenses)]
        [HttpGet("all")]
        public ActionResult<IEnumerable<Expense>> GetAll()
        {
            return database.ToList();
        }

        [Authorize(Constants.AuthorizationPolicies.ReadMyExpenses)]
        [HttpGet("{id}")]
        public ActionResult<Expense> Get(Guid id)
        {
            var expense = database.Where(e => e.Id == id).SingleOrDefault();
            if (expense == null)
            {
                return NotFound();
            }
            if (expense.CreatedUserId != this.User.GetUserId())
            {
                return Unauthorized("You can only retrieve your own expenses.");
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
            value.Status = ExpenseStatus.Submitted; // Ensure nobody can create an expense that is already approved or paid.
            database.Add(value);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult> Put(Guid id, [FromBody]Expense value)
        {
            var expense = database.Where(e => e.Id == id).SingleOrDefault();
            if (expense == null)
            {
                return NotFound();
            }

            if (expense.Status != value.Status)
            {
                // The expense status is being changed, apply strict business rules before making changes.
                if (value.Status == ExpenseStatus.Approved)
                {
                    // The expense is being approved, apply 3 business rules.
                    // 1: The current user must have the "ExpenseApprover" role.
                    if (!User.IsInRole(Constants.Roles.ExpenseApprover))
                    {
                        return Unauthorized($"You must have the \"{Constants.Roles.ExpenseApprover}\" role.");
                    }

                    // 2: A user cannot approve their own expenses.
                    if (expense.CreatedUserId == this.User.GetUserId())
                    {
                        return Unauthorized("You are not allowed to approve your own expenses.");
                    }

                    // 3: No other fields may be changed during expense approval.
                    expense.Status = ExpenseStatus.Approved;
                }
                else if (value.Status == ExpenseStatus.Paid)
                {
                    // The expense is marked as paid out, apply 2 business rules.
                    // 1: The current user must have the "Expenses.ReadWrite.All" role.
                    if (!User.IsInRole(Constants.Roles.ExpensesReadWriteAll))
                    {
                        return Unauthorized($"You must have the \"{Constants.Roles.ExpensesReadWriteAll}\" role.");
                    }

                    // 2: No other fields may be changed during expense payment.
                    expense.Status = ExpenseStatus.Paid;
                }
            }
            else
            {
                // An expense is being updated (not approved), apply 4 business rules.
                // 1: The current user must have the proper scopes (through an authorization policy).
                var authorizationCheck = await this.authorizationService.AuthorizeAsync(this.User, Constants.AuthorizationPolicies.ReadWriteMyExpenses);
                if (!authorizationCheck.Succeeded)
                {
                    return Unauthorized("You don't have the necessary permissions to update your expenses.");
                }

                // 2: A user can only modify their own expenses.
                if (expense.CreatedUserId != this.User.GetUserId())
                {
                    return Unauthorized("You can only update your own expenses.");
                }

                // 3: Only submitted expenses can still be changed.
                if (expense.Status != ExpenseStatus.Submitted)
                {
                    return Unauthorized("You can only update submitted expenses.");
                }

                // 4: Only certain fields may be changed (e.g. prevent that users modify the created user or time).
                expense.Purpose = value.Purpose;
                expense.Amount = value.Amount;
            }
            return Ok();
        }

        [Authorize(Constants.AuthorizationPolicies.ReadWriteMyExpenses)]
        [HttpDelete("{id}")]
        public ActionResult Delete(Guid id)
        {
            var expense = database.Where(e => e.Id == id).SingleOrDefault();
            if (expense == null)
            {
                return NotFound();
            }
            if (expense.CreatedUserId != this.User.GetUserId())
            {
                return Unauthorized("You can only delete your own expenses.");
            }
            if (expense.Status != ExpenseStatus.Submitted)
            {
                return Unauthorized("You can only delete submitted expenses.");
            }
            database.Remove(expense);
            return Ok();
        }
    }
}
