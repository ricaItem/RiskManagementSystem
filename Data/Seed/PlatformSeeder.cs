using Microsoft.EntityFrameworkCore;
using WEB_Sentro.Data;
using WEB_Sentro.Data.Entities;

namespace WEB_Sentro.Data.Seed;

/// <summary>
/// Seeds platform data: Plans (Basic, Professional, Enterprise).
/// </summary>
public static class PlatformSeeder
{
    public static async Task SeedPlansAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<PlatformDbContext>();
        if (await db.Plans.AnyAsync())
            return;

        var plans = new[]
        {
            new Plan { Code = "Basic", DisplayName = "Basic", AmountCentavos = 4900, Currency = "PHP", BillingInterval = "month", IsActive = true, SortOrder = 1 },
            new Plan { Code = "Professional", DisplayName = "Professional", AmountCentavos = 14900, Currency = "PHP", BillingInterval = "month", IsActive = true, SortOrder = 2 },
            new Plan { Code = "Enterprise", DisplayName = "Enterprise", AmountCentavos = 39900, Currency = "PHP", BillingInterval = "month", IsActive = true, SortOrder = 3 },
        };

        db.Plans.AddRange(plans);
        await db.SaveChangesAsync();
    }
}
