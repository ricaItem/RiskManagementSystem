using WEB_Sentro.Services;
using Xunit;

namespace WEB_Sentro.Tests
{
    /// <summary>Matrix band fallback matches static RiskLevelFromScore (default 5x5).</summary>
    public class RiskMatrixServiceTests
    {
        [Fact]
        public void DefaultBandThresholds_MatchStaticRiskLevelFromScore()
        {
            Assert.Equal("Low", RiskEvaluationService.RiskLevelFromScore(6));
            Assert.Equal("Medium", RiskEvaluationService.RiskLevelFromScore(7));
            Assert.Equal("High", RiskEvaluationService.RiskLevelFromScore(15));
            Assert.Equal("Critical", RiskEvaluationService.RiskLevelFromScore(20));
        }
    }
}
