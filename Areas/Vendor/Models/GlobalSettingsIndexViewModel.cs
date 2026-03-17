namespace WEB_Sentro.Areas.Vendor.Models;

public class GlobalSettingsIndexViewModel
{
    public string ActiveTab { get; set; } = "risk";

    public bool HasRiskScoringConfig { get; set; }
    public bool HasDefaultWorkflowConfig { get; set; }
    public bool HasNotificationTemplatesConfig { get; set; }
    public bool HasSecurityPoliciesConfig { get; set; }

    public RiskScoringSettingsForm RiskScoring { get; set; } = RiskScoringSettingsForm.CreateDefault();
    public DefaultWorkflowSettingsForm WorkflowDefaults { get; set; } = DefaultWorkflowSettingsForm.CreateDefault();
    public NotificationTemplateSettingsForm NotificationTemplates { get; set; } = NotificationTemplateSettingsForm.CreateDefault();
    public SecurityPolicySettingsForm SecurityPolicies { get; set; } = SecurityPolicySettingsForm.CreateDefault();
}

public class RiskScoringSettingsForm
{
    public string FormulaMode { get; set; } = "multiply";
    public int WeightedLikelihoodPercent { get; set; } = 50;
    public int WeightedImpactPercent { get; set; } = 50;

    public string LikelihoodLabel1 { get; set; } = "1 - Rare";
    public string LikelihoodLabel2 { get; set; } = "2 - Unlikely";
    public string LikelihoodLabel3 { get; set; } = "3 - Possible";
    public string LikelihoodLabel4 { get; set; } = "4 - Likely";
    public string LikelihoodLabel5 { get; set; } = "5 - Almost Certain";

    public string ImpactLabel1 { get; set; } = "1 - Negligible";
    public string ImpactLabel2 { get; set; } = "2 - Minor";
    public string ImpactLabel3 { get; set; } = "3 - Moderate";
    public string ImpactLabel4 { get; set; } = "4 - Major";
    public string ImpactLabel5 { get; set; } = "5 - Catastrophic";

    public int LowMaxScore { get; set; } = 6;
    public int MediumMaxScore { get; set; } = 14;
    public int HighMaxScore { get; set; } = 19;

    public static RiskScoringSettingsForm CreateDefault() => new();
}

public class DefaultWorkflowSettingsForm
{
    public bool RequireApprovalForHighRisk { get; set; } = true;
    public bool RequireApprovalForCriticalRisk { get; set; } = true;
    public int InitialResponseSlaHours { get; set; } = 24;
    public int EscalationAfterHours { get; set; } = 48;
    public bool AutoAssignToRole { get; set; } = true;
    public string DefaultAssigneeRole { get; set; } = "RiskManager";
    public bool NotifyOnEscalation { get; set; } = true;

    public static DefaultWorkflowSettingsForm CreateDefault() => new();
}

public class NotificationTemplateSettingsForm
{
    public string InvoiceDueSubject { get; set; } = "Invoice {{InvoiceNumber}} due on {{DueDate}}";
    public string InvoiceDueBody { get; set; } = "Hello {{OrgName}}, your invoice {{InvoiceNumber}} is due on {{DueDate}}. Amount due: {{AmountDue}}.";

    public string RenewalReminderSubject { get; set; } = "Subscription renews on {{RenewalDate}}";
    public string RenewalReminderBody { get; set; } = "Hello {{OrgName}}, your {{PlanName}} subscription renews on {{RenewalDate}}.";

    public string RiskAlertSubject { get; set; } = "{{RiskLevel}} risk alert: {{RiskTitle}}";
    public string RiskAlertBody { get; set; } = "A {{RiskLevel}} risk ({{RiskTitle}}) was recorded for {{OrgName}}. Score: {{RiskScore}}.";

    public static NotificationTemplateSettingsForm CreateDefault() => new();
}

public class SecurityPolicySettingsForm
{
    public int SessionTimeoutMinutes { get; set; } = 60;
    public int PasswordMinLength { get; set; } = 12;
    public int LockoutMaxFailedAccessAttempts { get; set; } = 5;
    public int LockoutWindowMinutes { get; set; } = 15;
    public bool RequireUppercase { get; set; } = true;
    public bool RequireLowercase { get; set; } = true;
    public bool RequireDigit { get; set; } = true;
    public bool RequireNonAlphanumeric { get; set; } = true;

    public static SecurityPolicySettingsForm CreateDefault() => new();
}
