using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Text.Json;
using WEB_Sentro.Data;
using WEB_Sentro.Data.Entities;
using WEB_Sentro.Models.Identity;
using WEB_Sentro.Data.Seed;
using WEB_Sentro.Services;
using WEB_Sentro.Services.Auth;
using WEB_Sentro.Services.PayMongo;
using Microsoft.AspNetCore.Identity.UI.Services;
using WEB_Sentro.Filters;
using WEB_Sentro.Models.Auth;

var builder = WebApplication.CreateBuilder(args);

// --------------------
// Platform DB (shared)
// --------------------
var platformConnectionString = builder.Configuration.GetConnectionString("PlatformDb")
    ?? builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'PlatformDb' (or legacy 'DefaultConnection') not found.");

var securityDefaults = LoadSecurityDefaults(platformConnectionString);

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
    options.Password.RequiredLength = securityDefaults?.PasswordMinLength ?? 12;
    options.Password.RequireUppercase = securityDefaults?.RequireUppercase ?? true;
    options.Password.RequireLowercase = securityDefaults?.RequireLowercase ?? true;
    options.Password.RequireDigit = securityDefaults?.RequireDigit ?? true;
    options.Password.RequireNonAlphanumeric = securityDefaults?.RequireNonAlphanumeric ?? true;
    options.Lockout.MaxFailedAccessAttempts = securityDefaults?.LockoutMaxFailedAccessAttempts ?? 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(securityDefaults?.LockoutWindowMinutes ?? 15);
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<PlatformDbContext>()
.AddClaimsPrincipalFactory<ApplicationUserClaimsPrincipalFactory>();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Identity/Account/Login";
    options.LogoutPath = "/Identity/Account/Logout";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";

    options.ExpireTimeSpan = TimeSpan.FromMinutes(securityDefaults?.SessionTimeoutMinutes ?? 60);
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
    options.AddPolicy("NonEmployee", p => p.RequireAssertion(ctx => !ctx.User.IsInRole("Employee")));

    options.AddPolicy("SuperAdminOnly", p => p.RequireRole("SuperAdmin"));
    options.AddPolicy("MainAdminOnly", p => p.RequireRole("SuperAdmin", "Admin"));
    options.AddPolicy("RiskGovernance", p => p.RequireRole("SuperAdmin", "Admin", "RiskManager"));
    options.AddPolicy("ProcurementAccess", p => p.RequireRole("SuperAdmin", "Admin", "ProcurementOfficer"));
    options.AddPolicy("EmployeeWorkspace", p => p.RequireRole("Employee"));
    options.AddPolicy("RiskContributors", p => p.RequireRole("SuperAdmin", "Admin", "RiskManager", "Employee"));
    options.AddPolicy("ClientReports", p => p.RequireRole("SuperAdmin", "Admin", "RiskManager"));
});

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwtOptions.SigningKey))
{
    throw new InvalidOperationException("Jwt:SigningKey is required in configuration.");
}

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<ReCaptchaOptions>(builder.Configuration.GetSection(ReCaptchaOptions.SectionName));
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IReCaptchaVerifier, ReCaptchaVerifier>();

builder.Services.AddAuthentication()
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
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
builder.Services.AddMemoryCache();
builder.Services.Configure<PayMongoOptions>(builder.Configuration.GetSection(PayMongoOptions.SectionName));
builder.Services.AddScoped<IPayMongoService, PayMongoService>();
builder.Services.AddScoped<IOpenWeatherService, WeatherApiService>();
builder.Services.AddScoped<MonitoringHubService>();
builder.Services.AddScoped<IProcurementOverdueService, ProcurementOverdueService>();
builder.Services.AddScoped<SupplierRiskService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IGlobalSettingsService, GlobalSettingsService>();
builder.Services.AddScoped<IOrganizationGovernanceService, OrganizationGovernanceService>();
builder.Services.AddScoped<IRevenueAnalyticsService, RevenueAnalyticsService>();
builder.Services.AddScoped<IIncidentService, IncidentService>();
builder.Services.AddScoped<ProjectService>();
builder.Services.AddScoped<OrganizationAnalyticsSnapshotRefreshService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHostedService<MonitoringSyncHostedService>();
builder.Services.AddHostedService<RiskReviewReminderHostedService>();
builder.Services.AddHostedService<OrganizationAnalyticsSnapshotHostedService>();

builder.Services.AddScoped<OrganizationWriteAccessFilter>();
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.AddService<OrganizationWriteAccessFilter>();
});
builder.Services.AddRazorPages();

//SMTP --start--

builder.Services.AddTransient<IEmailSender, SmtpEmailSender>();

//SMTP --end--

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
            
            // Seed Cost Codes
            await WEB_Sentro.Data.Seed.CostCodeSeeder.SeedAsync(tenantDb, 1);
        }
    }

    await PlatformSeeder.SeedPlansAsync(services);
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

static SecurityPolicyDefaults? LoadSecurityDefaults(string connectionString)
{
    try
    {
        var dbOptions = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        using var db = new PlatformDbContext(dbOptions);
        var json = db.PlatformSettings
            .AsNoTracking()
            .Where(x => x.Key == GlobalSettingKeys.SecurityPolicies)
            .Select(x => x.JsonValue)
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return JsonSerializer.Deserialize<SecurityPolicyDefaults>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }
    catch
    {
        return null;
    }
}
