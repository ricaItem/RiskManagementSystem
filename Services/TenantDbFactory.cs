using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WEB_Sentro.Data;

namespace WEB_Sentro.Services
{
    public class TenantDbFactory : ITenantDbFactory
    {
        private readonly ITenantConnectionProvider _connectionProvider;

        public TenantDbFactory(ITenantConnectionProvider connectionProvider)
        {
            _connectionProvider = connectionProvider;
        }

        public async Task<TenantDbContext> CreateAsync(int orgId)
        {
            var cs = await _connectionProvider.GetTenantConnectionStringAsync(orgId);
            if (string.IsNullOrWhiteSpace(cs))
            {
                throw new InvalidOperationException($"No tenant connection string configured for org {orgId}.");
            }

            var builder = new DbContextOptionsBuilder<TenantDbContext>();
            builder.UseSqlServer(cs, sql =>
            {
                sql.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorNumbersToAdd: null);
            });

            return new TenantDbContext(builder.Options);
        }
    }
}
