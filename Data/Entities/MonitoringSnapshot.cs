namespace WEB_Sentro.Data.Entities
{
    public class MonitoringSnapshot
    {
        public int SnapshotId { get; set; }
        public int OrgId { get; set; }
        public int MonitoringSiteId { get; set; }
        public DateTime CapturedAtUtc { get; set; }
        public decimal? Temperature { get; set; }
        public decimal? WindSpeed { get; set; }
        public decimal? Humidity { get; set; }
        public decimal? RainMm { get; set; }
        public string? Condition { get; set; }
        public string? RawJson { get; set; }
    }
}
