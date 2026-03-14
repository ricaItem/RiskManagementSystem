using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WEB_Sentro.Areas.Client.Models;
using WEB_Sentro.Data;
using WEB_Sentro.Data.Entities;
using WEB_Sentro.Models.Identity;
using WEB_Sentro.Services;

namespace WEB_Sentro.Areas.Client.Controllers
{
    [Area("Client")]
    [Authorize]
    public class OrganizationController : Controller
    {
        private readonly PlatformDbContext _platformDb;
        private readonly ITenantDbFactory _tenantDbFactory;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAuditService _auditService;
        private readonly IWebHostEnvironment _env;

        public OrganizationController(
            PlatformDbContext platformDb,
            ITenantDbFactory tenantDbFactory,
            UserManager<ApplicationUser> userManager,
            IAuditService auditService,
            IWebHostEnvironment env)
        {
            _platformDb = platformDb;
            _tenantDbFactory = tenantDbFactory;
            _userManager = userManager;
            _auditService = auditService;
            _env = env;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string tab = "profile", CancellationToken ct = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();
            if (user.OrganizationId <= 0) return Forbid();

            var vm = await BuildViewModelAsync(user.OrganizationId, tab, ct);
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> UpdateProfile([Bind(Prefix = "Profile")] OrganizationProfileForm model, IFormFile? logoFile, CancellationToken ct)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();
            if (user.OrganizationId <= 0) return Forbid();

            if (!ModelState.IsValid)
            {
                TempData["OrgSettingsError"] = "Please review the profile fields and try again.";
                return RedirectToAction(nameof(Index), new { tab = "profile" });
            }

            var org = await _platformDb.Organizations.FirstOrDefaultAsync(o => o.OrganizationId == user.OrganizationId, ct);
            if (org == null) return NotFound();

            org.OrgName = model.OrgName.Trim();
            org.DisplayName = Normalize(model.DisplayName);
            org.Website = Normalize(model.Website);
            org.PrimaryEmail = Normalize(model.PrimaryEmail);
            org.PrimaryPhone = Normalize(model.PrimaryPhone);
            org.TaxId = Normalize(model.TaxId);

            if (logoFile != null && logoFile.Length > 0)
            {
                if (!logoFile.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                {
                    TempData["OrgSettingsError"] = "Logo upload failed. Only image files are allowed.";
                    return RedirectToAction(nameof(Index), new { tab = "profile" });
                }

                const long maxBytes = 2 * 1024 * 1024;
                if (logoFile.Length > maxBytes)
                {
                    TempData["OrgSettingsError"] = "Logo upload failed. Maximum size is 2 MB.";
                    return RedirectToAction(nameof(Index), new { tab = "profile" });
                }

                var ext = Path.GetExtension(logoFile.FileName);
                var fileName = $"{Guid.NewGuid():N}{ext}";
                var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "org-logos", user.OrganizationId.ToString());
                Directory.CreateDirectory(uploadsFolder);
                var filePath = Path.Combine(uploadsFolder, fileName);

                await using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await logoFile.CopyToAsync(stream, ct);
                }

                org.LogoPath = $"/uploads/org-logos/{user.OrganizationId}/{fileName}";
            }

            org.UpdatedAt = DateTime.UtcNow;
            await _platformDb.SaveChangesAsync(ct);

            await _auditService.LogAsync(
                user.OrganizationId,
                user.Id,
                "OrganizationSettings",
                org.OrganizationId,
                "OrganizationProfileUpdated",
                "Organization profile settings updated.",
                "Info",
                HttpContext.Connection.RemoteIpAddress?.ToString());

