using System.ComponentModel.DataAnnotations;

namespace WEB_Sentro.Areas.Client.Models
{
    public class OrganizationSettingsViewModel
    {
        public string ActiveTab { get; set; } = "profile";
        public bool CanEditOrganization { get; set; }
        public bool CanManageBilling { get; set; }

        public int OrganizationId { get; set; }
        public string OrgCode { get; set; } = string.Empty;
        public string OrgName { get; set; } = string.Empty;
        public string? Status { get; set; }

        public OrganizationProfileForm Profile { get; set; } = new();
        public OrganizationAddressForm Addresses { get; set; } = new();

        public CurrentSubscriptionViewModel? CurrentSubscription { get; set; }
        public List<PlanOptionViewModel> AvailablePlans { get; set; } = new();
        public List<InvoiceRowViewModel> Invoices { get; set; } = new();
        public List<PaymentRowViewModel> Payments { get; set; } = new();

        public List<OrgUserRowViewModel> Users { get; set; } = new();
        public List<AuditRowViewModel> AuditEntries { get; set; } = new();
    }

    public class OrganizationProfileForm
    {
        [Required]
        [StringLength(200)]
        public string OrgName { get; set; } = string.Empty;

        [StringLength(200)]
        public string? DisplayName { get; set; }

        [StringLength(200)]
        public string? Website { get; set; }

        [EmailAddress]
        [StringLength(256)]
        public string? PrimaryEmail { get; set; }

        [Phone]
        [StringLength(50)]
        public string? PrimaryPhone { get; set; }

        [StringLength(60)]
        public string? TaxId { get; set; }

        public string? LogoPath { get; set; }
    }

    public class OrganizationAddressForm
    {
        [StringLength(200)]
        public string? LegalAddressLine1 { get; set; }

        [StringLength(200)]
        public string? LegalAddressLine2 { get; set; }

        [StringLength(80)]
        public string? LegalCity { get; set; }

        [StringLength(80)]
        public string? LegalProvince { get; set; }

        [StringLength(20)]
        public string? LegalPostalCode { get; set; }

        [StringLength(80)]
        public string? LegalCountry { get; set; }

        [StringLength(200)]
        public string? BillingAddressLine1 { get; set; }

        [StringLength(200)]
        public string? BillingAddressLine2 { get; set; }

        [StringLength(80)]
        public string? BillingCity { get; set; }

        [StringLength(80)]
        public string? BillingProvince { get; set; }

        [StringLength(20)]
        public string? BillingPostalCode { get; set; }

        [StringLength(80)]
        public string? BillingCountry { get; set; }

        [StringLength(120)]
        public string? BillingContactName { get; set; }

        [EmailAddress]
        [StringLength(256)]
        public string? BillingEmail { get; set; }

        [Phone]
        [StringLength(50)]
        public string? BillingPhone { get; set; }
    }

    public class CurrentSubscriptionViewModel
    {
        public int SubscriptionId { get; set; }
        public int CurrentPlanId { get; set; }
        public string CurrentPlanName { get; set; } = string.Empty;
        public long CurrentPlanAmountCentavos { get; set; }
        public string Currency { get; set; } = "PHP";
        public string BillingInterval { get; set; } = "month";
        public string Status { get; set; } = "Active";
        public DateTime CurrentPeriodStart { get; set; }
        public DateTime CurrentPeriodEnd { get; set; }
        public int? PendingPlanId { get; set; }
        public string? PendingPlanName { get; set; }
        public string? PendingChangeType { get; set; }
        public DateTime? PendingChangeEffectiveAt { get; set; }
    }

    public class PlanOptionViewModel
    {
        public int PlanId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public long AmountCentavos { get; set; }
        public string Currency { get; set; } = "PHP";
        public string BillingInterval { get; set; } = "month";
        public bool IsCurrent { get; set; }
        public bool IsUpgrade { get; set; }
        public bool IsDowngrade { get; set; }
    }

    public class InvoiceRowViewModel
    {
        public int InvoiceId { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public long AmountDueCentavos { get; set; }
        public string Currency { get; set; } = "PHP";
        public DateTime DueDate { get; set; }
        public DateTime? PaidAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class PaymentRowViewModel
    {
        public int PaymentId { get; set; }
        public string Gateway { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public long AmountCentavos { get; set; }
        public string Currency { get; set; } = "PHP";
        public DateTime? PaidAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class OrgUserRowViewModel
    {
        public string UserId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? ProfileImagePath { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<string> Roles { get; set; } = new();
    }

    public class AuditRowViewModel
    {
        public DateTime CreatedAt { get; set; }
        public string ActionType { get; set; } = string.Empty;
        public string EntityType { get; set; } = string.Empty;
        public string? Level { get; set; }
        public string? Message { get; set; }
        public string? UserId { get; set; }
    }
}
