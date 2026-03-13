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
            var key = $"Tenant_Org_{orgId}";
            var cs = _configuration.GetConnectionString(key);

            if (string.IsNullOrWhiteSpace(cs) && orgId <= 0)
            {
                cs = _configuration.GetConnectionString("Tenant_Org_1");
            }

            return Task.FromResult(cs);
        }
    }
}
