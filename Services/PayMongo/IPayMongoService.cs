namespace WEB_Sentro.Services.PayMongo;

/// <summary>
/// PayMongo payment operations. Use for creating payment intents (card, GCash, Maya) and checking status.
/// </summary>
public interface IPayMongoService
{
    /// <summary>
    /// Create a payment intent. Returns client_key and id for client-side payment method attachment (card 3DS, GCash, Maya).
    /// </summary>
    /// <param name="amountInCentavos">Amount in centavos (e.g. 10000 = ₱100.00). Minimum 10000.</param>
    /// <param name="paymentMethods">Allowed methods: "card", "paymaya", "gcash".</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<PayMongoPaymentIntentResult> CreatePaymentIntentAsync(
        long amountInCentavos,
        IReadOnlyList<string> paymentMethods,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieve a payment intent by id (e.g. to poll status after 3DS or redirect).
    /// </summary>
    Task<PayMongoPaymentIntentResult?> GetPaymentIntentAsync(string paymentIntentId, CancellationToken cancellationToken = default);
}
