using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quiz_Application_College.Data;

namespace Quiz_Application_College.Areas.Student.Controllers
{
    [Area("Student")]
    [Authorize(Roles = "Student")]
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _db;
        public HomeController(ApplicationDbContext db) => _db = db;

        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value;
            var now = DateTimeOffset.Now;

            var enrolledCount = await _db.Enrollments.CountAsync(e => e.UserId == userId && e.Status == "Active");
            var openNowCount = await (from e in _db.Enrollments
                                      join s in _db.QuizSchedules on e.QuizId equals s.QuizId
                                      where e.UserId == userId && e.Status == "Active"
                                            && s.StartAt <= now && now <= s.EndAt
                                      select s).CountAsync();
            var attemptsCount = await _db.Attempts.CountAsync(a => a.UserId == userId);
            var pendingAttempt = await _db.Attempts
                .Include(a => a.Quiz)
                .Where(a => a.UserId == userId && a.SubmittedAt == null)
                .OrderByDescending(a => a.StartedAt)
                .FirstOrDefaultAsync();

            ViewBag.EnrolledCount = enrolledCount;
            ViewBag.OpenNowCount = openNowCount;
            ViewBag.AttemptsCount = attemptsCount;
            ViewBag.PendingAttempt = pendingAttempt;

            return View();
        }
    }
}
