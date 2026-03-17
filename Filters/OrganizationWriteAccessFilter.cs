using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using WEB_Sentro.Data;

namespace WEB_Sentro.Filters;

public class OrganizationWriteAccessFilter : IAsyncActionFilter
{
    private static readonly HashSet<string> SafeMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        HttpMethods.Get,
        HttpMethods.Head,
        HttpMethods.Options,
        HttpMethods.Trace
    };

    private readonly PlatformDbContext _platformDb;
    private readonly ITempDataDictionaryFactory _tempDataFactory;

    public OrganizationWriteAccessFilter(PlatformDbContext platformDb, ITempDataDictionaryFactory tempDataFactory)
    {
        _platformDb = platformDb;
        _tempDataFactory = tempDataFactory;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var http = context.HttpContext;
        var request = http.Request;

        if (SafeMethods.Contains(request.Method))
        {
            await next();
            return;
        }

        if (!http.User.Identity?.IsAuthenticated ?? true)
        {
            await next();
            return;
        }

        var path = request.Path.Value ?? string.Empty;
        if (path.StartsWith("/Identity/Account/Logout", StringComparison.OrdinalIgnoreCase))
        {
            await next();
            return;
        }

        var area = context.RouteData.Values.TryGetValue("area", out var areaObj)
            ? areaObj?.ToString() ?? string.Empty
            : string.Empty;

        var isClientArea = string.Equals(area, "Client", StringComparison.OrdinalIgnoreCase);
        var isApi = path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase);
        if (!isClientArea && !isApi)
        {
            await next();
            return;
        }

        var userId = http.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            await next();
            return;
        }

        var orgId = await _platformDb.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => (int?)u.OrganizationId)
            .FirstOrDefaultAsync(http.RequestAborted);

        if (!orgId.HasValue || orgId.Value <= 0)
        {
            await next();
            return;
        }

        var status = await _platformDb.Organizations.AsNoTracking()
            .Where(o => o.OrganizationId == orgId.Value)
            .Select(o => o.Status)
            .FirstOrDefaultAsync(http.RequestAborted);

        if (!string.Equals(status, "Suspended", StringComparison.OrdinalIgnoreCase))
        {
            await next();
            return;
        }

        const string message = "Your organization is suspended. Write actions are disabled; read-only access remains available.";

        var acceptsJson = request.Headers.Accept.ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase);
        if (isApi || acceptsJson)
        {
            context.Result = new ObjectResult(new
            {
                error = "organization_suspended_read_only",
                message
            })
            {
                StatusCode = StatusCodes.Status423Locked
            };
            return;
        }

        var tempData = _tempDataFactory.GetTempData(http);
        tempData["Error"] = message;
        context.Result = new RedirectToActionResult("Index", "Dashboard", new { area = "Client" });
    }
}
