using Microsoft.AspNetCore.Identity;

namespace WEB_Sentro.Models.Identity
{
    public class ApplicationUser : IdentityUser
    {
        //Tenant 
        public int OrganizationId { get; set; }

        //Profile
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastLoginAt { get; set; }
    }
}
