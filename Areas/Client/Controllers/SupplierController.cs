using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WEB_Sentro.Data.Entities;
using WEB_Sentro.Models.Identity;
using WEB_Sentro.Services;
using Web_Sentro.Areas.Client.Models;

namespace Web_Sentro.Areas.Client.Controllers
{
    [Area("Client")]
    [Authorize]
    public class SupplierController : Controller
    {
        private const int DefaultPageSize = 8;
        private const int SupplierRiskWarningThreshold = 60;

        private readonly ITenantDbFactory _tenantDbFactory;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RiskService _riskService;
        private readonly SupplierRiskService _supplierRiskService;

        public SupplierController(ITenantDbFactory tenantDbFactory, UserManager<ApplicationUser> userManager, RiskService riskService, SupplierRiskService supplierRiskService)
        {
            _tenantDbFactory = tenantDbFactory;
            _userManager = userManager;
            _riskService = riskService;
            _supplierRiskService = supplierRiskService;
        }

        private async Task<ApplicationUser?> GetCurrentUserAsync() => await _userManager.GetUserAsync(User);
        private async Task<int?> GetMyOrgIdAsync()
        {
            var me = await GetCurrentUserAsync();
            return me?.OrganizationId;
        }
        private bool IsSuperAdmin() => User.IsInRole("SuperAdmin");

        public async Task<IActionResult> Index(string? search, string? resourceType, string? financialStatus, int page = 1, int pageSize = DefaultPageSize)
        {
            ViewData["Title"] = "Supplier Risk Registry";
            var orgId = IsSuperAdmin() ? null : await GetMyOrgIdAsync();
            if (!orgId.HasValue)
                return View(new PagedResult<SupplierRiskViewModel> { Items = new List<SupplierRiskViewModel>(), TotalCount = 0, PageNumber = 1, PageSize = pageSize });

            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 4, 20);

            await using var db = await _tenantDbFactory.CreateAsync(orgId.Value);
            var query = db.Suppliers.AsNoTracking().Where(s => s.OrgId == orgId.Value);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(s => s.Name.Contains(term) || (s.ContactPerson != null && s.ContactPerson.Contains(term)) || (s.Email != null && s.Email.Contains(term)));
            }
            if (!string.IsNullOrWhiteSpace(resourceType))
                query = query.Where(s => s.Category == resourceType);

