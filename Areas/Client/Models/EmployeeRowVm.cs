namespace WEB_Sentro.Areas.Client.Models
{
    public class EmployeeRowVm
    {
        public string UserId { get; set; } = "";
        public string Name { get; set; } = "";
        public string Email { get; set; } = "";
        public string? ProfileImagePath { get; set; }
        public string Role { get; set; } = "";
        public string Status { get; set; } = "";
        public int OrganizationId { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
