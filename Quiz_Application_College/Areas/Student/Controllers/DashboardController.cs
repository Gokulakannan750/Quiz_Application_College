using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quiz_Application_College.Services.Student;

namespace Quiz_Application_College.Areas.Student.Controllers
{
    [Area("Student")]
    [Authorize(AuthenticationSchemes = "StudentCookie", Roles = "Student")]
    public class DashboardController : Controller
    {
        private readonly AvailableQuizService _svc;
        public DashboardController(AvailableQuizService svc) => _svc = svc;

        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value;

            // Use UTC for consistent window comparisons
            var now = DateTimeOffset.UtcNow;

            var available = await _svc.GetAvailableAsync(userId, now);
            var exhausted = await _svc.GetOpenButExhaustedAsync(userId, now);
            ViewBag.Exhausted = exhausted.Select(x => new { x.Title, x.MaxAttempts }).ToList();

            return View(available);
        }

    }
}
