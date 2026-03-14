using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using WEB_Sentro.Data;
using WEB_Sentro.Data.Entities;
using WEB_Sentro.Models.Identity;
using WEB_Sentro.Services.PayMongo;

namespace WEB_Sentro.Controllers.Api;

/// <summary>
/// Registration flow: email verification, complete registration after payment.
/// </summary>
[ApiController]
[Route("api/registration")]
[AllowAnonymous]
public class RegistrationController : ControllerBase
{
    private readonly IMemoryCache _cache;
    private readonly IEmailSender _emailSender;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly PlatformDbContext _db;
    private readonly IPayMongoService _payMongo;
    private readonly IWebHostEnvironment _env;
    private const string VerifyCacheKeyPrefix = "reg_verify:";
    private static readonly TimeSpan VerifyCodeExpiry = TimeSpan.FromMinutes(15);
    private static readonly string[] AllowedPlans = { "Basic", "Professional", "Enterprise" };

    public RegistrationController(
        IMemoryCache cache,
        IEmailSender emailSender,
        UserManager<ApplicationUser> userManager,
        PlatformDbContext db,
        IPayMongoService payMongo,
        IWebHostEnvironment env)
    {
        _cache = cache;
        _emailSender = emailSender;
        _userManager = userManager;
        _db = db;
        _payMongo = payMongo;
        _env = env;
    }

    /// <summary>Send a 6-digit verification code to the email. Stores in cache for 15 minutes.</summary>
    [HttpPost("send-verification-code")]
    public async Task<IActionResult> SendVerificationCode([FromBody] SendCodeRequest request, CancellationToken ct)
    {
        var email = request?.Email?.Trim();
        if (string.IsNullOrEmpty(email) || !IsValidEmail(email))
            return BadRequest(new { error = "Valid email is required." });

        var code = new Random().Next(100000, 999999).ToString();
        _cache.Set(VerifyCacheKeyPrefix + email.ToLowerInvariant(), code, VerifyCodeExpiry);

        var html = $@"
<div style='font-family: sans-serif; max-width: 400px;'>
  <h2 style='color: #0B1F33;'>Sentro – Verify your email</h2>
  <p>Your verification code is:</p>
  <p style='font-size: 28px; font-weight: bold; letter-spacing: 4px; color: #0B1F33;'>{code}</p>
  <p style='color: #64748b; font-size: 14px;'>This code expires in 15 minutes. If you didn't request this, you can ignore this email.</p>
  <p style='margin-top: 24px;'>— Sentro</p>
</div>";
        await _emailSender.SendEmailAsync(email, "Your Sentro verification code", html);

        return Ok(new { success = true, message = "Verification code sent." });
    }

    /// <summary>Verify the 6-digit code for the email. Returns success so the client can proceed to next step.</summary>
    [HttpPost("verify-code")]
    public IActionResult VerifyCode([FromBody] VerifyCodeRequest request)
    {
        var email = request?.Email?.Trim();
        var code = request?.Code?.Trim();
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(code) || code.Length != 6)
            return BadRequest(new { error = "Email and 6-digit code are required." });

        var key = VerifyCacheKeyPrefix + email.ToLowerInvariant();
        if (!_cache.TryGetValue(key, out string? stored) || stored != code)
            return BadRequest(new { error = "Invalid or expired code. Please try again or request a new code." });

