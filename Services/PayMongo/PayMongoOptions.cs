namespace WEB_Sentro.Services.PayMongo;

/// <summary>
/// PayMongo API configuration. Bind from "PayMongo" section in appsettings.json.
/// </summary>
public class PayMongoOptions
{
    public const string SectionName = "PayMongo";

    /// <summary>Secret API key (sk_test_... or sk_live_...). Used for server-side calls.</summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>Public API key (pk_test_... or pk_live_...). Exposed to client for attaching payment methods.</summary>
    public string PublicKey { get; set; } = string.Empty;

    /// <summary>PayMongo API base URL. Default: https://api.paymongo.com</summary>
    public string BaseUrl { get; set; } = "https://api.paymongo.com";
}
