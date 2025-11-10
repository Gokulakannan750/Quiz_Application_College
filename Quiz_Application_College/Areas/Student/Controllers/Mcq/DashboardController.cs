using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quiz_Application_College.Data;
using Quiz_Application_College.Domain;

namespace Quiz_Application_College.Areas.Student.Controllers.Mcq
{
    [Area("Student")]
    [Authorize(AuthenticationSchemes = "StudentCookie")]
    [Route("Student/MCQ")]
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _db;
        public DashboardController(ApplicationDbContext db) => _db = db;

        private Guid CurrentProfileId()
            => Guid.TryParse(User.FindFirst("spid")?.Value, out var id) ? id : Guid.Empty;

        [HttpGet("")]
        [HttpGet("Dashboard")]
        public async Task<IActionResult> Index()
        {
            var spid = CurrentProfileId();
            if (spid == Guid.Empty) return RedirectToAction("Login", "Auth", new { area = "Student" });

            var now = DateTimeOffset.UtcNow;

            var model = await (from e in _db.Enrollments
                               join s in _db.QuizSchedules on e.QuizId equals s.QuizId
                               join q in _db.Quizzes on s.QuizId equals q.Id
                               where e.StudentProfileId == spid
                                     && q.Type == QuizType.Mcq
                                     && s.StartAt <= now && now <= s.EndAt
                               select new AvailableTileVm
                               {
                                   QuizId = q.Id,
                                   ScheduleId = s.Id,
                                   Title = q.Title,
                                   DurationMinutes = q.DurationMinutes,
                                   StartAt = s.StartAt,
                                   EndAt = s.EndAt,
                                   MaxAttempts = s.MaxAttempts,
                               })
                               .OrderBy(x => x.EndAt)
                               .ToListAsync();

            // UsedAttempts (count in Attempts table by quiz within schedule window)
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "";
            foreach (var x in model)
            {
                x.UsedAttempts = await _db.Attempts.CountAsync(a =>
                    a.UserId == userId && a.QuizId == x.QuizId &&
                    a.StartedAt >= x.StartAt && a.StartedAt <= x.EndAt);
            }

            model = model.Where(x => x.UsedAttempts < x.MaxAttempts).ToList();

            return View("~/Areas/Student/Views/Mcq/Dashboard/Index.cshtml", model);
        }
    }

    public class AvailableTileVm
    {
        public Guid QuizId { get; set; }
        public Guid ScheduleId { get; set; }
        public string Title { get; set; } = "";
        public int DurationMinutes { get; set; }
        public DateTimeOffset StartAt { get; set; }
        public DateTimeOffset EndAt { get; set; }
        public int MaxAttempts { get; set; }
        public int UsedAttempts { get; set; }
    }
}
