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

            // total enrolled
            var enrolledCount = await _db.Enrollments
                .CountAsync(e => e.UserId == userId && e.Status == "Active");

            // count of open schedules where the student still has attempts remaining
            var openWithRemaining = await (
                from e in _db.Enrollments
                join s in _db.QuizSchedules on e.QuizId equals s.QuizId
                where e.UserId == userId
                      && e.Status == "Active"
                      && s.StartAt <= now && now <= s.EndAt
                select new
                {
                    s.MaxAttempts,
                    Used = _db.Attempts.Count(a =>
                        a.UserId == userId &&
                        a.QuizId == e.QuizId &&
                        a.StartedAt >= s.StartAt &&
                        a.StartedAt <= s.EndAt)
                })
                .Where(x => x.Used < x.MaxAttempts)
                .CountAsync();

            // total attempts (all time)
            var attemptsCount = await _db.Attempts.CountAsync(a => a.UserId == userId);

            // keep this if your view uses it (safe even if you don't show a resume banner)
            var pendingAttempt = await _db.Attempts
                .Include(a => a.Quiz)
                .Where(a => a.UserId == userId && a.SubmittedAt == null)
                .OrderByDescending(a => a.StartedAt)
                .FirstOrDefaultAsync();

            ViewBag.EnrolledCount = enrolledCount;
            ViewBag.OpenNowCount = openWithRemaining; // ✅ now reflects remaining attempts
            ViewBag.AttemptsCount = attemptsCount;
            ViewBag.PendingAttempt = pendingAttempt;

            return View();
        }

    }
}
