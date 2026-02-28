namespace WEB_Sentro.Data.Entities
{
    public class MonitoringAlert
    {
        public int AlertId { get; set; }
        public int OrgId { get; set; }
        public int MonitoringSiteId { get; set; }
        public string RuleCode { get; set; } = null!;
        public string RuleName { get; set; } = null!;
        public string? MeasuredValues { get; set; }
        public string Severity { get; set; } = null!; // High, Critical
        public DateTime TriggeredAt { get; set; }
        public int? RiskId { get; set; }
    }
}
