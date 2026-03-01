namespace WEB_Sentro.Data.Entities
{
    public class Control
    {
        public int ControlId { get; set; }
        public int OrgId { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public string? OwnerId { get; set; }
        public string? Frequency { get; set; }
        public string? Type { get; set; }
        public string Status { get; set; } = "Active";
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public ICollection<RiskControl> RiskControls { get; set; } = new List<RiskControl>();
    }
}