        _cache.Remove(key);
        return Ok(new { success = true });
    }

    /// <summary>Complete registration after payment: create organization and admin user, send receipt email. Requires payment_intent_id with status succeeded, or use useTestPayment in Development.</summary>
    [HttpPost("complete")]
    public async Task<IActionResult> CompleteRegistration([FromBody] CompleteRegistrationRequest request, CancellationToken ct)
    {
        if (request == null)
            return BadRequest(new { error = "Request body is required." });

        var email = request.Email?.Trim();
        if (string.IsNullOrEmpty(email) || !IsValidEmail(email))
            return BadRequest(new { error = "Valid email is required." });

        var plan = request.Plan?.Trim();
        if (string.IsNullOrEmpty(plan) || !AllowedPlans.Contains(plan))
            return BadRequest(new { error = "Valid plan is required (Basic, Professional, Enterprise)." });

        var orgName = request.OrganizationName?.Trim();
        if (string.IsNullOrEmpty(orgName))
            return BadRequest(new { error = "Organization name is required." });

        var orgCode = request.OrganizationCode?.Trim();
        if (string.IsNullOrEmpty(orgCode))
            return BadRequest(new { error = "Organization code is required." });

        var firstName = request.AdminFirstName?.Trim();
        var lastName = request.AdminLastName?.Trim();
        if (string.IsNullOrEmpty(firstName) || string.IsNullOrEmpty(lastName))
            return BadRequest(new { error = "Admin first and last name are required." });

        var password = request.Password;
        if (string.IsNullOrEmpty(password) || password.Length < 12)
            return BadRequest(new { error = "Password must be at least 12 characters." });

        bool paymentOk = false;
        string? paymentIntentId = request.PaymentIntentId?.Trim();

        if (request.UseTestPayment == true && _env.IsDevelopment())
        {
            paymentOk = true;
        }
        else if (!string.IsNullOrEmpty(paymentIntentId))
        {
            var intent = await _payMongo.GetPaymentIntentAsync(paymentIntentId, ct);
            if (intent != null && intent.Status == "succeeded")
                paymentOk = true;
        }

        // Credentials (organization, user account) are stored only after payment is confirmed. No persistence until this point.
        if (!paymentOk)
            return BadRequest(new { error = "Valid payment is required. Complete payment first or use test mode in development." });

        if (await _userManager.FindByEmailAsync(email) != null)
            return BadRequest(new { error = "An account with this email already exists." });

        if (await _db.Organizations.AnyAsync(o => o.OrgCode == orgCode, ct))
            return BadRequest(new { error = "This organization code is already in use." });

        var planEntity = await _db.Plans.FirstOrDefaultAsync(p => p.Code == plan, ct);
        if (planEntity == null)
            return BadRequest(new { error = "Plan not found. Ensure database migrations are applied and plans are seeded." });

        var now = DateTime.UtcNow;
        var periodEnd = now.AddMonths(1);

        Organization org;
        Invoice invoice;
        ApplicationUser user;

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            org = new Organization
            {
                OrgCode = orgCode,
                OrgName = orgName,
                PrimaryEmail = email,
                PlanName = plan,
                Status = "Active",
                CreatedAt = now
            };
            _db.Organizations.Add(org);
            await _db.SaveChangesAsync(ct);

            var subscription = new Subscription
            {
                OrganizationId = org.OrganizationId,
                PlanId = planEntity.PlanId,
                Status = "Active",
                CurrentPeriodStart = now,
                CurrentPeriodEnd = periodEnd,
                StartedAt = now,
                CreatedAt = now
            };
            _db.Subscriptions.Add(subscription);
            await _db.SaveChangesAsync(ct);

            var invoiceNumber = await GenerateNextInvoiceNumberAsync(ct);
            invoice = new Invoice
            {
                OrganizationId = org.OrganizationId,
                SubscriptionId = subscription.SubscriptionId,
                InvoiceNumber = invoiceNumber,
                Status = "Open",
                AmountDueCentavos = planEntity.AmountCentavos,
                Currency = planEntity.Currency,
                DueDate = now,
                CreatedAt = now
            };
            _db.Invoices.Add(invoice);
            await _db.SaveChangesAsync(ct);

            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FirstName = firstName,
                LastName = lastName,
                OrganizationId = org.OrganizationId,
                IsActive = true,
                CreatedAt = now
            };
            var createResult = await _userManager.CreateAsync(user, password);
            if (!createResult.Succeeded)
            {
                await transaction.RollbackAsync(ct);
                return BadRequest(new { error = string.Join(" ", createResult.Errors.Select(e => e.Description)) });
            }

            org.CreatedByUserId = user.Id;

            var finalPaymentIntentId = paymentIntentId ?? "test";
            var payment = new Payment
            {
                OrganizationId = org.OrganizationId,
                InvoiceId = invoice.InvoiceId,
                Gateway = "PayMongo",
                GatewayPaymentIntentId = finalPaymentIntentId,
                GatewayStatus = "succeeded",
                AmountCentavos = planEntity.AmountCentavos,
                Currency = planEntity.Currency,
                Status = "Succeeded",
                PaidAt = now,
                CreatedAt = now,
                CreatedByUserId = user.Id
            };
            _db.Payments.Add(payment);

            invoice.Status = "Paid";
            invoice.PaidAt = now;
            invoice.UpdatedAt = now;

            await _db.SaveChangesAsync(ct);
            await _userManager.AddToRoleAsync(user, "Admin");
            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }

        var amountDisplay = planEntity.AmountCentavos % 100 == 0
            ? $"₱{planEntity.AmountCentavos / 100:N0}"
            : $"₱{planEntity.AmountCentavos / 100.0:F2}";
        var receiptHtml = $@"
