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

            // 1. First, clean up any alerts for POs that are no longer overdue or have been Drafted/Cancelled/Received
            var activeAlerts = await db.ProcurementAlerts
                .Where(a => a.OrgId == orgId && a.AlertCode == "PO_OVERDUE" && a.Status == "Active" && a.PurchaseOrderId != null)
                .Include(a => a.PurchaseOrder)
                .Include(a => a.Risk)
                .ToListAsync(ct);

            foreach (var existingAlert in activeAlerts)
            {
                var po = existingAlert.PurchaseOrder;
                // If PO is deleted, not 'Sent', or expected delivery is moved to future, resolve the alert
                if (po == null || po.Status != "Sent" || !po.ExpectedDeliveryDate.HasValue || po.ExpectedDeliveryDate.Value >= today)
                {
                    existingAlert.Status = "Resolved";
                    // Note: 'ResolvedAt' property does not currently exist on ProcurementAlert entity, omitted.

                    if (existingAlert.Risk != null && existingAlert.Risk.Status != "Closed_Controlled" && existingAlert.Risk.Status != "Closed_Invalid" && existingAlert.Risk.Status != "Rejected")
                    {
                        // Check if this risk is linked to other active alerts, if not we can close it
                        var otherActiveAlertsForRisk = await db.ProcurementAlerts
                            .Where(a => a.OrgId == orgId && a.RiskId == existingAlert.RiskId && a.Status == "Active" && a.AlertId != existingAlert.AlertId)
                            .AnyAsync(ct);

                        if (!otherActiveAlertsForRisk)
                        {
                            existingAlert.Risk.Status = "Closed_Controlled";
                            existingAlert.Risk.UpdatedAt = DateTime.UtcNow;
                            existingAlert.Risk.OverdueFlag = false;
                            _riskService.AddAuditLog(db, orgId, userId, "Risk", existingAlert.Risk.RiskId, "AutoClosed", $"Supplier delay risk auto-closed as PO {po?.OrderNumber ?? "Unknown"} is no longer overdue.", null);
                        }
                    }
                }
            }
            await db.SaveChangesAsync(ct);

            // 2. Identify new overdue POs. Only consider POs that are actively 'Sent' to the supplier.
            var overduePos = await db.PurchaseOrders
                .Include(p => p.Supplier)
                .Where(p => p.OrgId == orgId
                    && p.ExpectedDeliveryDate.HasValue
                    && p.ExpectedDeliveryDate.Value < today
                    && p.Status == "Sent")
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
                            projectId: null,
                            ct: ct);
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
