namespace WEB_Sentro.Data.Entities
{
    /// <summary>Alerts for procurement events (e.g. PO overdue). Separate from site-based MonitoringAlerts.</summary>
    public class ProcurementAlert
    {
        public int AlertId { get; set; }
        public int OrgId { get; set; }
        public int PurchaseOrderId { get; set; }
        public int SupplierId { get; set; }
        public string AlertCode { get; set; } = "PO_OVERDUE";
        public string Message { get; set; } = null!;
        public string Severity { get; set; } = "High";
        public string Status { get; set; } = "Active";
        public DateTime TriggeredAt { get; set; }
        public int? RiskId { get; set; }

        public PurchaseOrder PurchaseOrder { get; set; } = null!;
        public Supplier Supplier { get; set; } = null!;
        public Risk? Risk { get; set; }
    }
}
