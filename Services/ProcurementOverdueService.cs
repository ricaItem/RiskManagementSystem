using Microsoft.EntityFrameworkCore;
using WEB_Sentro.Data;
using WEB_Sentro.Data.Entities;

namespace WEB_Sentro.Services
{
    public class ProcurementOverdueService : IProcurementOverdueService
    {
        private readonly ITenantDbFactory _tenantDbFactory;
        private readonly RiskService _riskService;

        public ProcurementOverdueService(ITenantDbFactory tenantDbFactory, RiskService riskService)
        {
            _tenantDbFactory = tenantDbFactory;
            _riskService = riskService;
        }

        public async Task CheckOverduePurchaseOrdersAsync(int orgId, string userId, CancellationToken ct = default)
        {
            var today = DateTime.UtcNow.Date;
            await using var db = await _tenantDbFactory.CreateAsync(orgId);

            var overduePos = await db.PurchaseOrders
                .Include(p => p.Supplier)
                .Where(p => p.OrgId == orgId
                    && p.ExpectedDeliveryDate.HasValue
                    && p.ExpectedDeliveryDate.Value < today
                    && p.Status != "Received"
                    && p.Status != "Cancelled")
                .ToListAsync(ct);

            var existingAlertPoIdsList = await db.ProcurementAlerts
                .Where(a => a.OrgId == orgId && a.AlertCode == "PO_OVERDUE" && a.Status == "Active")
                .Select(a => a.PurchaseOrderId)
                .ToListAsync(ct);
            var existingAlertPoIds = new HashSet<int>(existingAlertPoIdsList);

            foreach (var po in overduePos)
            {
                var daysOverdue = (int)(today - po.ExpectedDeliveryDate!.Value).TotalDays;
                var message = $"PO {po.OrderNumber} – {po.Supplier?.Name ?? "Supplier"}: {daysOverdue} day(s) overdue.";

                if (!existingAlertPoIds.Contains(po.PurchaseOrderId))
                {
                    var alert = new ProcurementAlert
                    {
                        OrgId = orgId,
                        PurchaseOrderId = po.PurchaseOrderId,
                        SupplierId = po.SupplierId,
                        AlertCode = "PO_OVERDUE",
                        Message = message.Length > 500 ? message.Substring(0, 500) : message,
                        Severity = "High",
                        Status = "Active",
                        TriggeredAt = DateTime.UtcNow
                    };
                    db.ProcurementAlerts.Add(alert);
                    await db.SaveChangesAsync(ct);

                    var existingDelayRisk = await db.Risks
                        .Where(r => r.OrgId == orgId && r.SupplierId == po.SupplierId && r.SourceType == "Supplier" && r.DeletedAt == null
                            && (r.Title != null && r.Title.Contains("Delay") || r.Category == "Delivery"))
                        .OrderByDescending(r => r.CreatedAt)
                        .FirstOrDefaultAsync(ct);

                    if (existingDelayRisk != null)
                    {
                        existingDelayRisk.OverdueFlag = true;
                        existingDelayRisk.NextReviewDate = today;
                        existingDelayRisk.UpdatedAt = DateTime.UtcNow;
                        alert.RiskId = existingDelayRisk.RiskId;
                        await db.SaveChangesAsync(ct);
                    }
                    else
                    {
                        var supplierName = po.Supplier?.Name ?? "Unknown";
                        var title = $"Supplier Delay Risk – {supplierName}";
                        var risk = await _riskService.CreateRiskAsync(
                            orgId,
                            userId,
                            title,
                            "Delivery",
                            "Supplier",
                            null,
                            message,
                            "Submitted",
                            siteId: null,
                            supplierId: po.SupplierId,
                            ct);
                        alert.RiskId = risk.RiskId;
                        await db.SaveChangesAsync(ct);
                        _riskService.AddAuditLog(db, orgId, userId, "Risk", risk.RiskId, "AutoCreatedFromOverduePO", $"Supplier delay risk created from overdue PO {po.OrderNumber}", null);
                        await _riskService.SaveChangesAsync(db, ct);
                    }
                    existingAlertPoIds.Add(po.PurchaseOrderId);
                }
            }
        }
    }
}
