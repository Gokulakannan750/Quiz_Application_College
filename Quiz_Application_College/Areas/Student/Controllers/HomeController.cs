using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quiz_Application_College.Data;

namespace Quiz_Application_College.Areas.Student.Controllers
{
    [Area("Student")]
    [Authorize(AuthenticationSchemes = "StudentCookie", Roles = "Student")]
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _db;
        public HomeController(ApplicationDbContext db) => _db = db;

        public async Task<IActionResult> Index()
        {
            // StudentProfile.Id is stored in NameIdentifier claim by our Student login
            var profileId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            // Map to legacy Identity UserId (may be null for newly-imported students; we handle that)
            var legacyUserId = await _db.StudentProfiles
                                        .Where(p => p.Id == profileId)
                                        .Select(p => p.UserId)
                                        .FirstOrDefaultAsync();

            // If there is no legacy user id, counts drop to 0 gracefully
            var now = DateTimeOffset.UtcNow;

            // --- Enrolled quizzes count (legacy: Enrollment.UserId) ---
            int enrolledCount = 0;
            if (!string.IsNullOrWhiteSpace(legacyUserId))
            {
                enrolledCount = await _db.Enrollments
                                         .Where(e => e.UserId == legacyUserId)
                                         .CountAsync();
            }
            ViewBag.EnrolledCount = enrolledCount;

            // --- Open now: join enrolled quizzes to schedules active right now ---
            int openNowCount = 0;
            if (!string.IsNullOrWhiteSpace(legacyUserId))
            {
                openNowCount = await _db.Enrollments
                    .Where(e => e.UserId == legacyUserId)
                    .Join(_db.QuizSchedules,
                          e => e.QuizId,
                          s => s.QuizId,
                          (e, s) => s)
                    .Where(s => s.StartAt <= now && now <= s.EndAt)
                    .CountAsync();
            }
            ViewBag.OpenNowCount = openNowCount;

            // --- Attempts (legacy: Attempt.UserId) ---
            var attemptsQ = _db.Attempts.AsQueryable();
            if (!string.IsNullOrWhiteSpace(legacyUserId))
                attemptsQ = attemptsQ.Where(a => a.UserId == legacyUserId);
            else
                attemptsQ = attemptsQ.Where(a => false); // no legacy id => zero

            ViewBag.AttemptsCount = await attemptsQ.CountAsync();

            var resumeAttempt = await attemptsQ
                .Include(a => a.Quiz)
                .Where(a => a.SubmittedAt == null)
                .OrderByDescending(a => a.StartedAt)
                .FirstOrDefaultAsync();

            ViewBag.ResumeAttempt = resumeAttempt;

            return View("~/Areas/Student/Views/Home/Index.cshtml");
        }
    }
}
