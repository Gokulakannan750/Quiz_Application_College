using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quiz_Application_College.Data;
using Quiz_Application_College.Domain;

namespace Quiz_Application_College.Areas.Student.Controllers.Coding
{
    [Area("Student")]
    [Authorize(AuthenticationSchemes = "StudentCookie")]
    [Route("Student/Coding")]
    public class AttemptController : Controller
    {
        private readonly ApplicationDbContext _db;
        public AttemptController(ApplicationDbContext db) => _db = db;

        private Guid GetStudentProfileId()
            => Guid.TryParse(User.FindFirst("spid")?.Value, out var id) ? id : Guid.Empty;

        // GET /Student/Coding/Start?quizId=...
        [HttpGet("Start")]
        public async Task<IActionResult> Start(Guid quizId)
        {
            var spid = GetStudentProfileId();
            if (spid == Guid.Empty) return RedirectToAction("Login", "Auth", new { area = "Student" });

            // 1) Validate enrollment + schedule window
            var now = DateTimeOffset.UtcNow;

            var ok = await (from e in _db.Enrollments
                            join q in _db.Quizzes on e.QuizId equals q.Id
                            join s in _db.QuizSchedules on q.Id equals s.QuizId
                            where e.StudentProfileId == spid
                               && q.Id == quizId
                               && q.Type == QuizType.Coding
                               && s.StartAt <= now && now <= s.EndAt
                            select 1).AnyAsync();

            if (!ok) return BadRequest("You are not allowed to start this coding quiz right now.");

            // TODO: enforce MaxAttempts here if you already store attempt rows

            // Go to the Take page (temporary placeholder)
            return RedirectToAction("Index", "Take", new { area = "Student", quizId });
        }
        // TEMP: quick health check
        [HttpGet("Ping")]
        public IActionResult Ping() => Content("CODING OK");
    }
}
