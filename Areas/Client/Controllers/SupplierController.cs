using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Web_Sentro.Areas.Client.Models;

[Area("Client")]
[Authorize]
public class SupplierController : Controller
{
    private const int DefaultPageSize = 8;

    public IActionResult Index(int page = 1, int pageSize = DefaultPageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 4, 20);

        var all = new List<SupplierRiskViewModel>
        {
            new SupplierRiskViewModel { Id = 1, SupplierName = "Global Steel Co.", ResourceType = "Structural Steel", ReliabilityScore = 88, FinancialStatus = "Stable", DeliveryTrend = "On-Time", ContractValue = 1250000 },
            new SupplierRiskViewModel { Id = 2, SupplierName = "Davao Cement Corp", ResourceType = "Concrete", ReliabilityScore = 42, FinancialStatus = "Warning", DeliveryTrend = "Critical", ContractValue = 450000 },
            new SupplierRiskViewModel { Id = 3, SupplierName = "Luzon Power Systems", ResourceType = "Electrical", ReliabilityScore = 75, FinancialStatus = "Stable", DeliveryTrend = "Delayed", ContractValue = 890000 },
            new SupplierRiskViewModel { Id = 4, SupplierName = "BuildRight Equipment", ResourceType = "Machinery", ReliabilityScore = 95, FinancialStatus = "Stable", DeliveryTrend = "On-Time", ContractValue = 2100000 }
        };

        var totalCount = all.Count;
        var items = all.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        var model = new PagedResult<SupplierRiskViewModel>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = page,
            PageSize = pageSize
        };
        return View(model);
    }


    [HttpPost]
    public IActionResult AddSupplier(SupplierRiskViewModel model)
    {
        return RedirectToAction("Index");
    }

    [HttpGet]
    [HttpGet]
    public IActionResult Audit(int id)
    {
        ViewData["Title"] = "Supplier Audit Trail";

        ViewBag.SupplierName = id == 2 ? "Davao Cement Corp" : "Global Steel Co.";
        ViewBag.TotalAudits = 14;
        ViewBag.PositiveEvents = 9;
        ViewBag.CriticalIssues = 2;
        ViewBag.AverageImpact = id == 2 ? "-12.5" : "+18.2";

        var auditLogs = new List<dynamic>
        {
            new { Date = DateTime.Now.AddDays(-2), Event = "Delivery Delay", Impact = -15, Note = "Logistics breakdown at port.", Auditor = "S. Ramos" },
            new { Date = DateTime.Now.AddDays(-15), Event = "Quality Audit", Impact = 20, Note = "Material testing passed ISO standards.", Auditor = "System" },
            new { Date = DateTime.Now.AddMonths(-1), Event = "Safety Violation", Impact = -30, Note = "Uncertified operator on delivery vehicle.", Auditor = "J. Dela Cruz" },
            new { Date = DateTime.Now.AddMonths(-2), Event = "Bulk Discount Applied", Impact = 5, Note = "Contract renegotiation successful.", Auditor = "Admin" },
            new { Date = DateTime.Now.AddMonths(-3), Event = "Initial Onboarding", Impact = 50, Note = "Standard baseline applied.", Auditor = "Admin" }
        };

        return View(auditLogs);
    }

   
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult OpenDispute(int supplierId, string reason, string severity)
    {
        TempData["Alert"] = "Dispute case filed successfully. Legal team notified.";
        TempData["AlertType"] = "success";

        return RedirectToAction("Index");
    }
}