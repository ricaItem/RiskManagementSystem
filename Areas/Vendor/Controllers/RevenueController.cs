using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using WEB_Sentro.Services;

namespace WEB_Sentro.Areas.Vendor.Controllers;

[Area("Vendor")]
[Authorize(Policy = "SuperAdminOnly")]
public class RevenueController : Controller
{
    private readonly IRevenueAnalyticsService _revenueAnalyticsService;

    public RevenueController(IRevenueAnalyticsService revenueAnalyticsService)
    {
        _revenueAnalyticsService = revenueAnalyticsService;
    }

    public async Task<IActionResult> Index(string? range, CancellationToken ct = default)
    {
        var model = await _revenueAnalyticsService.BuildAsync(range, ct);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> ExportTopOrganizationsCsv(string? range, CancellationToken ct = default)
    {
        var model = await _revenueAnalyticsService.BuildAsync(range, ct);
        var sb = new StringBuilder();
        sb.AppendLine("OrganizationId,OrganizationName,CollectedCentavos,CollectedDisplay");

        foreach (var row in model.TopOrganizations)
        {
            sb.Append(EscapeCsv(row.OrganizationId.ToString()));
            sb.Append(',');
            sb.Append(EscapeCsv(row.OrganizationName));
            sb.Append(',');
            sb.Append(EscapeCsv(row.CollectedCentavos.ToString()));
            sb.Append(',');
            sb.Append(EscapeCsv(FormatCurrency(row.CollectedCentavos)));
            sb.AppendLine();
        }

        var fileName = $"revenue-top-organizations-{model.SelectedRange}-{DateTime.UtcNow:yyyyMMddHHmmss}.csv";
        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", fileName);
    }

    [HttpGet]
    public async Task<IActionResult> ExportAgingCsv(string? range, CancellationToken ct = default)
    {
        var model = await _revenueAnalyticsService.BuildAsync(range, ct);
        var sb = new StringBuilder();
        sb.AppendLine("Bucket,InvoiceCount,AmountCentavos,AmountDisplay");

        foreach (var row in model.Aging)
        {
            sb.Append(EscapeCsv(row.Bucket));
            sb.Append(',');
            sb.Append(EscapeCsv(row.InvoiceCount.ToString()));
            sb.Append(',');
            sb.Append(EscapeCsv(row.AmountCentavos.ToString()));
            sb.Append(',');
            sb.Append(EscapeCsv(FormatCurrency(row.AmountCentavos)));
            sb.AppendLine();
        }

        var fileName = $"revenue-ar-aging-{model.SelectedRange}-{DateTime.UtcNow:yyyyMMddHHmmss}.csv";
        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", fileName);
    }

    private static string EscapeCsv(string value)
    {
        value ??= string.Empty;
        if (value.Contains('"'))
        {
            value = value.Replace("\"", "\"\"");
        }

        if (value.Contains(',') || value.Contains('\n') || value.Contains('\r') || value.Contains('"'))
        {
            return $"\"{value}\"";
        }

        return value;
    }

    private static string FormatCurrency(long centavos)
    {
        return centavos % 100 == 0
            ? $"PHP {centavos / 100:N0}"
            : $"PHP {centavos / 100.0:N2}";
    }
}
