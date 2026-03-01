namespace WEB_Sentro.Data.Entities
{
    public class MonitoringAlert
    {
        public int AlertId { get; set; }
        public int OrgId { get; set; }
        public int MonitoringSiteId { get; set; }
        public int? RuleId { get; set; }
        public string RuleCode { get; set; } = null!;
        public string RuleName { get; set; } = null!;
        public string? MeasuredValues { get; set; }
        public string Severity { get; set; } = null!;
        public string Status { get; set; } = "Active";
        public DateTime TriggeredAt { get; set; }
        public DateTime? ResolvedAtUtc { get; set; }
        public DateTime? AcknowledgedAtUtc { get; set; }
        public string? AcknowledgedByUserId { get; set; }
        public int? RiskId { get; set; }
    }
}
