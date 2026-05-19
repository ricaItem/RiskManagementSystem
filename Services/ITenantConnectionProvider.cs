using System.Threading.Tasks;

namespace WEB_Sentro.Services
{
    public interface ITenantConnectionProvider
    {
        Task<string?> GetTenantConnectionStringAsync(int orgId);
    }
}
