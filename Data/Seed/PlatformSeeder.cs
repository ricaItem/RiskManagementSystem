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
            new Plan { Code = "Basic", DisplayName = "Basic", AmountCentavos = 4900, Currency = "PHP", BillingInterval = "month", IsActive = true, SortOrder = 1, MaxAdminSeats = 10 },
            new Plan { Code = "Professional", DisplayName = "Professional", AmountCentavos = 14900, Currency = "PHP", BillingInterval = "month", IsActive = true, SortOrder = 2, MaxAdminSeats = 50 },
            new Plan { Code = "Enterprise", DisplayName = "Enterprise", AmountCentavos = 39900, Currency = "PHP", BillingInterval = "month", IsActive = true, SortOrder = 3, MaxAdminSeats = 200 },
        };

        foreach (var p in plans)
        {
            var existing = await db.Plans.FirstOrDefaultAsync(x => x.Code == p.Code);
            if (existing == null)
            {
                db.Plans.Add(p);
            }
            else
            {
                existing.MaxAdminSeats = p.MaxAdminSeats;
                existing.AmountCentavos = p.AmountCentavos;
                existing.DisplayName = p.DisplayName;
            }
        }
        
        await db.SaveChangesAsync();
    }
}
