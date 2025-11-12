using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quiz_Application_College.Data;
using Quiz_Application_College.Domain;
using System.ComponentModel.DataAnnotations;

namespace Quiz_Application_College.Areas.Student.Controllers.Coding
{
    [Area("Student")]
    [Authorize(AuthenticationSchemes = "StudentCookie")]
    [Route("Student/Coding/Exam")]
    public class ExamController : Controller
    {
        private readonly ApplicationDbContext _db;
        public ExamController(ApplicationDbContext db) => _db = db;

        private Guid Spid()
            => Guid.TryParse(User.FindFirst("spid")?.Value, out var id) ? id : Guid.Empty;

        private string StudentAttemptKey(Guid spid) => $"SP:{spid:D}";

        // GET: /Student/Coding/Exam/Play?quizId=...
        [HttpGet("Play")]
        public async Task<IActionResult> Play(Guid quizId)
        {
            var vm = await BuildVmAsync(quizId);
            if (vm is null) return BadRequest("You are not allowed to take this Coding quiz right now.");
            return View("~/Areas/Student/Views/Coding/Exam/Play.cshtml", vm);
        }

        // POST: Review / Back / Submit
        [HttpPost("Play")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Play([FromForm] Guid quizId, [FromForm] string mode, CodingExamVm posted)
        {
            var id = quizId != Guid.Empty ? quizId : posted?.QuizId ?? Guid.Empty;

            var vm = await BuildVmAsync(id);
            if (vm is null) return BadRequest("You are not allowed to take this Coding quiz right now.");

            // Carry student code selection back into fresh VM
            vm.Language = posted.Language;
            vm.Code = posted.Code;

            mode = (mode ?? "").Trim().ToLowerInvariant();

            if (mode == "review")
            {
                vm.IsReview = true;
                return View("~/Areas/Student/Views/Coding/Exam/Play.cshtml", vm);
            }
            if (mode == "back")
            {
                vm.IsReview = false;
                return View("~/Areas/Student/Views/Coding/Exam/Play.cshtml", vm);
            }
            if (mode == "submit")
            {
                if (!await HasAttemptsLeftAsync(id))
                    return BadRequest("You have already used all attempts for this quiz.");

                // Record attempt (no grading yet)
                await RecordAttemptAsync(id);

                var result = new CodingResultVm
                {
                    QuizId = vm.QuizId,
                    QuizTitle = vm.QuizTitle,
                    ProblemTitle = vm.ProblemTitle,
                    Language = vm.Language,
                    Code = vm.Code
                };

                return View("~/Areas/Student/Views/Coding/Exam/Result.cshtml", result);
            }

            return View("~/Areas/Student/Views/Coding/Exam/Play.cshtml", vm);
        }

        // ===== Helpers =====

        private async Task<CodingExamVm?> BuildVmAsync(Guid quizId)
        {
            var spid = Spid();
            if (spid == Guid.Empty) return null;

            var now = DateTimeOffset.UtcNow;

            // enrolled + coding quiz + active schedule
            var allowed = await (from e in _db.Enrollments
                                 join q in _db.Quizzes on e.QuizId equals q.Id
                                 join s in _db.QuizSchedules on q.Id equals s.QuizId
                                 where e.StudentProfileId == spid
                                    && q.Id == quizId
                                    && q.Type == QuizType.Coding
                                    && s.StartAt <= now && now <= s.EndAt
                                 select 1).AnyAsync();

            if (!allowed) return null;

            if (!await HasAttemptsLeftAsync(quizId))
                return null;

            var quiz = await _db.Quizzes.AsNoTracking().FirstOrDefaultAsync(q => q.Id == quizId);
            if (quiz == null) return null;

            // Expecting one or more coding problems attached to the quiz
            // We’ll load the first problem for now (extend to multi-problem later).
            var cq = await (from qq in _db.QuizCodingQuestions
                            join c in _db.CodeQuestions on qq.CodeQuestionId equals c.Id
                            where qq.QuizId == quizId
                            orderby qq.Order
                            select new { c.Title, c.Prompt })
                           .AsNoTracking()
                           .FirstOrDefaultAsync();

            var vm = new CodingExamVm
            {
                QuizId = quiz.Id,
                QuizTitle = quiz.Title,
                DurationMinutes = quiz.DurationMinutes,
                ProblemTitle = cq?.Title ?? "Problem",
                Prompt = cq?.Prompt ?? "Write a program to solve the problem.",
                Language = "cpp", // default – change on UI
                Code = ""
            };

            return vm;
        }

        private async Task<bool> HasAttemptsLeftAsync(Guid quizId)
        {
            var spid = Spid();
            var key = StudentAttemptKey(spid);
            var now = DateTimeOffset.UtcNow;

            var maxAttempts = await _db.QuizSchedules
                .Where(s => s.QuizId == quizId && s.StartAt <= now && now <= s.EndAt)
                .Select(s => (int?)s.MaxAttempts)
                .FirstOrDefaultAsync() ?? 1;

            var used = await _db.Attempts
                .Where(a => a.QuizId == quizId && a.UserId == key)
                .CountAsync();

            return used < maxAttempts;
        }

        private async Task RecordAttemptAsync(Guid quizId)
        {
            var spid = Spid();
            var key = StudentAttemptKey(spid);

            var attempt = new Attempt
            {
                Id = Guid.NewGuid(),
                QuizId = quizId,
                UserId = key,
                StartedAt = DateTimeOffset.UtcNow,
            };

            _db.Attempts.Add(attempt);
            await _db.SaveChangesAsync();
        }

        // ===== VMs =====

        public class CodingExamVm
        {
            [Required] public Guid QuizId { get; set; }
            public string QuizTitle { get; set; } = "";
            public int DurationMinutes { get; set; }
            public string ProblemTitle { get; set; } = "Problem";
            public string Prompt { get; set; } = "";
            public string Language { get; set; } = "cpp";
            public string Code { get; set; } = "";
            public bool IsReview { get; set; } = false;
        }

        public class CodingResultVm
        {
            public Guid QuizId { get; set; }
            public string QuizTitle { get; set; } = "";
            public string ProblemTitle { get; set; } = "";
            public string Language { get; set; } = "";
            public string Code { get; set; } = "";
        }
    }
}
