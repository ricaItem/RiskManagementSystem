namespace WEB_Sentro.Data.Entities
{
    /// <summary>Many-to-many: risk &lt;-&gt; control with optional notes.</summary>
    public class RiskControl
    {
        public int RiskControlId { get; set; }
        public int RiskId { get; set; }
        public int ControlId { get; set; }
        public string? Notes { get; set; }
        public DateTime LinkedAt { get; set; }

        public Risk Risk { get; set; } = null!;
        public Control Control { get; set; } = null!;
    }
}
