using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace WEB_Sentro.Data
{
    /// <summary>
    /// Design-time factory for PlatformDbContext so PMC/CLI can run:
    /// Update-Database -Context PlatformDbContext
    /// </summary>
    public class PlatformDbContextFactory : IDesignTimeDbContextFactory<PlatformDbContext>
    {
        public PlatformDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<PlatformDbContext>();

            var basePath = ResolveProjectBasePath();
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
            var configuration = new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile("appsettings.json", optional: false)
                .AddJsonFile($"appsettings.{environment}.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            var cs = configuration.GetConnectionString("PlatformDb")
                     ?? configuration.GetConnectionString("DefaultConnection");

            if (string.IsNullOrWhiteSpace(cs))
            {
                throw new InvalidOperationException(
                    "Platform connection string not found. Set ConnectionStrings:PlatformDb or ConnectionStrings:DefaultConnection in appsettings.json for design-time migrations.");
            }

            optionsBuilder.UseSqlServer(cs, sql =>
            {
                sql.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorNumbersToAdd: null);
            });

            return new PlatformDbContext(optionsBuilder.Options);
        }

        /// <summary>
        /// Resolve project directory so appsettings.json is found when run from PMC or dotnet ef
        /// (current directory may be solution dir or bin/Debug/net8.0).
        /// </summary>
        private static string ResolveProjectBasePath()
        {
            var dir = Directory.GetCurrentDirectory();
            if (File.Exists(Path.Combine(dir, "appsettings.json")))
                return dir;

            var asmDir = Path.GetDirectoryName(typeof(PlatformDbContextFactory).Assembly.Location);
            if (!string.IsNullOrEmpty(asmDir))
            {
                // Running from bin/Debug/net8.0 or bin/Release/net8.0
                if (asmDir.Contains("bin", StringComparison.OrdinalIgnoreCase))
                {
                    var projectDir = Path.GetFullPath(Path.Combine(asmDir, "..", "..", ".."));
                    if (File.Exists(Path.Combine(projectDir, "appsettings.json")))
                        return projectDir;
                }
                if (File.Exists(Path.Combine(asmDir, "appsettings.json")))
                    return asmDir;
            }

            return dir;
        }
    }
}
