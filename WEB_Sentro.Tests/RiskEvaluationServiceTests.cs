using WEB_Sentro.Services;
using Xunit;

namespace WEB_Sentro.Tests
{
    /// <summary>Tests for matrix evaluation (score and band).</summary>
    public class RiskEvaluationServiceTests
    {
        [Theory]
        [InlineData(1, 1, 1)]
        [InlineData(2, 3, 6)]
        [InlineData(5, 5, 25)]
        [InlineData(3, 4, 12)]
        public void ComputeRiskScore_Returns_LikelihoodTimesImpact(int likelihood, int impact, int expected)
        {
            var score = RiskEvaluationService.ComputeRiskScore(likelihood, impact);
            Assert.Equal(expected, score);
        }

        [Theory]
        [InlineData(1, "Low")]
        [InlineData(6, "Low")]
        [InlineData(7, "Medium")]
        [InlineData(14, "Medium")]
        [InlineData(15, "High")]
        [InlineData(19, "High")]
        [InlineData(20, "Critical")]
        [InlineData(25, "Critical")]
        public void RiskLevelFromScore_Returns_CorrectBand(int score, string expectedBand)
        {
            var band = RiskEvaluationService.RiskLevelFromScore(score);
            Assert.Equal(expectedBand, band);
        }
    }
}
