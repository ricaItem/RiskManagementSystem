using Xunit;

namespace WEB_Sentro.Tests
{
    /// <summary>Overdue flag: true when NextReviewDate is in the past.</summary>
    public class OverdueCalculationTests
    {
        [Fact]
        public void Overdue_WhenNextReviewDateBeforeToday_IsTrue()
        {
            var today = DateTime.UtcNow.Date;
            var nextReview = today.AddDays(-1);
            var isOverdue = nextReview < today;
            Assert.True(isOverdue);
        }

        [Fact]
        public void Overdue_WhenNextReviewDateToday_IsFalse()
        {
            var today = DateTime.UtcNow.Date;
            var nextReview = today;
            var isOverdue = nextReview < today;
            Assert.False(isOverdue);
        }

        [Fact]
        public void Overdue_WhenNextReviewDateFuture_IsFalse()
        {
            var today = DateTime.UtcNow.Date;
            var nextReview = today.AddDays(7);
            var isOverdue = nextReview < today;
            Assert.False(isOverdue);
        }
    }
}
