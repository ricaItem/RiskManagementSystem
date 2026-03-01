namespace WEB_Sentro.Data.Entities
{
    public class MonitoringRule
    {
        public int RuleId { get; set; }
        public int OrgId { get; set; }
        public string Name { get; set; } = null!;
        public string Metric { get; set; } = null!;
        public decimal Threshold { get; set; }
        public string Operator { get; set; } = ">";
        public string Severity { get; set; } = "High";
        public int CooldownMinutes { get; set; }
        public bool Enabled { get; set; } = true;
    }
}
