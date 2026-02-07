using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace WEB_Sentro.Areas.Vendor.Controllers;

[Area("Vendor")]
[Authorize(Roles = "SuperAdmin")]
public class DashboardController : Controller
{
    private readonly ILogger<DashboardController> _logger;
    public DashboardController(ILogger<DashboardController> logger) => _logger = logger;

    public IActionResult Index()
    {
        _logger.LogInformation("Dashboard Index requested. IsAuthenticated={IsAuthenticated}", User?.Identity?.IsAuthenticated ?? false);
        _logger.LogInformation("UserName: {Name}", User?.Identity?.Name ?? "<null>");
        _logger.LogInformation("IsInRole SuperAdmin (User.IsInRole): {IsInRole}", User?.IsInRole("SuperAdmin") ?? false);
        foreach (var c in User?.Claims ?? Enumerable.Empty<System.Security.Claims.Claim>())
        {
            _logger.LogDebug("Claim: {Type} = {Value}", c.Type, c.Value);
        }
        return View();
    }
}
