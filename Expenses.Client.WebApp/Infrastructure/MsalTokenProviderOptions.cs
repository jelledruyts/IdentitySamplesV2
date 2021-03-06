using System.Collections.Generic;

namespace Expenses.Client.WebApp.Infrastructure
{
    public class MsalTokenProviderOptions
    {
        public IDictionary<string, string> ScopePlaceholderMappings { get; set; }
        public string CallbackPath { get; set; }
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
        public string TenantId { get; set; }
    }
}