using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Expenses.Common;
using Microsoft.AspNetCore.Authentication;

namespace Expenses.Api.Infrastructure
{
    /// <summary>
    /// Splits the scope claim assigned by Azure AD by spaces so that you can check for a scope with
    /// <code>User.HasClaim("scp", "my-scope")</code>, instead of having to split by space every time.
    /// </summary>
    /// <remarks>
    /// Inspired by https://github.com/juunas11/Joonasw.AzureAdApiSample/blob/master/Joonasw.AzureAdApiSample.Api/Authorization/AzureAdScopeClaimTransformation.cs
    /// </remarks>
    public class AzureAdScopeClaimTransformation : IClaimsTransformation
    {
        public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
        {
            // Find all "scp" claims, split their values by spaces and then take the ones that aren't yet on the principal individually.
            var scopes = principal.FindAll(Constants.ClaimTypes.Scope).SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries)).Where(s => !principal.HasClaim(Constants.ClaimTypes.Scope, s)).ToList();

            // Add all new "scp" claims to the principal's identity.
            ((ClaimsIdentity)principal.Identity).AddClaims(scopes.Select(s => new Claim(Constants.ClaimTypes.Scope, s)));
            return Task.FromResult(principal);
        }
    }
}