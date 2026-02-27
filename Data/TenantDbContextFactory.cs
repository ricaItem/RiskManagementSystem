using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace WEB_Sentro.Data
{
    public class TenantDbContextFactory : IDesignTimeDbContextFactory<TenantDbContext>
    {
        public TenantDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<TenantDbContext>();

            var cs =
                "Data Source=(localdb)\\MSSQLLocalDB;Initial Catalog=tenantSentro_DB;Integrated Security=True;Trust Server Certificate=True;";

            optionsBuilder.UseSqlServer(cs);

            return new TenantDbContext(optionsBuilder.Options);
        }
    }
}