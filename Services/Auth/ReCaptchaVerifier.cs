using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using WEB_Sentro.Models.Auth;

namespace WEB_Sentro.Services.Auth;

public class ReCaptchaVerifier : IReCaptchaVerifier
{
    private const string VerifyUrl = "https://www.google.com/recaptcha/api/siteverify";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ReCaptchaOptions _options;
    private readonly ILogger<ReCaptchaVerifier> _logger;

    public ReCaptchaVerifier(IHttpClientFactory httpClientFactory, IOptions<ReCaptchaOptions> options, ILogger<ReCaptchaVerifier> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ReCaptchaVerificationResult> VerifyAsync(string token, string? remoteIp, string expectedAction, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
            return new ReCaptchaVerificationResult(true, 1.0, expectedAction, null);

        if (string.IsNullOrWhiteSpace(_options.SecretKey))
            return new ReCaptchaVerificationResult(false, 0, null, "reCAPTCHA secret key is not configured.");

        if (string.IsNullOrWhiteSpace(token))
            return new ReCaptchaVerificationResult(false, 0, null, "Missing reCAPTCHA token.");

        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            var payload = new Dictionary<string, string>
            {
                ["secret"] = _options.SecretKey,
                ["response"] = token
            };

            if (!string.IsNullOrWhiteSpace(remoteIp))
                payload["remoteip"] = remoteIp;

            using var response = await httpClient.PostAsync(VerifyUrl, new FormUrlEncodedContent(payload), cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("reCAPTCHA verification HTTP failure: {StatusCode}", (int)response.StatusCode);
                return new ReCaptchaVerificationResult(false, 0, null, "reCAPTCHA verification service returned an error.");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var verifyResponse = await JsonSerializer.DeserializeAsync<ReCaptchaGoogleResponse>(stream, cancellationToken: cancellationToken);
            if (verifyResponse == null)
                return new ReCaptchaVerificationResult(false, 0, null, "Invalid reCAPTCHA verification response.");

            if (!verifyResponse.Success)
                return new ReCaptchaVerificationResult(false, verifyResponse.Score, verifyResponse.Action, string.Join(",", verifyResponse.ErrorCodes ?? []));

            if (!string.Equals(verifyResponse.Action, expectedAction, StringComparison.Ordinal))
                return new ReCaptchaVerificationResult(false, verifyResponse.Score, verifyResponse.Action, "reCAPTCHA action mismatch.");

            if (verifyResponse.Score < _options.MinimumScore)
                return new ReCaptchaVerificationResult(false, verifyResponse.Score, verifyResponse.Action, "reCAPTCHA score below threshold.");

            return new ReCaptchaVerificationResult(true, verifyResponse.Score, verifyResponse.Action, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "reCAPTCHA verification failed unexpectedly.");
            return new ReCaptchaVerificationResult(false, 0, null, "Unable to verify reCAPTCHA.");
        }
    }

    private sealed class ReCaptchaGoogleResponse
    {
        public bool Success { get; set; }
        public double Score { get; set; }
        public string? Action { get; set; }
        [JsonPropertyName("error-codes")]
        public string[]? ErrorCodes { get; set; }
    }
}
