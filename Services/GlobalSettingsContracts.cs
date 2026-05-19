namespace WEB_Sentro.Services;

public class RiskScoringDefaults
{
    public int LowMaxScore { get; set; } = 6;
    public int MediumMaxScore { get; set; } = 14;
    public int HighMaxScore { get; set; } = 19;
}

public class NotificationTemplateDefaults
{
    public string InvoiceDueSubject { get; set; } = "Invoice {{InvoiceNumber}} due on {{DueDate}}";
    public string InvoiceDueBody { get; set; } = "Hello {{OrgName}}, your invoice {{InvoiceNumber}} is due on {{DueDate}}. Amount due: {{AmountDue}}.";
    public string RenewalReminderSubject { get; set; } = "Subscription renews on {{RenewalDate}}";
    public string RenewalReminderBody { get; set; } = "Hello {{OrgName}}, your {{PlanName}} subscription renews on {{RenewalDate}}.";
    public string RiskAlertSubject { get; set; } = "{{RiskLevel}} risk alert: {{RiskTitle}}";
    public string RiskAlertBody { get; set; } = "A {{RiskLevel}} risk ({{RiskTitle}}) was recorded for {{OrgName}}. Score: {{RiskScore}}.";
}

public class SecurityPolicyDefaults
{
    public int SessionTimeoutMinutes { get; set; } = 60;
    public int PasswordMinLength { get; set; } = 12;
    public int LockoutMaxFailedAccessAttempts { get; set; } = 5;
    public int LockoutWindowMinutes { get; set; } = 15;
    public bool RequireUppercase { get; set; } = true;
    public bool RequireLowercase { get; set; } = true;
    public bool RequireDigit { get; set; } = true;
    public bool RequireNonAlphanumeric { get; set; } = true;
}
