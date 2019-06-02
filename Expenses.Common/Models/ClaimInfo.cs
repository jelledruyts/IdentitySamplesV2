using System.Collections.Generic;

namespace Expenses.Common.Models
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

        /// <summary>
        /// A remark about the claim (e.g. the interpretation of its value).
        /// </summary>
        public string Remark { get; set; }
    }
}