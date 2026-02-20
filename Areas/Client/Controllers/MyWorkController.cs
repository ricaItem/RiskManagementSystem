using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using WEB_Sentro.Models.Identity;
using Web_Sentro.Areas.Client.Models;

namespace WEB_Sentro.Areas.Client.Controllers
{
    [Area("Client")]
    [Authorize(Roles = "Employee")]
    public class MyWorkController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public MyWorkController(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        /// <summary>
        /// Employee dashboard: My Work — cards + task list + assigned assessments + recent alerts.
        /// All data tenant-scoped and filtered to current user (mock data for now).
        /// </summary>
        public async Task<IActionResult> Index()
        {
            ViewData["Title"] = "My Work";
            var user = await _userManager.GetUserAsync(User);
            var orgId = user?.OrganizationId ?? 0;

            // Placeholder counts and lists (tenant-scoped, user-filtered in real implementation)
            var vm = new MyWorkDashboardViewModel
            {
                MyOpenRisksCount = 0,
                MyAssignedTasksCount = 0,
                MyPendingAssessmentsCount = 0,
                MySiteAlertsCount = 0,
                MyTasks = new List<MitigationTaskViewModel>(),
                AssignedAssessments = new List<RiskAssessmentItemViewModel>(),
                MyRecentRisks = new List<RiskIdentificationViewModel>()
            };

            // Optional: inject a service that queries by user.Id and orgId for real data
            return View(vm);
        }

        /// <summary>My risks list (tenant + created-by filter). Stub until full implementation.</summary>
        [HttpGet]
        public IActionResult MyRisks() { ViewData["Title"] = "My Risks"; return View(); }

        /// <summary>My assigned tasks (tenant + assigned-to filter). Stub until full implementation.</summary>
        [HttpGet]
        public IActionResult MyTasks() { ViewData["Title"] = "My Tasks"; return View(); }

        /// <summary>Assessments assigned to me (tenant + assignee filter). Stub until full implementation.</summary>
        [HttpGet]
        public IActionResult MyAssessments() { ViewData["Title"] = "My Assessments"; return View(); }
    }

    public class MyWorkDashboardViewModel
    {
        public int MyOpenRisksCount { get; set; }
        public int MyAssignedTasksCount { get; set; }
        public int MyPendingAssessmentsCount { get; set; }
        public int MySiteAlertsCount { get; set; }
        public List<MitigationTaskViewModel> MyTasks { get; set; } = new();
        public List<RiskAssessmentItemViewModel> AssignedAssessments { get; set; } = new();
        public List<RiskIdentificationViewModel> MyRecentRisks { get; set; } = new();
    }

    public class RiskAssessmentItemViewModel
    {
        public int RiskId { get; set; }
        public string RiskTitle { get; set; } = "";
        public string Status { get; set; } = "";
        public DateTime? DueDate { get; set; }
    }
}
