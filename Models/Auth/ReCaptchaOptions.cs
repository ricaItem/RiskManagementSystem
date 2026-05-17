namespace WEB_Sentro.Models.Auth;

public class ReCaptchaOptions
{
    public const string SectionName = "ReCaptcha";

    public bool Enabled { get; set; } = false;
    public string SiteKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public double MinimumScore { get; set; } = 0.5;
}
