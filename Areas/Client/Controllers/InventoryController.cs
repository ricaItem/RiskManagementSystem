using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;

namespace Web_Sentro.Areas.Client.Controllers
{
    [Area("Client")]
    [Authorize]
    public class InventoryController : Controller
    {
        public IActionResult Index()
        {
            var items = new List<dynamic> {
                new { Id = 1, Name = "Portland Cement", SKU = "CONC-001", Stock = 450, Unit = "Bags", Status = "In Stock", MinLevel = 100 },
                new { Id = 2, Name = "Rebar 12mm", SKU = "STL-12MM", Stock = 85, Unit = "Pieces", Status = "Low Stock", MinLevel = 100 },
                new { Id = 3, Name = "Safety Vests", SKU = "PPE-042", Stock = 12, Unit = "Units", Status = "Out of Stock", MinLevel = 20 }
            };
            return View(items);
        }

        public IActionResult Expenses()
        {
            var expenses = new List<dynamic> {
                new { Id = 101, Description = "Heavy Equipment Rental", Category = "Operational", Amount = 15400.00, Date = DateTime.Now.AddDays(-2), Status = "Paid" },
                new { Id = 102, Description = "PNS Steel Shipment", Category = "Materials", Amount = 8920.50, Date = DateTime.Now.AddDays(-5), Status = "Pending" },
                new { Id = 103, Description = "Site Electricity Bill", Category = "Utilities", Amount = 1200.00, Date = DateTime.Now.AddDays(-10), Status = "Overdue" }
            };
            return View(expenses);
        }
    }
}