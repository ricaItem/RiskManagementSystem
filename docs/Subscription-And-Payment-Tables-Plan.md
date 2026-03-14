# Plan: Subscription, Invoice & Payment Tables (no implementation yet)

## 1. Current state: Users & Organization (double-check)

### 1.1 Users (Identity + ApplicationUser)

| Source | Table(s) | Purpose |
|--------|----------|--------|
| Identity | `AspNetUsers` | Core identity (Id, UserName, Email, PasswordHash, etc.) |
| ApplicationUser | same table, extra columns | `OrganizationId`, `FirstName`, `LastName`, `IsActive`, `CreatedAt`, `LastLoginAt` |

- **ApplicationUser** has **OrganizationId** (int): each user belongs to one organization.
- Identity tables: `AspNetRoles`, `AspNetUserRoles`, `AspNetUserClaims`, `AspNetUserLogins`, `AspNetUserTokens`, `AspNetRoleClaims`.

**Note:** There is no FK from `AspNetUsers.OrganizationId` → `Organizations.OrganizationId` in the current migrations. Consider adding it when the org exists at user creation (e.g. after registration flow creates org first), or keep it loose if users can exist before org.

---

### 1.2 Organization

| Column | Type | Notes |
|--------|------|--------|
| OrganizationId | int PK | Identity |
| OrgCode | nvarchar(30) | Unique |
| OrgName | nvarchar(200) | |
| AddressLine, City, Province, Country | nvarchar | Optional |
| PrimaryEmail, PrimaryPhone | nvarchar | Optional |
| **PlanName** | nvarchar(50) | Default "Basic" – **currently free text, no Plan FK** |
| **Status** | nvarchar(20) | Default "Active" |
| CreatedAt, UpdatedAt | datetime2 | |
| CreatedByUserId | nvarchar(450) | Admin user who created the org |

- **Gap:** `PlanName` is a string only. No link to a **Plan** entity (no price, billing interval, or feature set stored in one place).
- **Gap:** No subscription or billing history: no “current period”, no invoices, no payment records.

---

## 2. Proposed new tables (layout only)

All new entities are intended for the **platform DB** (same as Organizations / Identity), so they live in **PlatformDbContext**.

---

### 2.1 Plan (catalog of offerings)

**Purpose:** Single place for plan definitions (Basic, Professional, Enterprise): price, currency, billing interval, and optional metadata (e.g. seat limits). No per-org state here.

| Column | Type | Notes |
|--------|------|--------|
| PlanId | int PK | Identity |
| Code | nvarchar(50) | e.g. "Basic", "Professional", "Enterprise" – unique |
| DisplayName | nvarchar(100) | e.g. "Basic" |
| AmountCentavos | long | Price in centavos (e.g. 4900 = ₱49.00) |
| Currency | nvarchar(3) | "PHP" |
| BillingInterval | nvarchar(20) | "month", "year" |
| MaxAdminSeats | int? | Null = unlimited or defined elsewhere |
| IsActive | bool | So you can retire plans without deleting |
| SortOrder | int | For UI ordering |
| CreatedAt | datetime2 | |

- **No FK from Organization here.** Organization’s current plan is expressed via **Subscription** (and optionally keep `Organization.PlanName` as denormalized cache).

---

### 2.2 Subscription (per-organization, current plan & period)

**Purpose:** One “active” subscription per organization (or one current row per org): which plan they’re on, current billing period, and status. Ties org to Plan and to future Invoices.

| Column | Type | Notes |
|--------|------|--------|
| SubscriptionId | int PK | Identity |
| OrganizationId | int FK → Organizations | One active subscription per org (or enforce in app logic) |
| PlanId | int FK → Plan | |
| Status | nvarchar(20) | e.g. "Active", "PastDue", "Canceled", "Trialing" |
| CurrentPeriodStart | datetime2 | Start of current billing period |
| CurrentPeriodEnd | datetime2 | End of current billing period |
| StartedAt | datetime2 | When this subscription started |
| CanceledAt | datetime2? | If canceled |
| CreatedAt, UpdatedAt | datetime2 | |

