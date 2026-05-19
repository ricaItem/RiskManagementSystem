namespace WEB_Sentro.Data.Entities
{
    public class Organization
    {
        public int OrganizationId { get; set; }
        public string OrgCode { get; set; } = null!;
        public string OrgName { get; set; } = null!;
        public string? DisplayName { get; set; }
        public string? LogoPath { get; set; }
        public string? Website { get; set; }
        public string? AddressLine { get; set; }
        public string? City { get; set; }
        public string? Province { get; set; }
        public string? Country { get; set; }
        public string? PrimaryEmail { get; set; }
        public string? PrimaryPhone { get; set; }
        public string? TaxId { get; set; }

        public string? LegalAddressLine1 { get; set; }
        public string? LegalAddressLine2 { get; set; }
        public string? LegalCity { get; set; }
        public string? LegalProvince { get; set; }
        public string? LegalPostalCode { get; set; }
        public string? LegalCountry { get; set; }

        public string? BillingAddressLine1 { get; set; }
        public string? BillingAddressLine2 { get; set; }
        public string? BillingCity { get; set; }
        public string? BillingProvince { get; set; }
        public string? BillingPostalCode { get; set; }
        public string? BillingCountry { get; set; }
        public string? BillingContactName { get; set; }
        public string? BillingEmail { get; set; }
        public string? BillingPhone { get; set; }

        public string PlanName { get; set; } = "Basic";
        public string Status { get; set; } = "Active";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public string? CreatedByUserId { get; set; }
    }
}
