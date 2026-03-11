using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using WEB_Sentro.Areas.Client.Models;

namespace WEB_Sentro.Services;

public class RiskAnalyticsPdfService
{
    public byte[] GeneratePdf(RiskAnalyticsViewModel model, RiskAnalyticsExportScope scope)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter.Landscape());
                page.Margin(36);
                page.DefaultTextStyle(x => x.FontSize(9).FontColor(Colors.Grey.Darken3));

                page.Content().Column(column =>
                {
                    // ---- Title & scope ----
                    column.Item().Text("Risk Analytics Report").FontSize(18).Bold().FontColor(Colors.Blue.Darken2);
                    column.Item().PaddingTop(4).Text($"Generated on {DateTime.Now:MMMM d, yyyy} at {DateTime.Now:h:mm tt}");
                    column.Item().PaddingTop(2).Text($"Scope: {scope.PeriodLabel} · {scope.SiteLabel} · {scope.CategoryLabel}").FontColor(Colors.Grey.Medium);
                    column.Item().PaddingVertical(12).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                    // ---- Executive summary ----
                    column.Item().Text("Executive Summary").FontSize(12).Bold();
                    var kpis = model.Kpis;
                    var activeRisks = kpis?.ActiveRisks ?? 0;
                    var criticalRisks = kpis?.CriticalRisks ?? 0;
                    var createdInPeriod = kpis?.CreatedInPeriod ?? 0;
                    var avgReduction = kpis?.AvgRiskReductionPercent ?? 0;
                    var avgCloseDays = kpis?.AvgTimeToCloseDays ?? 0;
                    var summary = $"This report presents risk performance across the selected period. There are {activeRisks} active risks in scope, including {criticalRisks} critical. {createdInPeriod} new risks were identified in the period. On average, mitigation measures have reduced risk scores by {avgReduction}%, and risks are closed within approximately {avgCloseDays} days. The sections below provide trend data, category distribution, mitigation effectiveness, site-level comparison, and high-priority items requiring attention.";
                    column.Item().PaddingTop(4).Text(summary).LineHeight(1.3f);
                    column.Item().PaddingVertical(12).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                    // ---- Key metrics ----
                    column.Item().Text("Key Metrics").FontSize(12).Bold();
                    column.Item().PaddingTop(2).Text("At a glance: active and critical risk counts, new risks in period, weather-related, average time to close, and overall risk reduction.").FontSize(8).FontColor(Colors.Grey.Medium);
                    column.Item().PaddingTop(6).Row(row =>
                    {
                        foreach (var k in model.Kpis?.KpiCards ?? new List<KpiCardViewModel>())
                        {
                            row.RelativeItem().Padding(6).Border(0.5f).BorderColor(Colors.Grey.Lighten2).Background(Colors.Grey.Lighten5).Column(c =>
                            {
                                c.Item().Text(k.Label).FontSize(8).FontColor(Colors.Grey.Medium);
                                c.Item().PaddingTop(2).Text(k.Value.ToString()).FontSize(14).Bold();
                                if (!string.IsNullOrEmpty(k.DeltaText))
                                    c.Item().PaddingTop(1).Text(k.DeltaText).FontSize(8).FontColor(k.DeltaUp ? Colors.Red.Medium : Colors.Green.Medium);
                            });
                        }
                    });
                    column.Item().PaddingVertical(12).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                    // ---- Risk trends (table) ----
                    column.Item().Text("Risk Trends").FontSize(12).Bold();
                    column.Item().PaddingTop(2).Text("New risks identified over time. Use this to spot spikes or confirm consistent detection.").FontSize(8).FontColor(Colors.Grey.Medium);
                    column.Item().PaddingTop(4).Table(table =>
                    {
                        table.ColumnsDefinition(cd => { cd.RelativeColumn(); cd.ConstantColumn(60); });
                        table.Header(h =>
                        {
                            h.Cell().Padding(5).Background(Colors.Blue.Lighten4).Text("Period").Bold();
                            h.Cell().Padding(5).Background(Colors.Blue.Lighten4).AlignRight().Text("Count").Bold();
                        });
                        var labels = model.Charts?.RisksOverTimeLabels ?? new List<string>();
                        var values = model.Charts?.RisksOverTimeValues ?? new List<int>();
                        for (var i = 0; i < Math.Max(labels.Count, values.Count); i++)
                        {
                            table.Cell().Padding(4).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Text(i < labels.Count ? labels[i] : "");
                            table.Cell().Padding(4).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).AlignRight().Text(i < values.Count ? values[i].ToString() : "0");
                        }
                    });
                    column.Item().PaddingVertical(8);

                    // ---- Risk by category (table) ----
                    column.Item().Text("Risk Categories").FontSize(12).Bold();
                    column.Item().PaddingTop(2).Text("Distribution by type to focus controls and training.").FontSize(8).FontColor(Colors.Grey.Medium);
                    column.Item().PaddingTop(4).Table(table =>
                    {
                        table.ColumnsDefinition(cd => { cd.RelativeColumn(); cd.ConstantColumn(60); });
                        table.Header(h =>
                        {
                            h.Cell().Padding(5).Background(Colors.Teal.Lighten4).Text("Category").Bold();
                            h.Cell().Padding(5).Background(Colors.Teal.Lighten4).AlignRight().Text("Count").Bold();
                        });
                        var catLabels = model.Charts?.RisksByCategoryLabels ?? new List<string>();
                        var catValues = model.Charts?.RisksByCategoryValues ?? new List<int>();
                        for (var i = 0; i < Math.Max(catLabels.Count, catValues.Count); i++)
                        {
                            table.Cell().Padding(4).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Text(i < catLabels.Count ? catLabels[i] : "");
                            table.Cell().Padding(4).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).AlignRight().Text(i < catValues.Count ? catValues[i].ToString() : "0");
                        }
                    });
                    column.Item().PaddingVertical(12).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                    // ---- Mitigation effectiveness ----
                    var mit = model.Mitigation;
                    column.Item().Text("Mitigation Effectiveness").FontSize(12).Bold();
                    column.Item().PaddingTop(2).Text("Initial vs residual risk scores. Higher reduction indicates effective controls.").FontSize(8).FontColor(Colors.Grey.Medium);
                    column.Item().PaddingTop(6).Row(r =>
                    {
                        r.RelativeItem().Padding(8).Background(Colors.Grey.Lighten5).Column(c =>
                        {
                            c.Item().Text("Avg Initial Score").FontSize(8).FontColor(Colors.Grey.Medium);
                            c.Item().Text((mit?.AvgInitialScore ?? 0).ToString("F1")).FontSize(16).Bold();
                        });
                        r.RelativeItem().Padding(8).Background(Colors.Green.Lighten5).Column(c =>
                        {
                            c.Item().Text("Avg Residual Score").FontSize(8).FontColor(Colors.Grey.Medium);
                            c.Item().Text((mit?.AvgResidualScore ?? 0).ToString("F1")).FontSize(16).Bold().FontColor(Colors.Green.Darken2);
                        });
                        r.RelativeItem().Padding(8).Background(Colors.Blue.Lighten5).Column(c =>
                        {
                            c.Item().Text("Avg Reduction").FontSize(8).FontColor(Colors.Grey.Medium);
                            c.Item().Text((mit?.AvgReductionPercent ?? 0) + "%").FontSize(16).Bold().FontColor(Colors.Blue.Darken2);
                        });
                        r.RelativeItem().Padding(8).Column(c =>
                        {
                            c.Item().Text("Reassessed %").FontSize(8).FontColor(Colors.Grey.Medium);
                            c.Item().Text((mit?.ReassessedPercent ?? 0) + "%").FontSize(16).Bold();
                        });
                    });
                    column.Item().PaddingVertical(12).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                    // ---- Predictive insights summary ----
                    var insights = model.PredictiveInsights;
                    column.Item().Text("Predictive Insights").FontSize(12).Bold();
                    column.Item().PaddingTop(2).Text("Forward-looking signals: escalation candidates, momentum.").FontSize(8).FontColor(Colors.Grey.Medium);
                    column.Item().PaddingTop(6).Row(r =>
                    {
                        r.RelativeItem().Padding(6).Border(0.5f).BorderColor(Colors.Grey.Lighten2).Column(c =>
                        {
                            c.Item().Text("High-risk candidates").FontSize(8);
                            c.Item().Text((insights?.EscalationHigh ?? 0).ToString()).FontSize(14).Bold();
                        });
                        r.RelativeItem().Padding(6).Border(0.5f).BorderColor(Colors.Grey.Lighten2).Column(c =>
                        {
                            c.Item().Text("Medium").FontSize(8);
                            c.Item().Text((insights?.EscalationMedium ?? 0).ToString()).FontSize(14).Bold();
                        });
                        r.RelativeItem().Padding(6).Border(0.5f).BorderColor(Colors.Grey.Lighten2).Column(c =>
                        {
                            c.Item().Text("Low").FontSize(8);
                            c.Item().Text((insights?.EscalationLow ?? 0).ToString()).FontSize(14).Bold();
                        });
                        r.RelativeItem().Padding(6).Border(0.5f).BorderColor(Colors.Grey.Lighten2).Column(c =>
                        {
                            c.Item().Text("Momentum").FontSize(8);
                            c.Item().Text(insights?.MomentumStatus ?? "Stable").FontSize(14).Bold();
                        });
                    });
                    column.Item().PaddingVertical(12).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                    // ---- Site performance table ----
                    column.Item().Text("Site Risk Performance").FontSize(12).Bold();
                    column.Item().PaddingTop(2).Text("Comparative analysis across locations. Ranked by criticality; On-time % = mitigation deadlines met.").FontSize(8).FontColor(Colors.Grey.Medium);
                    column.Item().PaddingTop(4).Table(table =>
                    {
                        table.ColumnsDefinition(cd =>
                        {
                            cd.RelativeColumn(2);
                            cd.ConstantColumn(50);
                            cd.ConstantColumn(45);
                            cd.ConstantColumn(50);
                            cd.ConstantColumn(40);
                            cd.ConstantColumn(55);
                            cd.ConstantColumn(50);
                        });
                        table.Header(h =>
                        {
                            h.Cell().Padding(5).Background(Colors.Grey.Lighten4).Text("Site").Bold();
                            h.Cell().Padding(5).Background(Colors.Grey.Lighten4).AlignCenter().Text("Active").Bold();
                            h.Cell().Padding(5).Background(Colors.Grey.Lighten4).AlignCenter().Text("Critical").Bold();
                            h.Cell().Padding(5).Background(Colors.Grey.Lighten4).AlignRight().Text("Avg Score").Bold();
                            h.Cell().Padding(5).Background(Colors.Grey.Lighten4).AlignCenter().Text("Trend").Bold();
                            h.Cell().Padding(5).Background(Colors.Grey.Lighten4).AlignRight().Text("Avg Close").Bold();
                            h.Cell().Padding(5).Background(Colors.Grey.Lighten4).AlignRight().Text("On-time %").Bold();
                        });
                        foreach (var row in model.SiteRankings ?? new List<SiteRankingRowViewModel>())
                        {
                            table.Cell().Padding(4).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Text(row.SiteName);
                            table.Cell().Padding(4).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).AlignCenter().Text(row.ActiveRisks.ToString());
                            table.Cell().Padding(4).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).AlignCenter().Text(row.CriticalCount.ToString());
                            table.Cell().Padding(4).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).AlignRight().Text(row.AvgScore.ToString("F1"));
                            table.Cell().Padding(4).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).AlignCenter().Text(row.TrendUp ? "↑" : "↓");
                            table.Cell().Padding(4).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).AlignRight().Text(row.AvgCloseTimeDays + " d");
                            table.Cell().Padding(4).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).AlignRight().Text(row.OnTimeMitigationPercent + "%");
                        }
                    });
                    column.Item().PaddingVertical(12).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                    // ---- High priority risks table ----
                    column.Item().Text("High Priority Risks").FontSize(12).Bold();
                    column.Item().PaddingTop(2).Text("Items requiring immediate attention (severity, score, owner, age).").FontSize(8).FontColor(Colors.Grey.Medium);
                    column.Item().PaddingTop(4).Table(table =>
                    {
                        table.ColumnsDefinition(cd =>
                        {
                            cd.RelativeColumn(2);
                            cd.ConstantColumn(55);
                            cd.ConstantColumn(45);
                            cd.ConstantColumn(40);
                            cd.ConstantColumn(50);
                            cd.ConstantColumn(55);
                            cd.ConstantColumn(35);
                        });
                        table.Header(h =>
                        {
                            h.Cell().Padding(5).Background(Colors.Grey.Lighten4).Text("Risk").Bold();
                            h.Cell().Padding(5).Background(Colors.Grey.Lighten4).Text("Category").Bold();
                            h.Cell().Padding(5).Background(Colors.Grey.Lighten4).AlignCenter().Text("Severity").Bold();
                            h.Cell().Padding(5).Background(Colors.Grey.Lighten4).AlignRight().Text("Score").Bold();
                            h.Cell().Padding(5).Background(Colors.Grey.Lighten4).Text("Status").Bold();
                            h.Cell().Padding(5).Background(Colors.Grey.Lighten4).Text("Owner").Bold();
                            h.Cell().Padding(5).Background(Colors.Grey.Lighten4).AlignRight().Text("Days").Bold();
                        });
                        foreach (var r in model.TopRisks ?? new List<TopRiskRowViewModel>())
                        {
                            table.Cell().Padding(4).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Text(r.RiskName).FontSize(8);
                            table.Cell().Padding(4).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Text(r.Category ?? "-");
                            table.Cell().Padding(4).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).AlignCenter().Text(r.Severity ?? "-");
                            table.Cell().Padding(4).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).AlignRight().Text(r.CurrentScore?.ToString("F1") ?? "-");
                            table.Cell().Padding(4).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Text(r.Status ?? "-");
                            table.Cell().Padding(4).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Text(r.Owner ?? "Unassigned");
                            table.Cell().Padding(4).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).AlignRight().Text((r.DaysOpen?.ToString() ?? "-") + " d");
                        }
                    });

                    column.Item().PaddingTop(16);
                    column.Item().Text($"Report generated by WEB Sentro Risk Analytics. End of report.").FontSize(8).FontColor(Colors.Grey.Medium);
                });
            });
        });

        return document.GeneratePdf();
    }
}