            // Fetch suppliers
            var totalCount = await query.CountAsync();
            var suppliers = await query
                .OrderBy(s => s.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Include(s => s.PurchaseOrders)
                .ThenInclude(p => p.LineItems)
                .ToListAsync();

            var supplierIds = suppliers.Select(s => s.SupplierId).ToList();
            var riskSummaries = await _supplierRiskService.GetSupplierRiskSummariesAsync(orgId.Value, supplierIds);

            var items = new List<SupplierRiskViewModel>();
            foreach (var s in suppliers)
            {
                var summary = riskSummaries.GetValueOrDefault(s.SupplierId) ?? new SupplierRiskSummaryDto();
                
                decimal contractValue = s.PurchaseOrders.Sum(po => po.LineItems.Sum(li => li.Quantity * li.UnitCost));

                items.Add(new SupplierRiskViewModel
                {
                    Id = s.SupplierId,
                    SupplierName = s.Name ?? "",
                    ResourceType = s.Category ?? "—",
                    ReliabilityScore = summary.ReliabilityScore,
                    FinancialStatus = summary.FinancialStatus,
                    DeliveryTrend = summary.DeliveryTrend,
                    ContractValue = contractValue
                });
            }

            // In-memory filter for financial status (since it's calculated dynamically)
            if (!string.IsNullOrWhiteSpace(financialStatus))
            {
                items = items.Where(i => i.FinancialStatus == financialStatus).ToList();
            }

            var model = new PagedResult<SupplierRiskViewModel>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = page,
                PageSize = pageSize
            };
            return View(model);
        }


        [HttpGet]
        public async Task<IActionResult> Audit(int id)
        {
            ViewData["Title"] = "Supplier Audit Trail";
            var orgId = IsSuperAdmin() ? null : await GetMyOrgIdAsync();
            if (!orgId.HasValue) return RedirectToAction(nameof(Index));

            await using var db = await _tenantDbFactory.CreateAsync(orgId.Value);
            var supplier = await db.Suppliers.AsNoTracking().FirstOrDefaultAsync(s => s.SupplierId == id && s.OrgId == orgId.Value);
            if (supplier == null) return NotFound();

            ViewBag.SupplierName = supplier.Name;
            ViewBag.SupplierId = id;
            var auditLogs = await db.AuditLogs.AsNoTracking()
                .Where(a => a.OrgId == orgId.Value && a.EntityType == "Supplier" && a.EntityId == id)
                .OrderByDescending(a => a.CreatedAt)
                .Take(50)
                .ToListAsync();

            var userIds = auditLogs.Select(a => a.UserId).Distinct().ToList();
            var users = await _userManager.Users.AsNoTracking()
                .Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => $"{u.FirstName} {u.LastName}".Trim());

            var auditList = new List<dynamic>();
            foreach (var a in auditLogs)
            {
                int impact = 0;
                // Calculate impact based on action type
                switch (a.ActionType)
                {
                    case "RiskIdentified": impact = -15; break;
                    case "DisputeFiled": impact = -10; break;
                    case "PerformanceReview": impact = 5; break;
                    case "ContractRenewal": impact = 10; break;
                    case "CertificationVerified": impact = 8; break;
                    case "Initial Onboarding": impact = 50; break;
                    default: impact = 0; break;
                }

                // Adjust based on keywords in message if generic
                if (a.Message != null)
                {
                    if (a.Message.Contains("Critical", StringComparison.OrdinalIgnoreCase)) impact -= 10;
                    else if (a.Message.Contains("High", StringComparison.OrdinalIgnoreCase)) impact -= 5;
                    else if (a.Message.Contains("Improvement", StringComparison.OrdinalIgnoreCase)) impact += 5;
                }

                string auditorName = "System";
                if (users.TryGetValue(a.UserId, out var name) && !string.IsNullOrWhiteSpace(name))
                    auditorName = name;
                else if (a.UserId != "System" && a.UserId != null)
                    auditorName = "Unknown User";

                auditList.Add(new { Date = a.CreatedAt, Event = a.ActionType, Impact = impact, Note = a.Message ?? "", Auditor = auditorName });
            }

            if (auditList.Count == 0)
                auditList.Add(new { Date = supplier.CreatedAt, Event = "Initial Onboarding", Impact = 50, Note = "Supplier registered.", Auditor = "System" });

            ViewBag.TotalAudits = auditList.Count;
            ViewBag.PositiveEvents = auditList.Count(x => (int)x.Impact > 0);
            ViewBag.CriticalIssues = auditList.Count(x => (int)x.Impact < 0);
            
            var sum = auditList.Sum(x => (int)x.Impact);
            var avg = auditList.Count > 0 ? (double)sum / auditList.Count : 0;
            ViewBag.AverageImpact = (avg > 0 ? "+" : "") + avg.ToString("F1");

            return View(auditList);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OpenDispute(int supplierId, string reason, string severity)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Challenge();
            var orgId = IsSuperAdmin() ? null : await GetMyOrgIdAsync();
            if (!orgId.HasValue) return RedirectToAction(nameof(Index));

            await using (var db = await _tenantDbFactory.CreateAsync(orgId.Value))
            {
                var supplier = await db.Suppliers.FindAsync(supplierId);
                if (supplier != null)
                {
                    _riskService.AddAuditLog(db, orgId.Value, user.Id, "Supplier", supplierId, "DisputeFiled", $"Dispute filed: {reason} (Severity: {severity})", HttpContext.Connection.RemoteIpAddress?.ToString());
                    await db.SaveChangesAsync();
                }
            }

            TempData["Alert"] = "Dispute case filed successfully. Legal team notified.";
            TempData["AlertType"] = "success";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateRiskFromSupplier(int supplierId, string? riskType)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Challenge();
            var orgId = IsSuperAdmin() ? null : await GetMyOrgIdAsync();
            if (!orgId.HasValue) return RedirectToAction(nameof(Index));

            await using var db = await _tenantDbFactory.CreateAsync(orgId.Value);
            var supplier = await db.Suppliers.AsNoTracking().FirstOrDefaultAsync(s => s.SupplierId == supplierId && s.OrgId == orgId.Value);
            if (supplier == null) return NotFound();

            string titlePrefix = "Supplier Risk";
            string category = "Supplier"; // Default category

            switch (riskType)
            {
                case "Delivery":
                    titlePrefix = "Supplier Delivery Delay Risk";
                    category = "Delivery";
                    break;
                case "Quality":
                    titlePrefix = "Supplier Quality Risk";
                    category = "Quality";
                    break;
                case "Financial":
                    titlePrefix = "Supplier Financial Risk";
                    category = "Financial";
                    break;
                case "Contract":
                    titlePrefix = "Supplier Contract Risk";
                    category = "Legal";
                    break;
            }

            var title = $"{titlePrefix} – {supplier.Name}";
            var risk = await _riskService.CreateRiskAsync(
                orgId.Value,
                user.Id,
                title,
                category,
                "Supplier",
                null,
                $"Risk created from Supplier Risk Registry. Type: {riskType ?? "General"}. Supplier: {supplier.Name}.",
                "Draft",
                siteId: null,
                supplierId: supplierId);

            await using (var db2 = await _tenantDbFactory.CreateAsync(orgId.Value))
            {
                _riskService.AddAuditLog(db2, orgId.Value, user.Id, "Risk", risk.RiskId, "RiskCreatedFromSupplier", $"Supplier risk created: {title}", HttpContext.Connection.RemoteIpAddress?.ToString());
                _riskService.AddAuditLog(db2, orgId.Value, user.Id, "Supplier", supplierId, "RiskIdentified", $"Risk identified: {title}", HttpContext.Connection.RemoteIpAddress?.ToString());
                await _riskService.SaveChangesAsync(db2);
            }

            TempData["SuccessMessage"] = "Risk created. You can assess it from the Risk register.";
            return RedirectToAction("Assess", "Risks", new { area = "Client", id = risk.RiskId });
        }
    }
}
