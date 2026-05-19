using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace WEB_Sentro.Services.PayMongo;

/// <summary>
/// PayMongo API client: create payment intents (card with 3DS, GCash, Maya) and retrieve status.
/// </summary>
public class PayMongoService : IPayMongoService
{
    private readonly HttpClient _httpClient;
    private readonly PayMongoOptions _options;

    public PayMongoService(IHttpClientFactory httpClientFactory, IOptions<PayMongoOptions> options)
    {
        _options = options.Value;
        _httpClient = httpClientFactory.CreateClient();
        _httpClient.BaseAddress = new Uri(_options.BaseUrl.TrimEnd('/') + "/");
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes(_options.SecretKey + ":")));
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<PayMongoPaymentIntentResult> CreatePaymentIntentAsync(
        long amountInCentavos,
        IReadOnlyList<string> paymentMethods,
        CancellationToken cancellationToken = default)
    {
        var body = new
        {
            data = new
            {
                attributes = new
                {
                    amount = amountInCentavos,
                    currency = "PHP",
                    payment_method_allowed = paymentMethods
                }
            }
        };
        var json = JsonSerializer.Serialize(body);
        var content = new StringContent(json, Encoding.UTF8, new MediaTypeHeaderValue("application/json"));

        var response = await _httpClient.PostAsync("v1/payment_intents", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        return ParsePaymentIntentResponse(responseJson);
    }

    public async Task<PayMongoPaymentIntentResult?> GetPaymentIntentAsync(string paymentIntentId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"v1/payment_intents/{paymentIntentId}", cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;
        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        return ParsePaymentIntentResponse(responseJson);
    }

    private static PayMongoPaymentIntentResult ParsePaymentIntentResponse(string responseJson)
    {
        using var doc = JsonDocument.Parse(responseJson);
        var data = doc.RootElement.GetProperty("data");
        var id = data.GetProperty("id").GetString() ?? "";
        var attrs = data.GetProperty("attributes");
        var clientKey = attrs.TryGetProperty("client_key", out var ck) ? ck.GetString() ?? "" : "";
        var amount = attrs.TryGetProperty("amount", out var am) ? am.GetInt64() : 0L;
        var currency = attrs.TryGetProperty("currency", out var cur) ? cur.GetString() ?? "PHP" : "PHP";
        var status = attrs.TryGetProperty("status", out var st) ? st.GetString() ?? "" : "";

        PayMongoNextAction? nextAction = null;
        if (attrs.TryGetProperty("next_action", out var na) && na.ValueKind == JsonValueKind.Object)
        {
            var type = na.TryGetProperty("type", out var t) ? t.GetString() ?? "" : "";
            var redirectUrl = na.TryGetProperty("redirect", out var r) && r.TryGetProperty("url", out var u) ? u.GetString() : null;
            nextAction = new PayMongoNextAction { Type = type, RedirectUrl = redirectUrl };
        }

        return new PayMongoPaymentIntentResult
        {
            Id = id,
            ClientKey = clientKey,
            Amount = amount,
            Currency = currency,
            Status = status,
            NextAction = nextAction
        };
    }
}
