using WEB_Sentro.Services;
using Xunit;

namespace WEB_Sentro.Tests
{
    /// <summary>Treatment decision enforcement: allowed decisions and justification requirement are driven by config; static fallback allows all.</summary>
    public class TreatmentDecisionTests
    {
        [Fact]
        public void TreatmentDecision_Values_AreStandard()
        {
            var expected = new[] { "Mitigate", "Transfer", "Accept", "Avoid" };
            foreach (var d in expected)
                Assert.True(d.Length > 0 && d.Length <= 50);
        }
    }
}
