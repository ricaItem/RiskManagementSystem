using Microsoft.AspNetCore.Mvc;

namespace Web_Sentro.Areas.Client.Controllers
{
    [Area("Client")]
    public class EmployeesController : Controller
    {
        public IActionResult Index()
        {
            var employees = new List<dynamic>
            {
                new {
                    Id = 1, Name = "Aris Gatdula", Email = "aris.g@sentro.com", Role = "Project Lead",
                    Department = "Operations", Performance = 94, Status = "Active",
                    Utilization = "85%", Bio = "Veteran project lead with 10+ years in structural engineering. Specializes in rapid deployment and foundation integrity.",
                    Skills = new[] { "Project Management", "Structural Analysis", "AutoCAD" }
                },
                new {
                    Id = 2, Name = "Elena Rossi", Email = "e.rossi@sentro.com", Role = "Safety Officer",
                    Department = "HSE", Performance = 72, Status = "Warning",
                    Utilization = "0%", Bio = "HSE specialist focused on site safety audits. Currently coordinating remote safety protocols.",
                    Skills = new[] { "OSHA Certified", "Risk Assessment", "First Aid" }
                },
                new {
                    Id = 3, Name = "Jun-Jun Maliksi", Email = "jj.maliksi@sentro.com", Role = "Site Engineer",
                    Department = "Construction", Performance = 88, Status = "Active",
                    Utilization = "100%", Bio = "On-site lead for high-rise developments. Expert in concrete pouring and logistics coordination.",
                    Skills = new[] { "Logistics", "Site Surveying", "Concrete Tech" }
                },
                new {
                    Id = 4, Name = "Sarah Geronimo", Email = "sarah.g@sentro.com", Role = "Architect",
                    Department = "Design", Performance = 45, Status = "Critical",
                    Utilization = "20%", Bio = "Design lead for modern commercial spaces. Currently on reduced capacity due to external consultancy.",
                    Skills = new[] { "Revit", "BIM", "Interior Design" }
                }
            };

            return View(employees);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Deploy(string name, string email, string role, string department)
        {
            // In a real app, you would save to the DB here:
            // var newEmp = new Employee { Name = name, Email = email ... };
            // _context.Employees.Add(newEmp);
            // _context.SaveChanges();

            TempData["Alert"] = $"Successfully deployed {name} to the {department} team.";

            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult UpdateStatus(int id, string newStatus, string reason)
        {
            // 1. In a real DB: 
            // var employee = _context.Employees.Find(id);
            // employee.Status = newStatus;
            // employee.StatusChangeReason = reason;
            // _context.SaveChanges();

            TempData["Alert"] = $"Employee status updated to {newStatus}.";

            // If status is Inactive, the view logic will naturally "archive" it 
            // by filtering it out or moving it to another table.
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult UpdateEmployee(int id, string name, string email, string role, string bio)
        {
            // Real App Logic:
            // var emp = _context.Employees.Find(id);
            // emp.Name = name; emp.Email = email; ...
            // _context.SaveChanges();

            TempData["Alert"] = $"Profile for {name} updated successfully.";
            return RedirectToAction("Index");
        }
    }

}