namespace WEB_Sentro.Data.Entities
{
    public class EmployeeNote
    {
        public int EmployeeNoteId { get; set; }
        public int OrgId { get; set; }
        public string UserId { get; set; } = null!;
        public string Title { get; set; } = null!;
        public string Body { get; set; } = null!;
        public bool Pinned { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
