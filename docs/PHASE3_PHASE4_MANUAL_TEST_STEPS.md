# Phase 3 & 4 – Manual Test Steps

Click-by-click verification for Supplier Risk integration and PO overdue monitoring.

---

## Prerequisites

1. Apply the tenant migration: **SupplierRiskAndPOOverdue** (adds Supplier risk columns, Risk.SupplierId, PurchaseOrder.ExpectedDeliveryDate, ProcurementAlerts table).
2. Ensure at least one **Site** and one **Supplier** exist. Optionally set a supplier’s **ReliabilityScore** below 60 (e.g. via DB or a future edit screen) to test the PO warning.

---

## Phase 3: Supplier Risk (DB + Create Risk)

### 3.1 Supplier Risk Registry reads from DB

1. Go to **Supplier & Procurement** → **Supplier Risk** (sidebar).
2. **Expected:** List shows **Suppliers** from the database (same as in **Suppliers**), with **ReliabilityScore**, **FinancialStatus**, **DeliveryTrend**, **ContractValue** on each card. No hardcoded “Global Steel Co.” etc. unless they exist as real Supplier records.
3. Use **Search** and **Resource / Financial** filters; confirm list updates.

### 3.2 Add Supplier creates a real record

1. On Supplier Risk Registry, click **Add Supplier**.
2. Fill: Legal Company Name, Resource Category, Contract Value (₱). Submit.
3. **Expected:** Message like “Supplier registered. New suppliers start with 50% Reliability Score.”
4. Go to **Suppliers** (sidebar). **Expected:** The new supplier appears in the list.
5. Return to **Supplier Risk**. **Expected:** The new supplier appears there with ReliabilityScore 50, FinancialStatus Stable, DeliveryTrend On-Time.

### 3.3 View History / Audit

1. On a supplier card, click **View History**.
2. **Expected:** **Supplier Audit Trail** with that supplier’s name. If there are no audit log entries, you see at least “Initial Onboarding” (or similar). Total Audits / Positive / Critical / Avg Impact show (placeholder or real counts).

### 3.4 Create Risk from Supplier

1. On Supplier Risk Registry, pick a supplier card and click **Create Risk**.
2. **Expected:** Redirect to **Risk Assessment** for a new risk with:
   - Title like **Supplier Risk – {SupplierName}**
   - SourceType = Supplier, linked to that supplier.
3. Complete assessment (likelihood/impact) and save if desired.
4. In **Risks** → **Identification**, find the new risk. **Expected:** It shows and has **SourceType** “Supplier” (and in DB, SupplierId set).

### 3.5 PO Create – supplier risk warning

1. Ensure at least one supplier has **ReliabilityScore &lt; 60** (e.g. update in DB: `UPDATE Suppliers SET ReliabilityScore = 50 WHERE SupplierId = X`).
2. Go to **Purchase Orders** → **New Purchase Order**.
3. Select **Site**, then in **Supplier** choose the low-score supplier.
4. **Expected:** An amber warning appears: “This supplier has elevated risk (score X). Consider mitigation or choose alternative supplier.” (X = that supplier’s score.)
5. Select a supplier with score ≥ 60. **Expected:** Warning hides.
6. Create the PO (with optional **Expected delivery date**). **Expected:** PO saves; warning does not block.

---

## Phase 4: PO Overdue Monitoring

### 4.1 Expected delivery date on PO

1. **Create PO:** **Purchase Orders** → **New Purchase Order**. Fill Site, Supplier, Order number, **Expected delivery date** (e.g. a past date like yesterday). Save.
2. **Expected:** PO is created; on **PO Details**, **Expected delivery** shows the chosen date.
3. **Edit PO:** Open the same PO → **Edit**. Change **Expected delivery date** or clear it. Save. **Expected:** Details page shows the updated value or “—”.

### 4.2 Overdue detection (lazy on Monitoring load)

1. Create (or use) a PO with **Expected delivery date** in the past and **Status** not **Received** or **Cancelled** (e.g. Draft or Sent).
2. Go to **Risk Monitoring Hub** (e.g. **Risks** → **Monitoring** or the monitoring entry in the menu).
3. **Expected:** Page loads. In the background, overdue check runs:
   - A **ProcurementAlert** is created for that PO (AlertCode `PO_OVERDUE`, Message includes PO number, supplier name, days overdue).
   - Either a **new Risk** “Supplier Delay Risk – {SupplierName}” is created (SourceType = Supplier, Category = Delivery) and linked to the alert, or an **existing** supplier delay risk for that supplier is found and **escalated** (OverdueFlag, NextReviewDate) and the alert linked to it.
4. In **Risks** → **Identification**, filter or search for “Delay” or “Supplier”. **Expected:** The new or updated supplier delay risk appears.
5. (Optional) In the tenant DB, check **ProcurementAlerts**: row(s) with AlertCode = `PO_OVERDUE`, PurchaseOrderId and SupplierId set, and RiskId pointing to the risk.

### 4.3 No duplicate alerts for same PO

1. With the same overdue PO as above, open **Risk Monitoring** again.
2. **Expected:** No second ProcurementAlert for the same PO (only one active PO_OVERDUE alert per PO). Existing risk may be escalated again (e.g. NextReviewDate/OverdueFlag updated) but no duplicate risk created for the same supplier delay.

---

## Quick checklist

| # | Step | Pass |
|---|------|------|
| 1 | Supplier Risk list from DB (no mock data) | ☐ |
| 2 | Add Supplier creates real Supplier record | ☐ |
| 3 | View History shows supplier audit / placeholder | ☐ |
| 4 | Create Risk from card → Risk with SourceType=Supplier | ☐ |
| 5 | PO Create shows warning when supplier score &lt; 60 | ☐ |
| 6 | PO Create/Edit/Details show Expected delivery date | ☐ |
| 7 | Open Monitoring → overdue PO creates alert + risk | ☐ |
| 8 | Re-open Monitoring → no duplicate alert for same PO | ☐ |
