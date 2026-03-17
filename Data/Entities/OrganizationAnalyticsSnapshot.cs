namespace WEB_Sentro.Data.Entities
{
    public class OrganizationAnalyticsSnapshot
    {
        public long SnapshotId { get; set; }
        public int OrganizationId { get; set; }
        public string RangeKey { get; set; } = "30d";
        public DateTime SnapshotAtUtc { get; set; } = DateTime.UtcNow;

        public string OrganizationName { get; set; } = string.Empty;
        public string PlanName { get; set; } = "-";
        public string SubscriptionStatus { get; set; } = "Unknown";

        public int UserCount { get; set; }
        public int? SeatLimit { get; set; }
        public int SeatUtilizationPercent { get; set; }
        public int LoginsInRange { get; set; }
        public int EventCountInRange { get; set; }
        public decimal ActivityChangePercent { get; set; }
        public decimal UserChangePercent { get; set; }
        public int FeatureAdoptionPercent { get; set; }
        public decimal AdoptionChangePercent { get; set; }
        public decimal ErrorRatePercent { get; set; }

        public DateTime? LastActivityAtUtc { get; set; }
        public int HealthScore { get; set; }
        public string ChurnRiskLabel { get; set; } = "Medium";
        public string SegmentLabel { get; set; } = "Stable";
        public string TrendLabel { get; set; } = "Stable";
        public string ActivityTrendJson { get; set; } = "[]";
        public string RenewalDateDisplay { get; set; } = "-";
    }
}
