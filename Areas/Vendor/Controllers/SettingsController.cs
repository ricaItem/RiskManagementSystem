using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WEB_Sentro.Areas.Vendor.Models;
using WEB_Sentro.Services;
using System.Security.Claims;

namespace WEB_Sentro.Areas.Vendor.Controllers
{
    [Area("Vendor")]
    [Authorize(Policy = "SuperAdminOnly")]
    public class SettingsController : Controller
    {
        private readonly IGlobalSettingsService _globalSettings;

        public SettingsController(IGlobalSettingsService globalSettings)
        {
            _globalSettings = globalSettings;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string tab = "risk", CancellationToken ct = default)
        {
            return View(await BuildViewModelAsync(tab, ct));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveRiskScoring([Bind(Prefix = "RiskScoring")] RiskScoringSettingsForm form, CancellationToken ct = default)
        {
            ValidateRiskScoring(form);
            if (!ModelState.IsValid)
            {
                TempData["ToastError"] = "Please correct the risk scoring fields and try again.";
                return View("Index", await BuildViewModelAsync("risk", ct, form));
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            await _globalSettings.SetAsync(GlobalSettingKeys.RiskScoring, form, userId, ct);

            TempData["ToastSuccess"] = "Risk scoring settings saved. These defaults will be used for new organizations.";
            return RedirectToAction(nameof(Index), new { tab = "risk" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveWorkflows([Bind(Prefix = "WorkflowDefaults")] DefaultWorkflowSettingsForm form, CancellationToken ct = default)
        {
            ValidateWorkflows(form);
            if (!ModelState.IsValid)
            {
                TempData["ToastError"] = "Please correct the workflow default fields and try again.";
                return View("Index", await BuildViewModelAsync("workflows", ct, null, form));
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            await _globalSettings.SetAsync(GlobalSettingKeys.DefaultWorkflows, form, userId, ct);

            TempData["ToastSuccess"] = "Default workflow settings saved. These defaults will be used for new organizations.";
            return RedirectToAction(nameof(Index), new { tab = "workflows" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveNotificationTemplates([Bind(Prefix = "NotificationTemplates")] NotificationTemplateSettingsForm form, CancellationToken ct = default)
        {
            ValidateNotificationTemplates(form);
            if (!ModelState.IsValid)
            {
                TempData["ToastError"] = "Please correct the notification template fields and try again.";
                return View("Index", await BuildViewModelAsync("notifications", ct, null, null, form));
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            await _globalSettings.SetAsync(GlobalSettingKeys.NotificationTemplates, form, userId, ct);

            TempData["ToastSuccess"] = "Notification templates saved. These defaults will be used for new organizations.";
            return RedirectToAction(nameof(Index), new { tab = "notifications" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveSecurityPolicies([Bind(Prefix = "SecurityPolicies")] SecurityPolicySettingsForm form, CancellationToken ct = default)
        {
            ValidateSecurityPolicies(form);
            if (!ModelState.IsValid)
            {
                TempData["ToastError"] = "Please correct the security policy fields and try again.";
                return View("Index", await BuildViewModelAsync("security", ct, null, null, null, form));
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            await _globalSettings.SetAsync(GlobalSettingKeys.SecurityPolicies, form, userId, ct);

            TempData["ToastSuccess"] = "Security policy defaults saved. These defaults will be used for new organizations.";
            return RedirectToAction(nameof(Index), new { tab = "security" });
        }

        private async Task<GlobalSettingsIndexViewModel> BuildViewModelAsync(
            string tab,
            CancellationToken ct,
            RiskScoringSettingsForm? riskOverride = null,
            DefaultWorkflowSettingsForm? workflowOverride = null,
            NotificationTemplateSettingsForm? notificationOverride = null,
            SecurityPolicySettingsForm? securityOverride = null)
        {
            var risk = riskOverride
                ?? await _globalSettings.GetAsync<RiskScoringSettingsForm>(GlobalSettingKeys.RiskScoring, ct)
                ?? RiskScoringSettingsForm.CreateDefault();

            var workflows = workflowOverride
                ?? await _globalSettings.GetAsync<DefaultWorkflowSettingsForm>(GlobalSettingKeys.DefaultWorkflows, ct)
                ?? DefaultWorkflowSettingsForm.CreateDefault();

            var notifications = notificationOverride
                ?? await _globalSettings.GetAsync<NotificationTemplateSettingsForm>(GlobalSettingKeys.NotificationTemplates, ct)
                ?? NotificationTemplateSettingsForm.CreateDefault();

            var security = securityOverride
                ?? await _globalSettings.GetAsync<SecurityPolicySettingsForm>(GlobalSettingKeys.SecurityPolicies, ct)
                ?? SecurityPolicySettingsForm.CreateDefault();

            return new GlobalSettingsIndexViewModel
            {
                ActiveTab = NormalizeTab(tab),
                HasRiskScoringConfig = await _globalSettings.ExistsAsync(GlobalSettingKeys.RiskScoring, ct),
                HasDefaultWorkflowConfig = await _globalSettings.ExistsAsync(GlobalSettingKeys.DefaultWorkflows, ct),
                HasNotificationTemplatesConfig = await _globalSettings.ExistsAsync(GlobalSettingKeys.NotificationTemplates, ct),
                HasSecurityPoliciesConfig = await _globalSettings.ExistsAsync(GlobalSettingKeys.SecurityPolicies, ct),
                RiskScoring = risk,
                WorkflowDefaults = workflows,
                NotificationTemplates = notifications,
                SecurityPolicies = security
            };
        }

        private void ValidateRiskScoring(RiskScoringSettingsForm form)
        {
            if (form.FormulaMode != "multiply" && form.FormulaMode != "weighted")
            {
                ModelState.AddModelError("RiskScoring.FormulaMode", "Formula mode must be multiply or weighted.");
            }

            if (form.FormulaMode == "weighted")
            {
                if (form.WeightedLikelihoodPercent < 0 || form.WeightedLikelihoodPercent > 100)
                    ModelState.AddModelError("RiskScoring.WeightedLikelihoodPercent", "Likelihood weight must be between 0 and 100.");

                if (form.WeightedImpactPercent < 0 || form.WeightedImpactPercent > 100)
                    ModelState.AddModelError("RiskScoring.WeightedImpactPercent", "Impact weight must be between 0 and 100.");

                if (form.WeightedLikelihoodPercent + form.WeightedImpactPercent != 100)
                    ModelState.AddModelError("RiskScoring.WeightedImpactPercent", "Weighted formula requires totals to equal 100%.");
            }

            if (string.IsNullOrWhiteSpace(form.LikelihoodLabel1) || string.IsNullOrWhiteSpace(form.LikelihoodLabel2) ||
                string.IsNullOrWhiteSpace(form.LikelihoodLabel3) || string.IsNullOrWhiteSpace(form.LikelihoodLabel4) ||
                string.IsNullOrWhiteSpace(form.LikelihoodLabel5))
            {
                ModelState.AddModelError("RiskScoring.LikelihoodLabel1", "All likelihood labels are required.");
            }

            if (string.IsNullOrWhiteSpace(form.ImpactLabel1) || string.IsNullOrWhiteSpace(form.ImpactLabel2) ||
                string.IsNullOrWhiteSpace(form.ImpactLabel3) || string.IsNullOrWhiteSpace(form.ImpactLabel4) ||
                string.IsNullOrWhiteSpace(form.ImpactLabel5))
            {
                ModelState.AddModelError("RiskScoring.ImpactLabel1", "All impact labels are required.");
            }

            if (form.LowMaxScore < 1 || form.HighMaxScore > 24)
            {
                ModelState.AddModelError("RiskScoring.LowMaxScore", "Band thresholds must be between 1 and 24.");
            }

            if (!(form.LowMaxScore < form.MediumMaxScore && form.MediumMaxScore < form.HighMaxScore))
            {
                ModelState.AddModelError("RiskScoring.MediumMaxScore", "Thresholds must be ordered Low < Medium < High.");
            }
        }

        private void ValidateWorkflows(DefaultWorkflowSettingsForm form)
        {
            if (form.InitialResponseSlaHours < 1 || form.InitialResponseSlaHours > 720)
            {
                ModelState.AddModelError("WorkflowDefaults.InitialResponseSlaHours", "Initial response SLA must be between 1 and 720 hours.");
            }

            if (form.EscalationAfterHours < 1 || form.EscalationAfterHours > 1440)
            {
                ModelState.AddModelError("WorkflowDefaults.EscalationAfterHours", "Escalation threshold must be between 1 and 1440 hours.");
            }

            if (form.EscalationAfterHours <= form.InitialResponseSlaHours)
            {
                ModelState.AddModelError("WorkflowDefaults.EscalationAfterHours", "Escalation threshold must be greater than initial response SLA.");
            }

            if (form.AutoAssignToRole && string.IsNullOrWhiteSpace(form.DefaultAssigneeRole))
            {
                ModelState.AddModelError("WorkflowDefaults.DefaultAssigneeRole", "Default assignee role is required when auto-assign is enabled.");
            }

            var allowedRoles = new[] { "RiskManager", "Admin", "ProcurementOfficer", "Employee" };
            if (!string.IsNullOrWhiteSpace(form.DefaultAssigneeRole) && !allowedRoles.Contains(form.DefaultAssigneeRole))
            {
                ModelState.AddModelError("WorkflowDefaults.DefaultAssigneeRole", "Default assignee role is invalid.");
            }
        }

        private void ValidateNotificationTemplates(NotificationTemplateSettingsForm form)
        {
            ValidateTemplate("NotificationTemplates.InvoiceDueSubject", form.InvoiceDueSubject, "Invoice due subject", "{{InvoiceNumber}}", "{{DueDate}}");
            ValidateTemplate("NotificationTemplates.InvoiceDueBody", form.InvoiceDueBody, "Invoice due body", "{{OrgName}}", "{{InvoiceNumber}}", "{{DueDate}}", "{{AmountDue}}");

            ValidateTemplate("NotificationTemplates.RenewalReminderSubject", form.RenewalReminderSubject, "Renewal reminder subject", "{{RenewalDate}}");
            ValidateTemplate("NotificationTemplates.RenewalReminderBody", form.RenewalReminderBody, "Renewal reminder body", "{{OrgName}}", "{{PlanName}}", "{{RenewalDate}}");

            ValidateTemplate("NotificationTemplates.RiskAlertSubject", form.RiskAlertSubject, "Risk alert subject", "{{RiskLevel}}", "{{RiskTitle}}");
            ValidateTemplate("NotificationTemplates.RiskAlertBody", form.RiskAlertBody, "Risk alert body", "{{OrgName}}", "{{RiskLevel}}", "{{RiskTitle}}", "{{RiskScore}}");
        }

        private void ValidateTemplate(string key, string value, string label, params string[] requiredTokens)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                ModelState.AddModelError(key, label + " is required.");
                return;
            }

            if (value.Length > 2000)
            {
                ModelState.AddModelError(key, label + " must be 2000 characters or less.");
            }

            foreach (var token in requiredTokens)
            {
                if (!value.Contains(token, StringComparison.Ordinal))
                {
                    ModelState.AddModelError(key, label + " must include token " + token + ".");
                }
            }
        }

        private void ValidateSecurityPolicies(SecurityPolicySettingsForm form)
        {
            if (form.SessionTimeoutMinutes < 5 || form.SessionTimeoutMinutes > 480)
            {
                ModelState.AddModelError("SecurityPolicies.SessionTimeoutMinutes", "Session timeout must be between 5 and 480 minutes.");
            }

            if (form.PasswordMinLength < 8 || form.PasswordMinLength > 64)
            {
                ModelState.AddModelError("SecurityPolicies.PasswordMinLength", "Password minimum length must be between 8 and 64.");
            }

            if (form.LockoutMaxFailedAccessAttempts < 3 || form.LockoutMaxFailedAccessAttempts > 20)
            {
                ModelState.AddModelError("SecurityPolicies.LockoutMaxFailedAccessAttempts", "Max failed access attempts must be between 3 and 20.");
            }

            if (form.LockoutWindowMinutes < 1 || form.LockoutWindowMinutes > 1440)
            {
                ModelState.AddModelError("SecurityPolicies.LockoutWindowMinutes", "Lockout window must be between 1 and 1440 minutes.");
            }

            if (!form.RequireUppercase && !form.RequireLowercase && !form.RequireDigit && !form.RequireNonAlphanumeric)
            {
                ModelState.AddModelError("SecurityPolicies.RequireUppercase", "Enable at least one password complexity rule.");
            }
        }

        private static string NormalizeTab(string? tab)
        {
            var value = (tab ?? string.Empty).Trim().ToLowerInvariant();
            return value switch
            {
                "risk" => "risk",
                "workflows" => "workflows",
                "notifications" => "notifications",
                "security" => "security",
                _ => "risk"
            };
        }
    }
}
