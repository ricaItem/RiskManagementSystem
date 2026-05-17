namespace WEB_Sentro.Services.Auth;

public interface IReCaptchaVerifier
{
    Task<ReCaptchaVerificationResult> VerifyAsync(string token, string? remoteIp, string expectedAction, CancellationToken cancellationToken = default);
}

public record ReCaptchaVerificationResult(bool IsSuccess, double Score, string? Action, string? Error);
