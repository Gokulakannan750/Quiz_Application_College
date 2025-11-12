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

        private Guid Spid()
            => Guid.TryParse(User.FindFirst("spid")?.Value, out var id) ? id : Guid.Empty;

        // must match ExamController's key so counts line up
        private string StudentAttemptKey(Guid spid) => $"SP:{spid:D}";

        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var spid = Spid();
            if (spid == Guid.Empty) return RedirectToAction("Login", "Auth", new { area = "Student" });

            var key = StudentAttemptKey(spid);
            var now = DateTimeOffset.UtcNow;

            // Enrolled MCQ quizzes with an active schedule window
            var rows = await (
                from e in _db.Enrollments
                join q in _db.Quizzes on e.QuizId equals q.Id
                join s in _db.QuizSchedules on q.Id equals s.QuizId
                where e.StudentProfileId == spid
                   && q.Type == QuizType.Mcq
                   && s.StartAt <= now && now <= s.EndAt
                select new
                {
                    q.Id,
                    q.Title,
                    q.DurationMinutes,
                    s.StartAt,
                    s.EndAt,
                    s.MaxAttempts
                })
                .AsNoTracking()
                .ToListAsync();

            var quizIds = rows.Select(r => r.Id).Distinct().ToList();

            // Count attempts per quiz using the SAME key that RecordAttemptAsync writes
            var usedByQuiz = await _db.Attempts
                .Where(a => quizIds.Contains(a.QuizId) && a.UserId == key)
                .GroupBy(a => a.QuizId)
                .Select(g => new { QuizId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.QuizId, x => x.Count);

            var vm = rows.Select(r => new McqAvailableVm
            {
                QuizId = r.Id,
                Title = r.Title,
                DurationMinutes = r.DurationMinutes,
                StartAt = r.StartAt,
                EndAt = r.EndAt,
                MaxAttempts = r.MaxAttempts,
                UsedAttempts = usedByQuiz.TryGetValue(r.Id, out var c) ? c : 0
            }).OrderBy(x => x.Title).ToList();

            return View("~/Areas/Student/Views/Mcq/Dashboard/Index.cshtml", vm);
        }

        public class McqAvailableVm
        {
            public Guid QuizId { get; set; }
            public string Title { get; set; } = "";
            public int DurationMinutes { get; set; }
            public DateTimeOffset StartAt { get; set; }
            public DateTimeOffset EndAt { get; set; }
            public int MaxAttempts { get; set; }
            public int UsedAttempts { get; set; }
            public bool CanStart => UsedAttempts < MaxAttempts;
        }
    }
}