- **Relations:** Organization 1 → 1 (or 0..1) current Subscription; Subscription N → 1 Plan.
- **Invoices** can reference SubscriptionId (and OrganizationId) for “this invoice is for this subscription/period”.

---

### 2.3 Invoice (billing document per period or ad-hoc)

**Purpose:** One row per billing document: amount due, line items (or summary), link to subscription period. Payments (PayMongo, etc.) can attach to an invoice.

| Column | Type | Notes |
|--------|------|--------|
| InvoiceId | int PK | Identity |
| OrganizationId | int FK → Organizations | |
| SubscriptionId | int? FK → Subscription | Null if one-off / manual invoice |
| InvoiceNumber | nvarchar(50) | Human-readable, unique (e.g. INV-2025-00123) |
| Status | nvarchar(20) | "Draft", "Open", "Paid", "PartiallyPaid", "Canceled", "Overdue" |
| AmountDueCentavos | long | Total in centavos |
| Currency | nvarchar(3) | "PHP" |
| PeriodStart | datetime2? | Billing period start (for subscription invoices) |
| PeriodEnd | datetime2? | Billing period end |
| DueDate | datetime2 | |
| PaidAt | datetime2? | When fully paid (or set when last payment covers it) |
| CreatedAt, UpdatedAt | datetime2 | |
| CreatedByUserId | nvarchar(450)? | Admin who created (if manual) |

- Optional: **InvoiceLineItem** (child table) for line-level detail (description, quantity, amount) if you want itemized invoices. For a simple “one line = subscription fee” you can skip and keep only AmountDueCentavos.

---

### 2.4 Payment (record of each payment transaction)

**Purpose:** One row per payment attempt/success: link to PayMongo (or another gateway), link to invoice and org, store amount and status. This is the “transaction log” for money in.

| Column | Type | Notes |
|--------|------|--------|
| PaymentId | int PK | Identity |
| OrganizationId | int FK → Organizations | |
| InvoiceId | int? FK → Invoice | Null if payment before invoice (e.g. prepay then create invoice) |
| Gateway | nvarchar(30) | "PayMongo" |
| GatewayPaymentIntentId | nvarchar(100) | PayMongo payment intent id (or external reference) |
| GatewayStatus | nvarchar(50) | e.g. "succeeded", "failed", "awaiting_payment_method" |
| AmountCentavos | long | Amount actually charged / attempted |
| Currency | nvarchar(3) | "PHP" |
| PaymentMethod | nvarchar(30) | "card", "gcash", "paymaya" |
| Status | nvarchar(20) | "Pending", "Succeeded", "Failed", "Refunded" |
| PaidAt | datetime2? | When gateway reported success |
| MetadataJson | nvarchar(max)? | Optional JSON for extra gateway data |
| CreatedAt, UpdatedAt | datetime2 | |
| CreatedByUserId | nvarchar(450)? | User who initiated (e.g. admin who completed setup) |

- **Relations:** Payment N → 1 Organization; Payment N → 0..1 Invoice. One Invoice can have many Payments (e.g. partial payments, or one successful payment).
- **Recording flow:** When PayMongo confirms payment (webhook or polling), create/update **Payment** and then update **Invoice** (e.g. Status = Paid, PaidAt) and optionally **Subscription** (renew period).

---

## 3. Relationship summary

```
Plan (1) ──────────< Subscription (N)  (each subscription has one plan)
                           │
Organization (1) ──────────┼──< Subscription (1 per org, “current”)
                           │
                           ├──< Invoice (N)  (invoices for this subscription / org)
                           │         │
                           │         └──< Payment (N)  (payments against this invoice)
                           │
User (ApplicationUser) ──── OrganizationId → Organization (N users per org)
Organization.CreatedByUserId → User (admin who created org)
```

- **Admin & organization:** Already have **User.OrganizationId** and **Organization.CreatedByUserId**. New tables only add: **Subscription**, **Invoice**, and **Payment** all reference **OrganizationId** (and optionally **CreatedByUserId** for audit). No change to existing User/Organization tables required for this plan.
- **Invoices and records:** **Invoice** stores the “document” (amount, period, status). **Payment** stores each transaction (PayMongo id, amount, status). You can list “all payments for this org” or “all payments for this invoice” and “all invoices for this org/subscription”.

