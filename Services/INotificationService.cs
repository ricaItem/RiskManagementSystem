namespace WEB_Sentro.Services
{
    public interface INotificationService
    {
        /// <summary>Notify recipients (risk owner + RiskManager/Admin in org) and log to audit.</summary>
        Task NotifyRiskEventAsync(int orgId, string eventType, int? riskId, string title, string message, string? reportByUserId, CancellationToken ct = default);

        /// <summary>Notify assignee and managers when a mitigation task is assigned or reassigned.</summary>
        Task NotifyMitigationTaskAssignmentAsync(int orgId, int riskId, int taskId, string taskTitle, string assignedToUserId, string assignedByUserId, CancellationToken ct = default);

        /// <summary>Notify admins/risk managers when a monitoring alert is raised.</summary>
        Task NotifyMonitoringAlertAsync(int orgId, int monitoringSiteId, string ruleName, string severity, string measuredValues, int? riskId, CancellationToken ct = default);
    }
}
