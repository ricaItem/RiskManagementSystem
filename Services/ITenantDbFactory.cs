using System.Threading.Tasks;
using WEB_Sentro.Data;

namespace WEB_Sentro.Services
{
    public interface ITenantDbFactory
    {
        Task<TenantDbContext> CreateAsync(int orgId);
    }
}
