namespace Expenses.Client.WebApp.Infrastructure
{
    public class TokenProviderOptions
    {
        public string ExpensesApiAppIdUri { get; set; }
        public string CallbackPath { get; set; }
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
        public string TenantId { get; set; }
    }
}