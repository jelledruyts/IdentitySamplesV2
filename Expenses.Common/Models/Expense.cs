using System;

namespace Expenses.Common.Models
{
    public class Expense
    {
        public Guid Id { get; set; }
        public string Purpose { get; set; }
        public decimal Amount { get; set; }
        public string CreatedUserId { get; set; }
        public string CreatedUserDisplayName { get; set; }
        public DateTimeOffset CreatedDate { get; set; }
    }
}