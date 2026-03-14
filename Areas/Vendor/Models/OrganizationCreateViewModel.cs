using System.ComponentModel.DataAnnotations;

namespace WEB_Sentro.Areas.Vendor.Models
{
    public class OrganizationCreateViewModel
    {
        [Required]
        [Display(Name = "Organization Name")]
        public string OrgName { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Plan")]
        public string PlanName { get; set; } = "Basic";

        [Required]
        [EmailAddress]
        [Display(Name = "Admin Email")]
        public string AdminEmail { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Temporary Password")]
        public string AdminPassword { get; set; } = string.Empty;
    }
}
