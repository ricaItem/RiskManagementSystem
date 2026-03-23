using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using WEB_Sentro.Areas.Vendor.Models;
using WEB_Sentro.Data;
using WEB_Sentro.Data.Entities;
using System.Security.Claims;
using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace WEB_Sentro.Areas.Vendor.Controllers
{
    [Area("Vendor")]
    [Authorize(Policy = "SuperAdminOnly")]
    public class SalesAndExpensesController : Controller
    {
        private readonly PlatformDbContext _db;
        private readonly IWebHostEnvironment _env;

        public SalesAndExpensesController(PlatformDbContext db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
            QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        }

        public async Task<IActionResult> Index(int? month, int? year, CancellationToken ct = default)
        {
            var model = new SalesAndExpensesIndexViewModel
            {
                SelectedMonth = month,
                SelectedYear = year
            };

            // Setup Filter Options
            var currentYear = DateTime.UtcNow.Year;
            model.YearOptions.Add(new SelectListItem { Value = "", Text = "All Years" });
            for (int y = currentYear; y >= currentYear - 5; y--)
            {
                model.YearOptions.Add(new SelectListItem { Value = y.ToString(), Text = y.ToString() });
            }

            model.MonthOptions.Add(new SelectListItem { Value = "", Text = "All Months" });
            for (int m = 1; m <= 12; m++)
            {
                model.MonthOptions.Add(new SelectListItem { Value = m.ToString(), Text = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(m) });
            }

            // Chart Title Logic
            if (year.HasValue && month.HasValue)
            {
                model.ChartTitle = $"Financial Analytics ({CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(month.Value)} {year.Value})";
            }
            else if (year.HasValue)
            {
                model.ChartTitle = $"Financial Analytics ({year.Value})";
            }
            else
            {
                model.ChartTitle = "Financial Analytics (Last 6 Months)";
            }

            // Base queries for KPIs
            var paymentsQ = _db.Payments.AsNoTracking().Where(p => p.Status == "Succeeded");
            var expensesQ = _db.PlatformExpenses.AsNoTracking().AsQueryable();

            if (year.HasValue)
            {
                paymentsQ = paymentsQ.Where(p => (p.PaidAt ?? p.CreatedAt).Year == year.Value);
                expensesQ = expensesQ.Where(e => e.ExpenseDate.Year == year.Value);
            }

            if (month.HasValue)
            {
                paymentsQ = paymentsQ.Where(p => (p.PaidAt ?? p.CreatedAt).Month == month.Value);
                expensesQ = expensesQ.Where(e => e.ExpenseDate.Month == month.Value);
            }

            // 1 & 2. Calculate Total Sales and Total Expenses based on filters
            var totalSalesCentavos = await paymentsQ.SumAsync(p => p.AmountCentavos, ct);
            var totalExpensesCentavos = await expensesQ.SumAsync(e => e.AmountCentavos, ct);
            var profitCentavos = totalSalesCentavos - totalExpensesCentavos;

            model.TotalSalesDisplay = FormatCurrency(totalSalesCentavos);
            model.TotalExpensesDisplay = FormatCurrency(totalExpensesCentavos);
            model.ProfitDisplay = FormatCurrency(profitCentavos);
            model.IsProfitPositive = profitCentavos >= 0;

            // 3. Financial Analytics (Chart data)
            List<DateTime> chartMonths = new List<DateTime>();
            
            if (year.HasValue)
            {
                // Show all 12 months of the selected year
                for (int i = 1; i <= 12; i++)
                {
                    chartMonths.Add(new DateTime(year.Value, i, 1));
                }
            }
            else
            {
                // Show last 6 months
                var now = DateTime.UtcNow;
                var sixMonthsAgo = now.AddMonths(-5);
                var startDate = new DateTime(sixMonthsAgo.Year, sixMonthsAgo.Month, 1);
                for (int i = 0; i < 6; i++)
                {
                    chartMonths.Add(startDate.AddMonths(i));
                }
            }

            var earliestChartDate = chartMonths.First();
            var latestChartDate = chartMonths.Last().AddMonths(1);

            var analyticsPayments = await _db.Payments.AsNoTracking()
                .Include(p => p.Organization)
                .Where(p => p.Status == "Succeeded" && (p.PaidAt ?? p.CreatedAt) >= earliestChartDate && (p.PaidAt ?? p.CreatedAt) < latestChartDate)
                .ToListAsync(ct);

            var analyticsExpenses = await _db.PlatformExpenses.AsNoTracking()
                .Where(e => e.ExpenseDate >= earliestChartDate && e.ExpenseDate < latestChartDate)
                .ToListAsync(ct);

            foreach (var monthDate in chartMonths)
            {
                var monthName = monthDate.ToString("MMM yyyy");
                model.AnalyticsLabels.Add(monthName);

                var monthlySalesCentavos = analyticsPayments
                    .Where(p => (p.PaidAt ?? p.CreatedAt).Year == monthDate.Year && (p.PaidAt ?? p.CreatedAt).Month == monthDate.Month)
                    .Sum(p => p.AmountCentavos);
                model.AnalyticsIncomeData.Add(monthlySalesCentavos / 100m);

                var monthlyExpenseCentavos = analyticsExpenses
                    .Where(e => e.ExpenseDate.Year == monthDate.Year && e.ExpenseDate.Month == monthDate.Month)
                    .Sum(e => e.AmountCentavos);
                model.AnalyticsExpenseData.Add(monthlyExpenseCentavos / 100m);
            }

            // 4. Recent Transactions (Top 10 combined)
            var incomeTransactions = analyticsPayments.Select(p => new TransactionViewModel
            {
                Type = "Income",
                Description = $"Payment from Org {(p.Organization?.OrgName ?? p.OrganizationId.ToString())}",
                Date = p.PaidAt ?? p.CreatedAt,
                AmountDisplay = FormatCurrency(p.AmountCentavos),
                IsPositive = true
            });

            var expenseTransactions = analyticsExpenses.Select(e => new TransactionViewModel
            {
                Type = "Expense",
                Description = e.Description,
                Date = e.ExpenseDate,
                AmountDisplay = FormatCurrency(e.AmountCentavos),
                IsPositive = false
            });

            model.RecentTransactions = incomeTransactions.Concat(expenseTransactions)
                .OrderByDescending(t => t.Date)
                .Take(10)
                .ToList();

            // 5. Load Expenses for CRUD Table
            var allExpenses = await _db.PlatformExpenses.AsNoTracking()
                .OrderByDescending(e => e.ExpenseDate)
                .ToListAsync(ct);

            model.Expenses = allExpenses.Select(e => new PlatformExpenseViewModel
            {
                Id = e.Id,
                Description = e.Description,
                Amount = e.AmountCentavos / 100m,
                ExpenseDate = e.ExpenseDate,
                AmountDisplay = FormatCurrency(e.AmountCentavos),
                DateDisplay = e.ExpenseDate.ToString("yyyy-MM-dd")
            }).ToList();

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> ExportPdf(int? month, int? year, CancellationToken ct = default)
        {
            var paymentsQ = _db.Payments.AsNoTracking().Where(p => p.Status == "Succeeded");
            var expensesQ = _db.PlatformExpenses.AsNoTracking().AsQueryable();

            string periodText = "All Time";
            if (year.HasValue && month.HasValue)
            {
                periodText = $"{CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(month.Value)} {year.Value}";
                paymentsQ = paymentsQ.Where(p => (p.PaidAt ?? p.CreatedAt).Year == year.Value && (p.PaidAt ?? p.CreatedAt).Month == month.Value);
                expensesQ = expensesQ.Where(e => e.ExpenseDate.Year == year.Value && e.ExpenseDate.Month == month.Value);
            }
            else if (year.HasValue)
            {
                periodText = $"{year.Value}";
                paymentsQ = paymentsQ.Where(p => (p.PaidAt ?? p.CreatedAt).Year == year.Value);
                expensesQ = expensesQ.Where(e => e.ExpenseDate.Year == year.Value);
            }
            else if (month.HasValue)
            {
                periodText = $"{CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(month.Value)}";
                paymentsQ = paymentsQ.Where(p => (p.PaidAt ?? p.CreatedAt).Month == month.Value);
                expensesQ = expensesQ.Where(e => e.ExpenseDate.Month == month.Value);
            }

            var totalSalesCentavos = await paymentsQ.SumAsync(p => p.AmountCentavos, ct);
            var expensesList = await expensesQ.OrderByDescending(e => e.ExpenseDate).ToListAsync(ct);
            var totalExpensesCentavos = expensesList.Sum(e => e.AmountCentavos);
            var profitCentavos = totalSalesCentavos - totalExpensesCentavos;

            var logoPath = Path.Combine(_env.WebRootPath, "images", "logoo.png");

            var pdfData = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    page.Header().Element(compose => 
                    {
                        compose.Row(row =>
                        {
                            row.RelativeItem().Column(col =>
                            {
                                col.Item().Text("Sales and Expenses Report").FontSize(20).SemiBold().FontColor(Colors.Blue.Darken2);
                                col.Item().Text($"Period: {periodText}");
                                col.Item().Text($"Generated on: {DateTime.Now:MMMM dd, yyyy}");
                            });

                            if (System.IO.File.Exists(logoPath))
                            {
                                row.ConstantItem(100).Image(logoPath);
                            }
                        });
                    });

                    page.Content().PaddingVertical(1, Unit.Centimetre).Column(col =>
                    {
                        col.Item().PaddingBottom(10).Text("Key Performance Indicators").FontSize(14).SemiBold();

                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                            });

                            table.Cell().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Column(c => {
                                c.Item().Text("Total Sales").FontSize(10).FontColor(Colors.Grey.Darken2);
                                c.Item().Text(FormatCurrency(totalSalesCentavos)).FontSize(12).SemiBold().FontColor(Colors.Green.Darken2);
                            });

                            table.Cell().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Column(c => {
                                c.Item().Text("Total Expenses").FontSize(10).FontColor(Colors.Grey.Darken2);
                                c.Item().Text(FormatCurrency(totalExpensesCentavos)).FontSize(12).SemiBold().FontColor(Colors.Red.Darken2);
                            });

                            table.Cell().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Column(c => {
                                c.Item().Text("Net Profit").FontSize(10).FontColor(Colors.Grey.Darken2);
                                c.Item().Text(FormatCurrency(profitCentavos)).FontSize(12).SemiBold().FontColor(profitCentavos >= 0 ? Colors.Green.Darken2 : Colors.Red.Darken2);
                            });
                        });

                        col.Item().PaddingTop(20).PaddingBottom(10).Text("Expenses Breakdown").FontSize(14).SemiBold();

                        if (expensesList.Any())
                        {
                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.ConstantColumn(80);
                                    columns.RelativeColumn();
                                    columns.RelativeColumn();
                                });

                                table.Header(header =>
                                {
                                    header.Cell().BorderBottom(1).Padding(5).Text("Date").SemiBold();
                                    header.Cell().BorderBottom(1).Padding(5).Text("Description").SemiBold();
                                    header.Cell().BorderBottom(1).Padding(5).AlignRight().Text("Amount").SemiBold();
                                });

                                foreach (var expense in expensesList)
                                {
                                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5).Text(expense.ExpenseDate.ToString("yyyy-MM-dd"));
                                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5).Text(expense.Description);
                                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5).AlignRight().Text(FormatCurrency(expense.AmountCentavos));
                                }
                            });
                        }
                        else
                        {
                            col.Item().Text("No expenses recorded for this period.").Italic().FontColor(Colors.Grey.Medium);
                        }
                    });

                    page.Footer().AlignCenter().Text(x =>
                    {
                        x.Span("Page ");
                        x.CurrentPageNumber();
                        x.Span(" of ");
                        x.TotalPages();
                    });
                });
            }).GeneratePdf();

            return File(pdfData, "application/pdf", $"Sales_Expenses_Report_{DateTime.Now:yyyyMMdd}.pdf");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateExpense(PlatformExpenseViewModel model, CancellationToken ct = default)
        {
            if (ModelState.IsValid)
            {
                var expense = new PlatformExpense
                {
                    Description = model.Description,
                    AmountCentavos = (long)(model.Amount * 100m),
                    ExpenseDate = model.ExpenseDate,
                    CreatedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                };

                _db.PlatformExpenses.Add(expense);
                await _db.SaveChangesAsync(ct);
                return RedirectToAction(nameof(Index));
            }

            TempData["ErrorMessage"] = "Failed to create expense. Please check your inputs.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditExpense(int id, PlatformExpenseViewModel model, CancellationToken ct = default)
        {
            if (ModelState.IsValid && id == model.Id)
            {
                var expense = await _db.PlatformExpenses.FindAsync(new object[] { id }, ct);
                if (expense != null)
                {
                    expense.Description = model.Description;
                    expense.AmountCentavos = (long)(model.Amount * 100m);
                    expense.ExpenseDate = model.ExpenseDate;
                    expense.UpdatedAt = DateTime.UtcNow;

                    await _db.SaveChangesAsync(ct);
                }
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteExpense(int id, CancellationToken ct = default)
        {
            var expense = await _db.PlatformExpenses.FindAsync(new object[] { id }, ct);
            if (expense != null)
            {
                _db.PlatformExpenses.Remove(expense);
                await _db.SaveChangesAsync(ct);
            }
            return RedirectToAction(nameof(Index));
        }

        private static string FormatCurrency(long centavos)
        {
            bool isNegative = centavos < 0;
            long absoluteCentavos = Math.Abs(centavos);
            
            string formatted = absoluteCentavos % 100 == 0
                ? $"PHP {absoluteCentavos / 100:N0}"
                : $"PHP {absoluteCentavos / 100.0:N2}";

            return isNegative ? $"-{formatted}" : formatted;
        }
    }
}