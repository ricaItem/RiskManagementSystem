using Microsoft.EntityFrameworkCore;
using WEB_Sentro.Data;
using WEB_Sentro.Data.Entities;

namespace WEB_Sentro.Services
{
    public class SupplierRiskSummaryDto
    {
        public int SupplierId { get; set; }
        public int ReliabilityScore { get; set; }
        public string DeliveryTrend { get; set; } = "On-Time";
        public string FinancialStatus { get; set; } = "Stable";
    }

    public class SupplierRiskService
    {
        private readonly ITenantDbFactory _tenantDbFactory;

        public SupplierRiskService(ITenantDbFactory tenantDbFactory)
        {
            _tenantDbFactory = tenantDbFactory;
        }

        public async Task<Dictionary<int, SupplierRiskSummaryDto>> GetSupplierRiskSummariesAsync(int orgId, List<int> supplierIds, CancellationToken ct = default)
        {
            var result = new Dictionary<int, SupplierRiskSummaryDto>();
            foreach (var id in supplierIds)
            {
                result[id] = new SupplierRiskSummaryDto { SupplierId = id, ReliabilityScore = 100 };
            }

            if (supplierIds == null || !supplierIds.Any())
                return result;

            await using var db = await _tenantDbFactory.CreateAsync(orgId);
            var today = DateTime.UtcNow.Date;

            // 1. Batch fetch overdue Purchase Orders
            // Status != Received and != Cancelled
            // ExpectedDeliveryDate < Today
            var overduePos = await db.PurchaseOrders
                .AsNoTracking()
                .Where(p => p.OrgId == orgId && supplierIds.Contains(p.SupplierId)
                       && p.ExpectedDeliveryDate.HasValue
                       && p.ExpectedDeliveryDate.Value < today
                       && p.Status != "Received" && p.Status != "Cancelled")
                .Select(p => p.SupplierId)
                .ToListAsync(ct);

            var overduePoCounts = overduePos
                .GroupBy(id => id)
                .ToDictionary(g => g.Key, g => g.Count());

            // 2. Batch fetch active Risks
            // Status != Closed_Invalid, != Rejected
            // We need Category (for Financial) and Priority (for Score)
            var activeRisks = await db.Risks
                .AsNoTracking()
                .Where(r => r.OrgId == orgId && r.SupplierId.HasValue && supplierIds.Contains(r.SupplierId.Value)
                       && r.DeletedAt == null
                       && r.Status != "Closed_Invalid" && r.Status != "Rejected")
                .Select(r => new { r.SupplierId, r.Category, r.Priority })
                .ToListAsync(ct);

            var risksBySupplier = activeRisks
                .GroupBy(r => r.SupplierId!.Value)
                .ToDictionary(g => g.Key, g => g.ToList());

            // 3. Calculate metrics for each supplier
            foreach (var supplierId in supplierIds)
            {
                var summary = result[supplierId];

                // --- Delivery Trend ---
                // 0 overdue -> "On-Time"
                // 1+ overdue -> "Delayed"
                // 3+ overdue -> "Critical"
                var overdueCount = overduePoCounts.GetValueOrDefault(supplierId, 0);
                
                if (overdueCount >= 3)
                    summary.DeliveryTrend = "Critical";
                else if (overdueCount >= 1)
                    summary.DeliveryTrend = "Delayed";
                else
                    summary.DeliveryTrend = "On-Time";

                // --- Financial Status ---
                // Logic: Active Financial Risks. 0 -> "Stable", 1 -> "Warning", 2+ -> "Critical"
                var supplierRisks = risksBySupplier.GetValueOrDefault(supplierId) ?? new();
                
                // Note: Category matching should be somewhat flexible or exact based on data. 
                // Assuming "Financial" is the category string used in the CreateRisk modal.
                var financialRiskCount = supplierRisks.Count(r => 
                    !string.IsNullOrEmpty(r.Category) && 
                    r.Category.Contains("Financial", StringComparison.OrdinalIgnoreCase));
                
                if (financialRiskCount >= 2)
                    summary.FinancialStatus = "Critical";
                else if (financialRiskCount >= 1)
                    summary.FinancialStatus = "Warning";
                else
                    summary.FinancialStatus = "Stable";

                // --- Reliability Score ---
                // Start 100.
                // Deduct:
                // -20 per High/Critical Risk
                // -10 per Medium Risk
                // -5 per overdue PO
                // Max(score, 0)
                int score = 100;
                
                var highCriticalCount = supplierRisks.Count(r => 
                    string.Equals(r.Priority, "High", StringComparison.OrdinalIgnoreCase) || 
                    string.Equals(r.Priority, "Critical", StringComparison.OrdinalIgnoreCase));
                
                var mediumCount = supplierRisks.Count(r => 
                    string.Equals(r.Priority, "Medium", StringComparison.OrdinalIgnoreCase));

                score -= (highCriticalCount * 20);
                score -= (mediumCount * 10);
                score -= (overdueCount * 5);

                summary.ReliabilityScore = Math.Max(score, 0);
            }

            return result;
        }
    }
}
