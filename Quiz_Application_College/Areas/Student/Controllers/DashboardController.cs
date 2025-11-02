using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quiz_Application_College.Services.Student;

namespace Quiz_Application_College.Areas.Student.Controllers
{
    [Area("Student")]
    [Authorize(Roles = "Student")]
    public class DashboardController : Controller
    {
        private readonly AvailableQuizService _svc;
        public DashboardController(AvailableQuizService svc) => _svc = svc;

        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value;
            var now = DateTimeOffset.UtcNow;
            var data = await _svc.GetAvailableAsync(userId, now);
            return View(data);
        }
    }
}
