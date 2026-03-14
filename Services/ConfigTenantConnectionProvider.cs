using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;

namespace WEB_Sentro.Services
{
    /// <summary>
    /// Reads tenant connection strings from configuration keys:
    /// ConnectionStrings:Tenant_Org_{orgId}
    /// </summary>
    public class ConfigTenantConnectionProvider : ITenantConnectionProvider
    {
        private readonly IConfiguration _configuration;

        public ConfigTenantConnectionProvider(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public Task<string?> GetTenantConnectionStringAsync(int orgId)
        {
            // 1. Try to find a specific connection string for this org (e.g. "Tenant_Org_5")
            var key = $"Tenant_Org_{orgId}";
            var cs = _configuration.GetConnectionString(key);

            // 2. If not found, fall back to the shared "TenantDb" connection string
            if (string.IsNullOrWhiteSpace(cs))
            {
                cs = _configuration.GetConnectionString("TenantDb");
            }

            // 3. Last resort fallback (useful for local dev if TenantDb isn't set but Tenant_Org_1 is)
            if (string.IsNullOrWhiteSpace(cs))
            {
                cs = _configuration.GetConnectionString("Tenant_Org_1");
            }

            return Task.FromResult(cs);
        }
    }
}
