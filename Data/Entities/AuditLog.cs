namespace WEB_Sentro.Data.Entities
{
    public class AuditLog
    {
        public int AuditId { get; set; }
        public int OrgId { get; set; }
        public string UserId { get; set; } = null!;
        public string EntityType { get; set; } = null!;
        public int EntityId { get; set; }
        public string ActionType { get; set; } = null!;
        public string? Level { get; set; }
        public string? Message { get; set; }
        public string? IpAddress { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
