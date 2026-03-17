namespace WEB_Sentro.Data.Entities
{
    public class PlatformSetting
    {
        public int PlatformSettingId { get; set; }
        public string Key { get; set; } = null!;
        public string JsonValue { get; set; } = "{}";
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public string? UpdatedByUserId { get; set; }
    }
}
