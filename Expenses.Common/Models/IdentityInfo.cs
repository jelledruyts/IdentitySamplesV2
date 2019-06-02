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
        private static readonly DateTimeOffset UnixTimestampEpoch = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);

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
                Claims = principal.Claims.Select(c => new ClaimInfo { Type = c.Type, Value = c.Value, Remark = GetRemark(c) }).ToList(),
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

        private static string GetRemark(Claim claim)
        {
            // [NOTE] Certain claims can be interpreted to more meaningful information.
            // See https://msdn.microsoft.com/en-us/library/azure/dn195587.aspx and
            // https://docs.microsoft.com/en-us/azure/active-directory/develop/active-directory-token-and-claims among others.
            switch (claim.Type.ToLowerInvariant())
            {
                case "aud":
                    return "The audience of the targeted application, i.e. the intended recipient of the token";
                case "iss":
                    return "The issuer, i.e. the Security Token Service that issued the token";
                case "idp":
                    return "The Identity Provider that authenticated the subject of the token";
                case "scp":
                case "http://schemas.microsoft.com/identity/claims/scope":
                    return "The scope, i.e. the impersonation permissions granted to the client application";
                case "iat":
                    return GetTimestampDescription("Issued at", claim.Value, true);
                case "nbf":
                    return GetTimestampDescription("Not valid before", claim.Value, true);
                case "exp":
                    return GetTimestampDescription("Not valid after", claim.Value, true);
                case "ver":
                    return "Version";
                case "pwd_exp":
                    return GetTimestampDescription("Password expires", claim.Value, false);
                case "appid":
                    return "Application id of the client that is using the token to access a resource";
                case "appidacr":
                    return "Application Authentication Context Class Reference" + (claim.Value == "0" ? ": Public Client" : (claim.Value == "1" ? ": Confidential Client (Client ID + Secret)" : (claim.Value == "2" ? ": Confidential Client (X509 Certificate)" : null)));
                case "auth_time":
                    return GetTimestampDescription("Authentication time", claim.Value, true);
                case "http://schemas.microsoft.com/ws/2008/06/identity/claims/authenticationinstant":
                    return GetTimestampDescription("Authentication instant", claim.Value, true);
                case "oid":
                    return "The unique and immutable object identifier of the user";
                case "name":
                case "unique_name":
                    return "A human-readable value that identifies the subject of the token";
                case "preferred_username":
                    return "The primary username that represents the user";
                case "email":
                    return "The email address of the user";
                case "ipaddr":
                    return "The IP address of the user";
                case "sub":
                    return "Subject, i.e. the principal about which the token asserts information, such as the user of an application";
                case "roles":
                    return "The roles that are assigned to the user";
                case "nonce":
                    return "The nonce value used to validate the token response";
                case "c_hash":
                    return "A hash of the OAuth 2.0 authorization code that was used to redeem this token";
                case "at_hash":
                    return "A hash of the OAuth 2.0 access token";
                case "tfp":
                    return "Trust Framework Policy, i.e. the user flow (policy) through which the user authenticated in Azure AD B2C";
                case "tid":
                    return "Tenant identifier";
                case "aio":
                case "rh":
                case "uti":
                    return "An internal claim used by Azure to revalidate tokens. Should be ignored.";
                case "http://schemas.microsoft.com/claims/authnmethodsreferences":
                case "amr":
                    return "Authentication method";
                case "http://schemas.microsoft.com/claims/authnclassreference":
                case "acr":
                    return "Authentication Context Class Reference" + (claim.Value == "0" ? ": End-user authentication did not meet the requirements of ISO/IEC 29115" : null);
            }
            return null;
        }

        private static string GetTimestampDescription(string prefix, string timestamp, bool secondsSinceEpoch)
        {
            var timestampValue = default(int);
            if (int.TryParse(timestamp, out timestampValue))
            {
                var utcTimestamp = default(DateTimeOffset);
                if (secondsSinceEpoch)
                {
                    utcTimestamp = UnixTimestampEpoch.AddSeconds(timestampValue);
                }
                else
                {
                    utcTimestamp = DateTimeOffset.UtcNow.AddSeconds(timestampValue);
                }
                return $"{prefix} {utcTimestamp.ToString()} UTC";
            }
            return null;
        }
    }
}