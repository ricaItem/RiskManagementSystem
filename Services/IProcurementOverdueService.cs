namespace WEB_Sentro.Services
{
    public interface IProcurementOverdueService
    {
        /// <summary>Check overdue POs, create procurement alerts and create or escalate supplier delay risks. Call when opening Risk Monitoring (lazy).</summary>
        Task CheckOverduePurchaseOrdersAsync(int orgId, string userId, CancellationToken ct = default);
    }
}
