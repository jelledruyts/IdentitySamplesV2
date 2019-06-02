using System;
using System.ComponentModel.DataAnnotations;

namespace Expenses.Common.Models
{
    public class Expense
    {
        public Guid Id { get; set; }
        [Required]
        public string Purpose { get; set; }
        [Range(1, 100000)]
        public int Amount { get; set; }
        public bool IsApproved { get; set; }
        public string CreatedUserId { get; set; }
        public string CreatedUserDisplayName { get; set; }
        public DateTimeOffset CreatedDate { get; set; }
    }
}