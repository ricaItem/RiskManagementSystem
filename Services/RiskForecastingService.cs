using Microsoft.EntityFrameworkCore;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using WEB_Sentro.Data;
using WEB_Sentro.Data.Entities;
using Microsoft.Extensions.Logging;

namespace WEB_Sentro.Services
{
    public class RiskForecastingService
    {
        private readonly ITenantDbFactory _tenantDbFactory;
        private readonly IConfiguration _config;
        private readonly ILogger<RiskForecastingService> _logger;
        private readonly HttpClient _httpClient;

        public RiskForecastingService(ITenantDbFactory tenantDbFactory, IConfiguration config, ILogger<RiskForecastingService> logger, HttpClient httpClient)
        {
            _tenantDbFactory = tenantDbFactory;
            _config = config;
            _logger = logger;
            _httpClient = httpClient;
        }

        public async Task<string> GenerateForecastAsync(int riskId, int orgId, CancellationToken ct = default)
        {
            try
            {
                var apiKey = _config["AiSettings:GeminiApiKey"]?.Trim();
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    return "AI Configuration is missing. Please contact your system administrator.";
                }

                // Debugging: Log the first few chars of the key to ensure it's loaded correctly
                var safeKeyStart = apiKey.Length > 5 ? apiKey.Substring(0, 5) : "TOO_SHORT";

                await using var db = await _tenantDbFactory.CreateAsync(orgId);

                var risk = await db.Risks.AsNoTracking().FirstOrDefaultAsync(r => r.RiskId == riskId && r.OrgId == orgId, ct);
                if (risk == null) return "Risk not found.";

                // Fetch recent evaluations
                var evaluations = await db.RiskEvaluations.AsNoTracking()
                    .Where(e => e.RiskId == riskId)
                    .OrderBy(e => e.EvaluatedAt) // Oldest to newest
                    .Take(10)
                    .Select(e => new
                    {
                        Date = e.EvaluatedAt.ToString("yyyy-MM-dd"),
                        e.LikelihoodScore,
                        e.ImpactScore,
                        e.RiskLevel,
                        e.RiskScore,
                        e.IsInherent,
                        e.Decision,
                        e.Remarks
                    })
                    .ToListAsync(ct);

                if (evaluations.Count < 2)
                {
                    return "Insufficient data for forecasting. The AI needs at least two past evaluations to analyze trends.";
                }

                // Construct Prompt
                var promptBuilder = new StringBuilder();
                promptBuilder.AppendLine($"You are an expert Risk Management AI assistant.");
                promptBuilder.AppendLine($"Analyze the trend in historical evaluations for a risk titled: \"{risk.Title}\". Category: {risk.Category}");
                promptBuilder.AppendLine("Here is the chronological evaluation history (oldest to newest):");
                foreach (var eval in evaluations)
                {
                    promptBuilder.AppendLine($"- Date: {eval.Date}, Likelihood: {eval.LikelihoodScore}/5, Impact: {eval.ImpactScore}/5, Score: {eval.RiskScore}, Level: {eval.RiskLevel}");
                }
                promptBuilder.AppendLine("Provide a concise, 3-sentence warning or forecast on where this risk is heading based on the trend, and what immediate action is recommended.");

                // Call Gemini API
                var requestUrl = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-flash-latest:generateContent?key={apiKey}";
                var payload = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new[]
                            {
                                new { text = promptBuilder.ToString() }
                            }
                        }
                    }
                };

                var response = await _httpClient.PostAsJsonAsync(requestUrl, payload, ct);
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync(ct);
                    _logger.LogError("Gemini API call failed with status: {StatusCode}. Details: {Error}", response.StatusCode, errorContent);
                    
                    if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                    {
                        return "AI Quota Exceeded. Please check your Gemini API billing or wait before trying again.";
                    }
                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        return "AI Model not found or configuration error. Please verify the Gemini model version.";
                    }
                    if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                    {
                        return "Invalid request to the AI API. The prompt may be too large or malformed.";
                    }

                    // Return a detailed debug string for the user
                    return $"AI API Error: {response.StatusCode}. Key starts with: {safeKeyStart}.... Details: {errorContent}";
                }

                var jsonDoc = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: ct);
                var textResponse = jsonDoc?
                    .RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString();

                return textResponse?.Trim() ?? "AI returned an empty response.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating AI forecast for risk {RiskId}", riskId);
                return "An error occurred while generating the AI forecast. Please try again later.";
            }
        }
    }
}
