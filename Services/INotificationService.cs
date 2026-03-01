namespace WEB_Sentro.Services
{
    public interface INotificationService
    {
        /// <summary>Notify recipients (risk owner + RiskManager/Admin in org) and log to audit.</summary>
        Task NotifyRiskEventAsync(int orgId, string eventType, int? riskId, string title, string message, string? reportByUserId, CancellationToken ct = default);
    }
}
