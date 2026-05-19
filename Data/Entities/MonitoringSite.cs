namespace WEB_Sentro.Data.Entities
{
    public class MonitoringSite
    {
        public int MonitoringSiteId { get; set; }
        public int OrgId { get; set; }
        public int? SiteId { get; set; }
        public string Name { get; set; } = null!;
        public double Latitude { get; set; }
        public double Longitude { get; set; }

        public Site? Site { get; set; }
    }
}
