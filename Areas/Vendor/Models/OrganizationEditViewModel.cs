using System.ComponentModel.DataAnnotations;

namespace WEB_Sentro.Areas.Vendor.Models
{
    public class OrganizationEditViewModel
    {
        public int OrganizationId { get; set; }

        [Required]
        [Display(Name = "Organization Name")]
        public string OrgName { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Code")]
        public string OrgCode { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Plan")]
        public string PlanName { get; set; } = "Basic";

        [Required]
        [Display(Name = "Status")]
        public string Status { get; set; } = "Active";

        [Display(Name = "Website")]
        public string? Website { get; set; }

        [Display(Name = "Primary Email")]
        [EmailAddress]
        public string? PrimaryEmail { get; set; }

        [Display(Name = "Phone")]
        public string? PrimaryPhone { get; set; }

        [Display(Name = "Address")]
        public string? AddressLine { get; set; }

        [Display(Name = "City")]
        public string? City { get; set; }

        [Display(Name = "Country")]
        public string? Country { get; set; }

        [Display(Name = "Tax ID")]
        public string? TaxId { get; set; }
    }
}
