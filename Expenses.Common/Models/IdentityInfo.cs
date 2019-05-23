using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;

namespace Expenses.Common.Models
{
    /// <summary>
    /// Represents information about an identity as seen from an application.
    /// </summary>
    public class IdentityInfo
    {
        /// <summary>
        /// The source of the identity information.
        /// </summary>
        public string Source { get; set; }

        /// <summary>
        /// The application from which the identity is observed.
        /// </summary>
        public string Application { get; set; }

        /// <summary>
        /// Determines if the identity is authenticated.
        /// </summary>
        public bool IsAuthenticated { get; set; }

        /// <summary>
        /// The authentication type.
        /// </summary>
        public string AuthenticationType { get; set; }

        /// <summary>
        /// The identity name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The claims.
        /// </summary>
        public IList<ClaimInfo> Claims { get; set; }

        /// <summary>
        /// The identities as seen from other applications related to the current application.
        /// </summary>
        public IList<IdentityInfo> RelatedApplicationIdentities { get; set; }

        public static IdentityInfo FromPrincipal(ClaimsPrincipal principal, string source, string application)
        {
            return FromPrincipal(principal, source, application, null);
        }

        public static IdentityInfo FromPrincipal(ClaimsPrincipal principal, string source, string application, IList<IdentityInfo> relatedApplicationIdentities)
        {
            return new IdentityInfo
            {
                Source = source,
                Application = application,
                IsAuthenticated = principal.Identity.IsAuthenticated,
                Name = principal.Identity.Name,
                AuthenticationType = principal.Identity.AuthenticationType,
                Claims = principal.Claims.Select(c => new ClaimInfo { Type = c.Type, Value = c.Value }).ToList(),
                RelatedApplicationIdentities = relatedApplicationIdentities ?? Array.Empty<IdentityInfo>()
            };
        }

        public static IdentityInfo FromException(Exception exc, string application)
        {
            return new IdentityInfo
            {
                Source = "Exception",
                Application = application,
                IsAuthenticated = false,
                Claims = new[]
                {
                    new ClaimInfo { Type = "ExceptionMessage", Value = exc.Message },
                    new ClaimInfo { Type = "ExceptionDetail", Value = exc.ToString() }
                }
            };
        }
    }
}