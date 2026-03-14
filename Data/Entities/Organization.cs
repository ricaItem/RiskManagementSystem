namespace WEB_Sentro.Data.Entities
{
    public class Organization
    {
        public int OrganizationId { get; set; }
        public string OrgCode { get; set; } = null!;
        public string OrgName { get; set; } = null!;
        public string? AddressLine { get; set; }
        public string? City { get; set; }
        public string? Province { get; set; }
        public string? Country { get; set; }
        public string? PrimaryEmail { get; set; }
        public string? PrimaryPhone { get; set; }
        public string PlanName { get; set; } = "Basic";
        public string Status { get; set; } = "Active";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public string? CreatedByUserId { get; set; }
    }
}