<div style='font-family: sans-serif; max-width: 500px;'>
  <h2 style='color: #0B1F33;'>Invoice &amp; payment receipt – Sentro</h2>
  <p>Thank you for subscribing. This email is your invoice and receipt for this payment.</p>
  <table style='width: 100%; border-collapse: collapse; margin: 20px 0;'>
    <tr style='border-bottom: 1px solid #e2e8f0;'><td style='padding: 8px 0; color: #64748b;'>Invoice number</td><td style='padding: 8px 0; font-weight: 600;'>{System.Net.WebUtility.HtmlEncode(invoice.InvoiceNumber)}</td></tr>
    <tr style='border-bottom: 1px solid #e2e8f0;'><td style='padding: 8px 0; color: #64748b;'>Organization</td><td style='padding: 8px 0;'>{System.Net.WebUtility.HtmlEncode(orgName)}</td></tr>
    <tr style='border-bottom: 1px solid #e2e8f0;'><td style='padding: 8px 0; color: #64748b;'>Plan</td><td style='padding: 8px 0;'>{System.Net.WebUtility.HtmlEncode(plan)}</td></tr>
    <tr style='border-bottom: 1px solid #e2e8f0;'><td style='padding: 8px 0; color: #64748b;'>Amount paid</td><td style='padding: 8px 0;'>{amountDisplay}</td></tr>
    <tr><td style='padding: 8px 0; color: #64748b;'>Date paid</td><td style='padding: 8px 0;'>{now:yyyy-MM-dd HH:mm} UTC</td></tr>
  </table>
  <p style='color: #64748b; font-size: 14px;'>You can log in at your Sentro login page with this email and your password.</p>
  <p style='margin-top: 24px;'>— Sentro</p>
</div>";
        await _emailSender.SendEmailAsync(email, "Your Sentro invoice and payment receipt", receiptHtml);

        return Ok(new
        {
            success = true,
            organizationId = org.OrganizationId,
            userId = user.Id,
            redirectUrl = Url.Content("~/Identity/Account/RegisterSuccess") + "?email=" + Uri.EscapeDataString(email) + "&plan=" + Uri.EscapeDataString(plan) + "&amount=" + Uri.EscapeDataString(amountDisplay)
        });
    }

    private async Task<string> GenerateNextInvoiceNumberAsync(CancellationToken ct)
    {
        var year = DateTime.UtcNow.Year;
        var count = await _db.Invoices.CountAsync(i => i.CreatedAt.Year == year, ct);
        return $"INV-{year}-{(count + 1):D5}";
    }

    private static bool IsValidEmail(string email)
    {
        try { var addr = new System.Net.Mail.MailAddress(email); return addr.Address == email; }
        catch { return false; }
    }
}

public class SendCodeRequest
{
    [Required] public string? Email { get; set; }
}

public class VerifyCodeRequest
{
    [Required] public string? Email { get; set; }
    [Required] [StringLength(6, MinimumLength = 6)] public string? Code { get; set; }
}

public class CompleteRegistrationRequest
{
    public string? Email { get; set; }
    public string? Plan { get; set; }
    public string? OrganizationName { get; set; }
    public string? OrganizationCode { get; set; }
    public string? AdminFirstName { get; set; }
    public string? AdminMiddleName { get; set; }
    public string? AdminLastName { get; set; }
    public string? Password { get; set; }
    public string? PaymentIntentId { get; set; }
    public bool? UseTestPayment { get; set; }
}
