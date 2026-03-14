using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WEB_Sentro.Services.PayMongo;

namespace WEB_Sentro.Controllers.Api;

/// <summary>
/// API for PayMongo payment intents (card 3DS, GCash, Maya). Use from registration/billing flow.
/// For production, consider [Authorize] and validating the current user/session.
/// </summary>
[ApiController]
[Route("api/paymongo")]
[AllowAnonymous]
public class PayMongoController : ControllerBase
{
    private readonly IPayMongoService _payMongoService;

    public PayMongoController(IPayMongoService payMongoService)
    {
        _payMongoService = payMongoService;
    }

    /// <summary>
    /// Create a payment intent. Returns client_key and id for client-side payment method attachment.
    /// Amount in centavos (e.g. 4900 = ₱49.00). Minimum 10000 (₱100) for PayMongo; for testing use 10000 or more.
    /// </summary>
    [HttpPost("payment-intents")]
    [ProducesResponseType(typeof(PayMongoPaymentIntentResult), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> CreatePaymentIntent(
        [FromBody] CreatePaymentIntentRequest request,
        CancellationToken cancellationToken)
    {
        if (request?.AmountCentavos < 10000)
            return BadRequest(new { error = "Amount must be at least 10000 centavos (₱100.00)." });

        var methods = request?.PaymentMethodAllowed ?? new[] { "card", "paymaya", "gcash" };
        var result = await _payMongoService.CreatePaymentIntentAsync(request!.AmountCentavos, methods, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieve payment intent status (e.g. after 3DS or e-wallet redirect). Poll until status is succeeded or awaiting_payment_method.
    /// </summary>
    [HttpGet("payment-intents/{id}")]
    [ProducesResponseType(typeof(PayMongoPaymentIntentResult), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetPaymentIntent(string id, CancellationToken cancellationToken)
    {
        var result = await _payMongoService.GetPaymentIntentAsync(id, cancellationToken);
        if (result == null)
            return NotFound();
        return Ok(result);
    }
}

public class CreatePaymentIntentRequest
{
    /// <summary>Amount in centavos (e.g. 10000 = ₱100.00). Minimum 10000.</summary>
    public long AmountCentavos { get; set; }

    /// <summary>Allowed methods: "card", "paymaya", "gcash". Defaults to all if not specified.</summary>
    public string[]? PaymentMethodAllowed { get; set; }
}
