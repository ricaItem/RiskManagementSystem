namespace WEB_Sentro.Data.Entities
{
    public class Attachment
    {
        public int AttachmentId { get; set; }
        public int RiskId { get; set; }
        public int OrgId { get; set; }
        public string UploadedByUserId { get; set; } = null!;
        public string? FileName { get; set; }
        public string? FileRef { get; set; }
        public DateTime UploadedAt { get; set; }

        public Risk Risk { get; set; } = null!;
    }
}
