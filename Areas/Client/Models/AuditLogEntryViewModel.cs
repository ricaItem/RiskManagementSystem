namespace Web_Sentro.Areas.Client.Models
{
    public class AuditLogEntryViewModel
    {
        public int Id { get; set; }
        public string User { get; set; } = "";
        public string Action { get; set; } = "";
        public string Module { get; set; } = "";
        public string Details { get; set; } = "";
        public DateTime Timestamp { get; set; }
        public string? IpAddress { get; set; }
        public string Status { get; set; } = "Success";
    }
}
