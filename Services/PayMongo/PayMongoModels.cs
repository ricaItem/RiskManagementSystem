namespace WEB_Sentro.Services.PayMongo;

/// <summary>
/// Result of creating or retrieving a PayMongo Payment Intent.
/// </summary>
public class PayMongoPaymentIntentResult
{
    public string Id { get; set; } = string.Empty;
    public string ClientKey { get; set; } = string.Empty;
    public long Amount { get; set; }
    public string Currency { get; set; } = "PHP";
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// If status is awaiting_next_action (e.g. 3DS), URL or details for the customer to complete.
    /// </summary>
    public PayMongoNextAction? NextAction { get; set; }
}

public class PayMongoNextAction
{
    public string Type { get; set; } = string.Empty;
    public string? RedirectUrl { get; set; }
}
