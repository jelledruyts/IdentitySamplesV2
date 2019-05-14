using System.Collections.Generic;

namespace Expenses.Client.WebApp.Models
{
    /// <summary>
    /// Represents information about a claim.
    /// </summary>
    public class ClaimInfo
    {
        /// <summary>
        /// The claim type.
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// The claim value.
        /// </summary>
        public string Value { get; set; }
    }
}