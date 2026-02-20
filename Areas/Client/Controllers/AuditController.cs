using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;

namespace Web_Sentro.Areas.Client.Controllers
{
    [Area("Client")]
    [Authorize]
    public class AuditController : Controller
    {
        // GET: /Client/Audit/Index
        public IActionResult Index()
        {
            var logs = new List<dynamic> {
                new { Id = 1001, User = "Admin_Mark", Action = "Approved Expense", Module = "Finance", Details = "Approved #EXP-2026-001 ($15,400.00)", Timestamp = DateTime.Now.AddMinutes(-15), IpAddress = "192.168.1.45", Status = "Success" },
                new { Id = 1002, User = "Site_Eng_Jane", Action = "Updated Stock", Module = "Inventory", Details = "Reduced Portland Cement by 50 bags", Timestamp = DateTime.Now.AddHours(-2), IpAddress = "192.168.1.88", Status = "Success" },
                new { Id = 1003, User = "Foreman_Mike", Action = "Deleted Record", Module = "Archive", Details = "Moved Diesel Fuel Invoice to Archive", Timestamp = DateTime.Now.AddHours(-5), IpAddress = "192.168.1.12", Status = "Warning" },
                new { Id = 1004, User = "System", Action = "Failed Login", Module = "Auth", Details = "Unauthorized access attempt from unknown IP", Timestamp = DateTime.Now.AddDays(-1), IpAddress = "104.22.11.5", Status = "Critical" }
            };

            return View(logs);
        }
    }
}