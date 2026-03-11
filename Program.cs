using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WEB_Sentro.Data;
using WEB_Sentro.Models.Identity;
using WEB_Sentro.Data.Seed;
using WEB_Sentro.Services;

var builder = WebApplication.CreateBuilder(args);

// --------------------
// Platform DB (shared)
// --------------------
var platformConnectionString = builder.Configuration.GetConnectionString("PlatformDb")
    ?? builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'PlatformDb' (or legacy 'DefaultConnection') not found.");

builder.Services.AddDbContext<PlatformDbContext>(options =>
    options.UseSqlServer(platformConnectionString, sql =>
    {
        sql.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorNumbersToAdd: null);
    }));

// Legacy; NOT used for Risk Monitoring (tenant data). Monitoring uses ITenantDbFactory only.
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(platformConnectionString, sql =>
    {
        sql.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorNumbersToAdd: null);
    }));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// Identity uses PlatformDbContext
builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<PlatformDbContext>()
.AddClaimsPrincipalFactory<ApplicationUserClaimsPrincipalFactory>();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Identity/Account/Login";
    options.LogoutPath = "/Identity/Account/Logout";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";

    options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
    options.SlidingExpiration = true;

    options.Events.OnSigningIn = context =>
    {
        context.CookieOptions.Expires = null;
        context.Properties.IsPersistent = false;
        return Task.CompletedTask;
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("VendorOnly", p => p.RequireRole("SuperAdmin"));
    options.AddPolicy("AdminOrVendor", p => p.RequireRole("SuperAdmin", "Admin"));
});

// --------------------
// Tenant DB resolution
// --------------------
builder.Services.AddScoped<ITenantConnectionProvider, ConfigTenantConnectionProvider>();
builder.Services.AddScoped<ITenantDbFactory, TenantDbFactory>();

// App services
builder.Services.AddScoped<RiskService>();
builder.Services.AddScoped<RiskAnalyticsService>();
builder.Services.AddScoped<RiskAnalyticsPdfService>();
builder.Services.AddScoped<RiskAttachmentService>();
builder.Services.AddScoped<RiskEvaluationService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IRiskVersionService, RiskVersionService>();
builder.Services.AddScoped<IRiskMatrixService, RiskMatrixService>();
builder.Services.AddScoped<ControlService>();
builder.Services.AddScoped<RiskExportService>();
builder.Services.AddHttpClient();
builder.Services.AddScoped<IOpenWeatherService, WeatherApiService>();
builder.Services.AddScoped<MonitoringHubService>();
builder.Services.AddScoped<IProcurementOverdueService, ProcurementOverdueService>();
builder.Services.AddScoped<SupplierRiskService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IIncidentService, IncidentService>();
builder.Services.AddHostedService<MonitoringSyncHostedService>();
builder.Services.AddHostedService<RiskReviewReminderHostedService>();

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

var app = builder.Build();

// --------------------
// Auto migrate (Platform)
// --------------------
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var autoMigrate = app.Configuration.GetValue<bool>("Database:AutoMigrate");

    if (autoMigrate)
    {
        // Platform DB migration (Identity + platform tables)
        var platformDb = services.GetRequiredService<PlatformDbContext>();
        await platformDb.Database.MigrateAsync();

        await IdentitySeeder.SeedAsync(services, app.Configuration);

        // OPTIONAL: tenant migration bootstrap (OFF by default)
        // This only migrates one tenant (ex: orgId=1) if enabled.
        var autoMigrateTenant = app.Configuration.GetValue<bool>("Database:AutoMigrateTenantOrg1");
        if (autoMigrateTenant)
        {
            var tenantFactory = services.GetRequiredService<ITenantDbFactory>();
            await using var tenantDb = await tenantFactory.CreateAsync(1);
            await tenantDb.Database.MigrateAsync();
        }
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

app.Run();