            TempData["OrgSettingsSuccess"] = "Profile settings updated.";
            return RedirectToAction(nameof(Index), new { tab = "profile" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> UpdateAddresses([Bind(Prefix = "Addresses")] OrganizationAddressForm model, CancellationToken ct)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();
            if (user.OrganizationId <= 0) return Forbid();

            if (!ModelState.IsValid)
            {
                TempData["OrgSettingsError"] = "Please review the address fields and try again.";
                return RedirectToAction(nameof(Index), new { tab = "addresses" });
            }

            var org = await _platformDb.Organizations.FirstOrDefaultAsync(o => o.OrganizationId == user.OrganizationId, ct);
            if (org == null) return NotFound();

            org.LegalAddressLine1 = Normalize(model.LegalAddressLine1);
            org.LegalAddressLine2 = Normalize(model.LegalAddressLine2);
            org.LegalCity = Normalize(model.LegalCity);
            org.LegalProvince = Normalize(model.LegalProvince);
            org.LegalPostalCode = Normalize(model.LegalPostalCode);
            org.LegalCountry = Normalize(model.LegalCountry);

            org.BillingAddressLine1 = Normalize(model.BillingAddressLine1);
            org.BillingAddressLine2 = Normalize(model.BillingAddressLine2);
            org.BillingCity = Normalize(model.BillingCity);
            org.BillingProvince = Normalize(model.BillingProvince);
            org.BillingPostalCode = Normalize(model.BillingPostalCode);
            org.BillingCountry = Normalize(model.BillingCountry);
            org.BillingContactName = Normalize(model.BillingContactName);
            org.BillingEmail = Normalize(model.BillingEmail);
            org.BillingPhone = Normalize(model.BillingPhone);

            org.AddressLine = org.LegalAddressLine1;
            org.City = org.LegalCity;
            org.Province = org.LegalProvince;
            org.Country = org.LegalCountry;
            org.UpdatedAt = DateTime.UtcNow;

            await _platformDb.SaveChangesAsync(ct);

            await _auditService.LogAsync(
                user.OrganizationId,
                user.Id,
                "OrganizationSettings",
                org.OrganizationId,
                "OrganizationAddressesUpdated",
                "Organization legal and billing addresses updated.",
                "Info",
                HttpContext.Connection.RemoteIpAddress?.ToString());

            TempData["OrgSettingsSuccess"] = "Address settings updated.";
            return RedirectToAction(nameof(Index), new { tab = "addresses" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> UpgradePlan(int planId, CancellationToken ct)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();
            if (user.OrganizationId <= 0) return Forbid();

            var subscription = await _platformDb.Subscriptions
                .Include(s => s.Plan)
                .FirstOrDefaultAsync(s => s.OrganizationId == user.OrganizationId && s.Status == "Active", ct);
            if (subscription == null)
            {
                TempData["OrgSettingsError"] = "No active subscription found.";
                return RedirectToAction(nameof(Index), new { tab = "billing" });
            }

            var targetPlan = await _platformDb.Plans.FirstOrDefaultAsync(p => p.PlanId == planId && p.IsActive, ct);
            if (targetPlan == null)
            {
                TempData["OrgSettingsError"] = "Selected plan is unavailable.";
                return RedirectToAction(nameof(Index), new { tab = "billing" });
            }

            if (targetPlan.AmountCentavos <= subscription.Plan.AmountCentavos)
            {
                TempData["OrgSettingsError"] = "Use schedule downgrade for lower-tier plans.";
                return RedirectToAction(nameof(Index), new { tab = "billing" });
            }

            if (targetPlan.PlanId == subscription.PlanId)
            {
                TempData["OrgSettingsError"] = "Your organization is already on this plan.";
                return RedirectToAction(nameof(Index), new { tab = "billing" });
            }

            var now = DateTime.UtcNow;
            var oldPlanName = subscription.Plan.DisplayName;
            var additionalAmount = targetPlan.AmountCentavos - subscription.Plan.AmountCentavos;

            subscription.PlanId = targetPlan.PlanId;
            subscription.PendingPlanId = null;
            subscription.PendingChangeType = null;
            subscription.PendingChangeEffectiveAt = null;
            subscription.UpdatedAt = now;

            var org = await _platformDb.Organizations.FirstOrDefaultAsync(o => o.OrganizationId == user.OrganizationId, ct);
            if (org != null)
            {
                org.PlanName = targetPlan.Code;
                org.UpdatedAt = now;
            }

            var invoice = new Invoice
            {
                OrganizationId = user.OrganizationId,
                SubscriptionId = subscription.SubscriptionId,
                InvoiceNumber = await GenerateNextInvoiceNumberAsync(ct),
                Status = "Paid",
                AmountDueCentavos = additionalAmount,
                Currency = targetPlan.Currency,
                PeriodStart = now,
                PeriodEnd = subscription.CurrentPeriodEnd,
                DueDate = now,
                PaidAt = now,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedByUserId = user.Id
            };
            _platformDb.Invoices.Add(invoice);

            var payment = new Payment
            {
                OrganizationId = user.OrganizationId,
                Invoice = invoice,
                Gateway = "Manual",
                GatewayPaymentIntentId = $"manual_upgrade_{Guid.NewGuid():N}",
                GatewayStatus = "succeeded",
                AmountCentavos = additionalAmount,
                Currency = targetPlan.Currency,
                PaymentMethod = "Account Adjustment",
                Status = "Succeeded",
                PaidAt = now,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedByUserId = user.Id
            };
            _platformDb.Payments.Add(payment);

            await _platformDb.SaveChangesAsync(ct);

            await _auditService.LogAsync(
                user.OrganizationId,
                user.Id,
                "Subscription",
                subscription.SubscriptionId,
                "SubscriptionUpgraded",
                $"Plan upgraded: {oldPlanName} -> {targetPlan.DisplayName}.",
                "Info",
                HttpContext.Connection.RemoteIpAddress?.ToString());

            TempData["OrgSettingsSuccess"] = $"Plan upgraded to {targetPlan.DisplayName}.";
            return RedirectToAction(nameof(Index), new { tab = "billing" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> ScheduleDowngrade(int planId, CancellationToken ct)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();
            if (user.OrganizationId <= 0) return Forbid();

            var subscription = await _platformDb.Subscriptions
                .Include(s => s.Plan)
                .FirstOrDefaultAsync(s => s.OrganizationId == user.OrganizationId && s.Status == "Active", ct);
            if (subscription == null)
            {
                TempData["OrgSettingsError"] = "No active subscription found.";
                return RedirectToAction(nameof(Index), new { tab = "billing" });
            }

            var targetPlan = await _platformDb.Plans.FirstOrDefaultAsync(p => p.PlanId == planId && p.IsActive, ct);
            if (targetPlan == null)
            {
                TempData["OrgSettingsError"] = "Selected plan is unavailable.";
                return RedirectToAction(nameof(Index), new { tab = "billing" });
            }

            if (targetPlan.PlanId == subscription.PlanId)
            {
                TempData["OrgSettingsError"] = "Your organization is already on this plan.";
                return RedirectToAction(nameof(Index), new { tab = "billing" });
            }

            if (targetPlan.AmountCentavos >= subscription.Plan.AmountCentavos)
            {
                TempData["OrgSettingsError"] = "Use upgrade for higher-tier plans.";
                return RedirectToAction(nameof(Index), new { tab = "billing" });
            }

            subscription.PendingPlanId = targetPlan.PlanId;
            subscription.PendingChangeType = "Downgrade";
            subscription.PendingChangeEffectiveAt = subscription.CurrentPeriodEnd;
            subscription.UpdatedAt = DateTime.UtcNow;
            await _platformDb.SaveChangesAsync(ct);

            await _auditService.LogAsync(
                user.OrganizationId,
                user.Id,
                "Subscription",
                subscription.SubscriptionId,
                "SubscriptionDowngradeScheduled",
                $"Downgrade scheduled: {subscription.Plan.DisplayName} -> {targetPlan.DisplayName} on {subscription.CurrentPeriodEnd:yyyy-MM-dd}.",
                "Info",
                HttpContext.Connection.RemoteIpAddress?.ToString());

            TempData["OrgSettingsSuccess"] = $"Downgrade scheduled to {targetPlan.DisplayName} on next billing cycle.";
            return RedirectToAction(nameof(Index), new { tab = "billing" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> CancelScheduledDowngrade(CancellationToken ct)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();
            if (user.OrganizationId <= 0) return Forbid();

            var subscription = await _platformDb.Subscriptions
                .FirstOrDefaultAsync(s => s.OrganizationId == user.OrganizationId && s.Status == "Active", ct);
            if (subscription == null)
            {
                TempData["OrgSettingsError"] = "No active subscription found.";
                return RedirectToAction(nameof(Index), new { tab = "billing" });
            }

            if (!string.Equals(subscription.PendingChangeType, "Downgrade", StringComparison.OrdinalIgnoreCase))
            {
                TempData["OrgSettingsError"] = "No scheduled downgrade found.";
                return RedirectToAction(nameof(Index), new { tab = "billing" });
            }

            subscription.PendingPlanId = null;
            subscription.PendingChangeType = null;
            subscription.PendingChangeEffectiveAt = null;
            subscription.UpdatedAt = DateTime.UtcNow;

            await _platformDb.SaveChangesAsync(ct);

            await _auditService.LogAsync(
                user.OrganizationId,
                user.Id,
                "Subscription",
                subscription.SubscriptionId,
                "SubscriptionDowngradeCanceled",
                "Scheduled downgrade canceled.",
                "Info",
                HttpContext.Connection.RemoteIpAddress?.ToString());

            TempData["OrgSettingsSuccess"] = "Scheduled downgrade canceled.";
            return RedirectToAction(nameof(Index), new { tab = "billing" });
        }

        private async Task<OrganizationSettingsViewModel> BuildViewModelAsync(int orgId, string tab, CancellationToken ct)
        {
            var org = await _platformDb.Organizations.AsNoTracking().FirstOrDefaultAsync(o => o.OrganizationId == orgId, ct);
            if (org == null)
            {
                return new OrganizationSettingsViewModel
                {
                    ActiveTab = NormalizeTab(tab),
                    OrganizationId = orgId,
                    OrgCode = string.Empty,
                    OrgName = "Organization"
                };
            }

            var subscription = await _platformDb.Subscriptions
                .AsNoTracking()
                .Include(s => s.Plan)
                .Include(s => s.PendingPlan)
                .FirstOrDefaultAsync(s => s.OrganizationId == orgId && s.Status == "Active", ct);

            var plans = await _platformDb.Plans.AsNoTracking()
                .Where(p => p.IsActive)
                .OrderBy(p => p.SortOrder)
                .ToListAsync(ct);

            var invoices = await _platformDb.Invoices.AsNoTracking()
                .Where(i => i.OrganizationId == orgId)
                .OrderByDescending(i => i.CreatedAt)
                .Take(12)
                .ToListAsync(ct);

            var payments = await _platformDb.Payments.AsNoTracking()
                .Where(p => p.OrganizationId == orgId)
                .OrderByDescending(p => p.CreatedAt)
                .Take(12)
                .ToListAsync(ct);

            var members = await _platformDb.Users.AsNoTracking()
                .Where(u => u.OrganizationId == orgId)
                .OrderBy(u => u.FirstName)
                .ThenBy(u => u.LastName)
                .Take(200)
                .ToListAsync(ct);

            var userRows = new List<OrgUserRowViewModel>();
            foreach (var member in members)
            {
                var memberRoles = await _userManager.GetRolesAsync(member);
                userRows.Add(new OrgUserRowViewModel
                {
                    UserId = member.Id,
                    FullName = $"{member.FirstName} {member.LastName}".Trim(),
                    Email = member.Email ?? string.Empty,
                    IsActive = member.IsActive,
                    CreatedAt = member.CreatedAt,
                    Roles = memberRoles.OrderBy(r => r).ToList()
                });
            }

            List<AuditRowViewModel> auditRows;
            await using (var tenantDb = await _tenantDbFactory.CreateAsync(orgId))
            {
                auditRows = await tenantDb.AuditLogs.AsNoTracking()
                    .Where(a => a.OrgId == orgId && (a.EntityType == "OrganizationSettings" || a.EntityType == "Subscription"))
                    .OrderByDescending(a => a.CreatedAt)
                    .Take(30)
                    .Select(a => new AuditRowViewModel
                    {
                        CreatedAt = a.CreatedAt,
                        ActionType = a.ActionType,
                        EntityType = a.EntityType,
                        Level = a.Level,
                        Message = a.Message,
                        UserId = a.UserId
                    })
                    .ToListAsync(ct);
            }

            var activeTab = NormalizeTab(tab);
            var currentPlanAmount = subscription?.Plan.AmountCentavos ?? 0;

            return new OrganizationSettingsViewModel
            {
                ActiveTab = activeTab,
                CanEditOrganization = User.IsInRole("Admin") || User.IsInRole("SuperAdmin"),
                CanManageBilling = User.IsInRole("Admin") || User.IsInRole("SuperAdmin"),
                OrganizationId = org.OrganizationId,
                OrgCode = org.OrgCode,
                OrgName = org.OrgName,
                Status = org.Status,
                Profile = new OrganizationProfileForm
                {
                    OrgName = org.OrgName,
                    DisplayName = org.DisplayName ?? org.OrgName,
                    Website = org.Website,
                    PrimaryEmail = org.PrimaryEmail,
                    PrimaryPhone = org.PrimaryPhone,
                    TaxId = org.TaxId,
                    LogoPath = org.LogoPath
                },
                Addresses = new OrganizationAddressForm
                {
                    LegalAddressLine1 = org.LegalAddressLine1 ?? org.AddressLine,
                    LegalAddressLine2 = org.LegalAddressLine2,
                    LegalCity = org.LegalCity ?? org.City,
                    LegalProvince = org.LegalProvince ?? org.Province,
                    LegalPostalCode = org.LegalPostalCode,
                    LegalCountry = org.LegalCountry ?? org.Country,
                    BillingAddressLine1 = org.BillingAddressLine1,
                    BillingAddressLine2 = org.BillingAddressLine2,
                    BillingCity = org.BillingCity,
                    BillingProvince = org.BillingProvince,
                    BillingPostalCode = org.BillingPostalCode,
                    BillingCountry = org.BillingCountry,
                    BillingContactName = org.BillingContactName,
                    BillingEmail = org.BillingEmail,
                    BillingPhone = org.BillingPhone
                },
                CurrentSubscription = subscription == null ? null : new CurrentSubscriptionViewModel
                {
                    SubscriptionId = subscription.SubscriptionId,
                    CurrentPlanId = subscription.PlanId,
                    CurrentPlanName = subscription.Plan.DisplayName,
                    CurrentPlanAmountCentavos = subscription.Plan.AmountCentavos,
                    Currency = subscription.Plan.Currency,
                    BillingInterval = subscription.Plan.BillingInterval,
                    Status = subscription.Status,
                    CurrentPeriodStart = subscription.CurrentPeriodStart,
                    CurrentPeriodEnd = subscription.CurrentPeriodEnd,
                    PendingPlanId = subscription.PendingPlanId,
                    PendingPlanName = subscription.PendingPlan?.DisplayName,
                    PendingChangeType = subscription.PendingChangeType,
                    PendingChangeEffectiveAt = subscription.PendingChangeEffectiveAt
                },
                AvailablePlans = plans.Select(p => new PlanOptionViewModel
                {
                    PlanId = p.PlanId,
                    Code = p.Code,
                    DisplayName = p.DisplayName,
                    AmountCentavos = p.AmountCentavos,
                    Currency = p.Currency,
                    BillingInterval = p.BillingInterval,
                    IsCurrent = subscription != null && p.PlanId == subscription.PlanId,
                    IsUpgrade = subscription != null && p.AmountCentavos > currentPlanAmount,
                    IsDowngrade = subscription != null && p.AmountCentavos < currentPlanAmount
                }).ToList(),
                Invoices = invoices.Select(i => new InvoiceRowViewModel
                {
                    InvoiceId = i.InvoiceId,
                    InvoiceNumber = i.InvoiceNumber,
                    Status = i.Status,
                    AmountDueCentavos = i.AmountDueCentavos,
                    Currency = i.Currency,
                    DueDate = i.DueDate,
                    PaidAt = i.PaidAt,
                    CreatedAt = i.CreatedAt
                }).ToList(),
                Payments = payments.Select(p => new PaymentRowViewModel
                {
                    PaymentId = p.PaymentId,
                    Gateway = p.Gateway,
                    Status = p.Status,
                    AmountCentavos = p.AmountCentavos,
                    Currency = p.Currency,
                    PaidAt = p.PaidAt,
                    CreatedAt = p.CreatedAt
                }).ToList(),
                Users = userRows,
                AuditEntries = auditRows
            };
        }

        private async Task<string> GenerateNextInvoiceNumberAsync(CancellationToken ct)
        {
            var year = DateTime.UtcNow.Year;
            var count = await _platformDb.Invoices.CountAsync(i => i.CreatedAt.Year == year, ct);
            return $"INV-{year}-{(count + 1):D5}";
        }

        private static string NormalizeTab(string? tab)
        {
            var value = (tab ?? "profile").Trim().ToLowerInvariant();
            return value switch
            {
                "profile" => "profile",
                "addresses" => "addresses",
                "billing" => "billing",
                "users" => "users",
                "audit" => "audit",
                _ => "profile"
            };
        }

        private static string? Normalize(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            return value.Trim();
        }
    }
}