---

## 4. Will this reflect and display in Organization, Admin accounts, SuperAdmin, and related?

**Yes.** The model is designed so the same data can be shown in:

| Where | What to show (data source) |
|-------|----------------------------|
| **SuperAdmin – Organizations list** (Vendor/Organizations/Index) | Rows from **Organizations** joined with **Subscription** (and **Plan**): Org name, **Plan** (from Plan.DisplayName or Subscription → Plan), **Status** (Organization.Status and/or Subscription.Status), admin count (count of Users where OrganizationId = X). Optional: last payment date or “Next billing” from Subscription.CurrentPeriodEnd / latest Invoice. |
| **SuperAdmin – Org Admins** (Vendor/OrgAdmins/Index) | **ApplicationUser** (name, email, LastLoginAt, IsActive) joined with **Organizations** (OrgName) for “Organization” column. Optional: show org’s **Plan** or **Subscription** status (from Subscription + Plan) per row. |
| **SuperAdmin – Billing** (Vendor/Billing/Index) | **Invoice** and **Payment**: list invoices per org (or across all orgs), MRR from **Subscription** + **Plan** (sum of active subscriptions × plan amount), “Total Revenue” from **Payment** (Status = Succeeded). |
| **Organization detail / edit** (when you add it) | One org: **Subscription** (current plan, period, status), list of **Invoices** (and optionally **Payments** for each). |
| **Client/Admin – Account or Billing** (org-scoped) | For the logged-in user’s **OrganizationId**: their org’s **Subscription**, **Invoices**, and **Payments** (read-only or “download invoice”). |

**How it works:**

- Every **Subscription**, **Invoice**, and **Payment** row has **OrganizationId**, so you can always filter by org and show “this org’s plan, invoices, and payments.”
- **Admin accounts** are **ApplicationUser** rows with **OrganizationId**; the Org Admins screen already lists “Administrator + Organization” — you’d load users from the DB and join **Organizations** (and optionally Subscription/Plan for the org).
- **SuperAdmin** sees all organizations and all admins because the Vendor area is not restricted by OrganizationId; you’ll query Organizations, Subscriptions, Invoices, Payments (and Users for admin count / list) and pass them to the existing views (replacing the current hardcoded “firms” / “admins” arrays).

So: **Organization**, **Admin accounts**, **SuperAdmin**, and **Billing** (and any org-level “Account” or “Billing” for clients) can all reflect and display this data once the tables exist and the controllers/views load from the DB instead of hardcoded data.

---

## 5. Suggested implementation order (when you implement)

1. **Plan** – create entity, migration, seed Basic/Professional/Enterprise.
2. **Subscription** – create entity, FK to Organization and Plan; migration; logic to create/update “current” subscription when org is created or plan changes.
3. **Invoice** – create entity, FK to Organization and optional Subscription; migration; number generation (InvoiceNumber).
4. **Payment** – create entity, FK to Organization and optional Invoice; migration; hook from PayMongo success (webhook or polling) to insert Payment and update Invoice/Subscription.

Optional later:

- **InvoiceLineItem** if you want itemized invoices.
- **Webhook** endpoint for PayMongo `payment.paid` / `payment.failed` to create/update **Payment** and then Invoice/Subscription.

---

## 6. Summary

- **Users:** AspNetUsers + ApplicationUser with **OrganizationId**; no FK to Organization in DB today – add if you want referential integrity.
- **Organization:** Has **PlanName** (string) and **Status**; no Plan or subscription/invoice/payment tables yet.
- **Plan:** New table for plan catalog (price, interval, code).
- **Subscription:** New table: one current subscription per org, links Org → Plan, holds period and status.
- **Invoice:** New table: billing document per org (and optional subscription), amount, status, due date; stores the “invoice” record.
- **Payment:** New table: per transaction, links to org and optional invoice, stores PayMongo id and status; stores the “payment” record for subscriptions and invoices.

This layout supports recording subscriptions, invoices, and payments and linking them to admin and organization without changing existing User/Organization table shapes; only additions and migrations when you implement.
