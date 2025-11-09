using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quiz_Application_College.Data;
using Quiz_Application_College.ViewModels;

namespace Quiz_Application_College.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Policy = "IsAdmin")]
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<IdentityUser> _userManager;

        public HomeController(ApplicationDbContext db, UserManager<IdentityUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var now = DateTimeOffset.Now;
            var todayLocal = now.Date;
            var tomorrowLocal = todayLocal.AddDays(1);

            var vm = new AdminDashboardVm
            {
                TotalQuizzes = await _db.Quizzes.CountAsync(),
                OpenSchedulesNow = await _db.QuizSchedules.CountAsync(s => s.StartAt <= now && now <= s.EndAt), // <—
                TotalEnrollments = await _db.Enrollments.CountAsync(),
                AttemptsToday = await _db.Attempts.CountAsync(a => a.StartedAt >= todayLocal && a.StartedAt < tomorrowLocal)
            };

            // Recent Enrollments (latest 10)
            // Recent Enrollments (latest 10)
            var re = await _db.Enrollments
                .Include(e => e.Quiz)
                .OrderByDescending(e => e.CreatedAt)
                .Take(10)
                .ToListAsync();

            // Filter out null/empty user IDs BEFORE hitting Identity
            var enrollUserIds = re.Select(x => x.UserId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct()
                .ToList();

            var enrollEmails = new Dictionary<string, string>();
            foreach (var id in enrollUserIds)
            {
                // id is guaranteed non-null/non-empty here
                var u = await _userManager.FindByIdAsync(id);
                if (u != null) enrollEmails[id] = string.IsNullOrWhiteSpace(u.Email) ? id : u.Email!;
            }

            // When mapping, NEVER pass a null key into TryGetValue
            vm.RecentEnrollments = re.Select(e => new AdminDashboardVm.EnrollmentRow
            {
                Id = e.Id,
                QuizId = e.QuizId,                     // keep existing property
                Email = (!string.IsNullOrWhiteSpace(e.UserId) && enrollEmails.TryGetValue(e.UserId, out var mail))
                            ? mail
                            : (e.UserId ?? "-"),
                QuizTitle = e.Quiz?.Title ?? "",
                CreatedAt = e.CreatedAt,
                Status = e.Status
            }).ToList();



            // Recent Attempts (latest 10)
            var ra = await _db.Attempts
                .Include(a => a.Quiz)
                .OrderByDescending(a => a.StartedAt)
                .Take(10)
                .ToListAsync();

            var attemptUserIds = ra.Select(x => x.UserId).Distinct().ToList();
            var attemptEmails = new Dictionary<string, string>();
            foreach (var id in attemptUserIds)
            {
                var u = await _userManager.FindByIdAsync(id);
                if (u != null) attemptEmails[id] = u.Email ?? id;
            }
            vm.RecentAttempts = ra.Select(a => new AdminDashboardVm.AttemptRow
            {
                Id = a.Id,
                QuizId = a.QuizId,                     // <— ADD THIS
                Email = attemptEmails.TryGetValue(a.UserId, out var mail) ? mail : a.UserId,
                QuizTitle = a.Quiz?.Title ?? "",
                StartedAt = a.StartedAt,
                SubmittedAt = a.SubmittedAt,
                Score = a.Score
            }).ToList();


            return View(vm);
        }
    }
}
