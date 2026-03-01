using ClosedXML.Excel;
using WEB_Sentro.Data;
using Web_Sentro.Areas.Client.Models;

namespace WEB_Sentro.Services
{
    public class RiskExportService
    {
        private readonly RiskService _riskService;
        private readonly ControlService _controlService;
        private readonly ITenantDbFactory _tenantDbFactory;

        public RiskExportService(RiskService riskService, ControlService controlService, ITenantDbFactory tenantDbFactory)
        {
            _riskService = riskService;
            _controlService = controlService;
            _tenantDbFactory = tenantDbFactory;
        }

        /// <summary>Builds Excel (governance-grade) with owner, accountable, treatment, score, band, next review, overdue, linked controls.</summary>
        public async Task<byte[]> ExportToExcelAsync(int orgId, string? userId, bool employeeOnly, CancellationToken ct = default)
        {
            var list = await _riskService.GetRisksForListAsync(orgId, userId, employeeOnly, null, null, null, null, false, ct);
            await using var db = await _tenantDbFactory.CreateAsync(orgId);
            var riskIds = list.Select(r => r.Id).ToList();
            var linkedControlsByRisk = new Dictionary<int, string>();
            foreach (var riskId in riskIds)
            {
                var links = await _controlService.GetLinkedControlsForRiskAsync(riskId, orgId, ct);
                linkedControlsByRisk[riskId] = links.Count > 0 ? string.Join("; ", links.Select(l => l.ControlName)) : "";
            }

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Risk Register");
            var headers = new[] { "Id", "Title", "Category", "Priority", "Status", "Owner", "Accountable", "Treatment decision", "Treatment justification", "Score", "Band", "Next review date", "Overdue", "Linked controls", "Reported by", "Date reported", "Site" };
            for (var i = 0; i < headers.Length; i++)
                ws.Cell(1, i + 1).Value = headers[i];
            ws.Row(1).Style.Font.Bold = true;

            var row = 2;
            foreach (var r in list)
            {
                ws.Cell(row, 1).Value = r.Id;
                ws.Cell(row, 2).Value = r.Title ?? "";
                ws.Cell(row, 3).Value = r.Category ?? "";
                ws.Cell(row, 4).Value = r.Priority ?? "";
                ws.Cell(row, 5).Value = r.Status ?? "";
                ws.Cell(row, 6).Value = r.RiskOwnerName ?? "";
                ws.Cell(row, 7).Value = r.AccountableName ?? "";
                ws.Cell(row, 8).Value = r.TreatmentDecision ?? "";
                ws.Cell(row, 9).Value = ""; // justification not in list VM; could add to VM if needed
                ws.Cell(row, 10).Value = r.RiskScore.HasValue ? r.RiskScore.Value.ToString() : "";
                ws.Cell(row, 11).Value = r.AppetiteBandName ?? r.Priority ?? ""; // Band from matrix or priority
                ws.Cell(row, 12).Value = r.NextReviewDate.HasValue ? r.NextReviewDate.Value.ToString("yyyy-MM-dd") : "";
                ws.Cell(row, 13).Value = r.OverdueFlag ? "Yes" : "No";
                ws.Cell(row, 14).Value = linkedControlsByRisk.TryGetValue(r.Id, out var controls) ? controls : "";
                ws.Cell(row, 15).Value = r.ReportedBy ?? "";
                ws.Cell(row, 16).Value = r.DateReported?.ToString("yyyy-MM-dd") ?? r.DateLogged.ToString("yyyy-MM-dd");
                ws.Cell(row, 17).Value = r.SiteName ?? "";
                row++;
            }
            ws.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream, false);
            return stream.ToArray();
        }
    }
}
