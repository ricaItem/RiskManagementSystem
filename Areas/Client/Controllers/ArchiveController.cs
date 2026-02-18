using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Web_Sentro.Areas.Client.Controllers
{
    [Area("Client")]
    public class ArchiveController : Controller
    {
        // GET: /Client/Archive/Index
        public IActionResult Index()
        {
            // Mocking the "Deleted" records
            var archivedItems = new List<dynamic> {
                new {
                    Id = 104,
                    Description = "Diesel for Generators",
                    Category = "Fuel",
                    ProjectName = "Bridge Rehab Q1",
                    Vendor = "Shell Fleet",
                    Amount = 950.00,
                    DeletedAt = DateTime.Now.AddHours(-5),
                    Module = "Expenses"
                },
                new {
                    Id = 5,
                    Description = "Old Plywood Sheets",
                    Category = "Materials",
                    ProjectName = "Skyline Residency",
                    Vendor = "Local Lumber",
                    Amount = 300.00,
                    DeletedAt = DateTime.Now.AddDays(-1),
                    Module = "Inventory"
                }
            };

            return View(archivedItems);
        }

        // Action for the Restore button
        [HttpPost]
        public IActionResult Restore(int id)
        {
            // In a mock setup, we just redirect back to the index.
            // In a real app, this is where you'd flip the 'IsDeleted' bit in SQL.
            return RedirectToAction(nameof(Index));
        }

        // Action for the Permanent Delete button
        [HttpPost]
        public IActionResult PermanentDelete(int id)
        {
            // Mock logic: just refresh the page
            return RedirectToAction(nameof(Index));
        }
    }
}