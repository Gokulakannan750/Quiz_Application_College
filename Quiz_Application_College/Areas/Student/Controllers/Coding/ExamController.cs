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

        // ========================
        // GET: Play (show editor)
        // ========================
        [HttpGet("Play")]
        public async Task<IActionResult> Play(Guid quizId)
        {
            var vm = await BuildVmAsync(quizId);
            if (vm is null)
                return BadRequest("You are not allowed to take this Coding quiz right now.");

            return View("~/Areas/Student/Views/Coding/Exam/Play.cshtml", vm);
        }

        // ========================
        // POST: Play (final submit)
        // ========================
        [HttpPost("Play")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Play([FromForm] Guid quizId, CodingExamVm posted)
        {
            // quizId comes from query string; also fall back to posted.QuizId
            var id = quizId != Guid.Empty ? quizId : posted?.QuizId ?? Guid.Empty;
            if (id == Guid.Empty)
                return BadRequest("Invalid quiz.");

            var spid = Spid();
            if (spid == Guid.Empty)
                return BadRequest("Invalid student.");

            var key = StudentAttemptKey(spid);

            // Find active attempt (not yet submitted)
            var attempt = await _db.Attempts
                .Where(a => a.QuizId == id && a.UserId == key && a.SubmittedAt == null)
                .OrderByDescending(a => a.StartedAt)
                .FirstOrDefaultAsync();

            if (attempt == null)
            {
                // Either time is over or attempt never started correctly
                return BadRequest("Your attempt is not active anymore.");
            }

            // Check if time is already over on server side
            var quiz = await _db.Quizzes.AsNoTracking().FirstOrDefaultAsync(q => q.Id == id);
            if (quiz == null)
                return BadRequest("Quiz not found.");

            var endAt = attempt.StartedAt + TimeSpan.FromMinutes(quiz.DurationMinutes);
            if (DateTimeOffset.UtcNow > endAt)
            {
                // Time over, mark as submitted
                attempt.SubmittedAt = endAt;
                await _db.SaveChangesAsync();
                return BadRequest("Time is over for this attempt.");
            }

            // Mark attempt as submitted now
            attempt.SubmittedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync();

            // Rebuild VM (question + testcases) only for display
            var vm = await BuildVmAsync(id);
            if (vm is null)
                return BadRequest("You are not allowed to take this Coding quiz right now.");

            // Copy submitted code + language into result
            vm.Language = posted.Language;
            vm.Code = posted.Code;

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

        // ========================
        // Helper: build VM and enforce timer
        // ========================
        private async Task<CodingExamVm?> BuildVmAsync(Guid quizId)
        {
            var spid = Spid();
            if (spid == Guid.Empty) return null;

            var now = DateTimeOffset.UtcNow;
            var key = StudentAttemptKey(spid);

            // Enrollment + schedule + quiz type = Coding
            var scheduleInfo = await (from e in _db.Enrollments
                                      join q in _db.Quizzes on e.QuizId equals q.Id
                                      join s in _db.QuizSchedules on q.Id equals s.QuizId
                                      where e.StudentProfileId == spid
                                            && q.Id == quizId
                                            && q.Type == QuizType.Coding
                                            && s.StartAt <= now && now <= s.EndAt
                                      select new { Quiz = q, Schedule = s })
                                     .FirstOrDefaultAsync();

            if (scheduleInfo == null)
                return null;

            var quiz = scheduleInfo.Quiz;
            var durationMinutes = quiz.DurationMinutes;

            // Look for active attempt (not submitted)
            var activeAttempt = await _db.Attempts
                .Where(a => a.QuizId == quizId && a.UserId == key && a.SubmittedAt == null)
                .OrderByDescending(a => a.StartedAt)
                .FirstOrDefaultAsync();

            // Count completed attempts
            var completedAttempts = await _db.Attempts
                .Where(a => a.QuizId == quizId && a.UserId == key && a.SubmittedAt != null)
                .CountAsync();

            var maxAttempts = scheduleInfo.Schedule.MaxAttempts <= 0
                ? 1
                : scheduleInfo.Schedule.MaxAttempts;

            if (activeAttempt == null)
            {
                // No active attempt – can we start a new one?
                if (completedAttempts >= maxAttempts)
                    return null;

                activeAttempt = new Attempt
                {
                    Id = Guid.NewGuid(),
                    QuizId = quizId,
                    UserId = key,
                    StartedAt = now
                };

                _db.Attempts.Add(activeAttempt);
                await _db.SaveChangesAsync();
            }

            // Compute remaining time based on StartedAt
            var endAt = activeAttempt.StartedAt + TimeSpan.FromMinutes(durationMinutes);
            var remaining = (int)Math.Ceiling((endAt - now).TotalSeconds);

            if (remaining <= 0)
            {
                // Time over – mark attempt as finished and block further play
                activeAttempt.SubmittedAt = endAt;
                await _db.SaveChangesAsync();
                return null;
            }

            // Load coding question linked to this quiz
            var cq = await (from qq in _db.QuizCodingQuestions
                            join c in _db.CodeQuestions on qq.CodeQuestionId equals c.Id
                            where qq.QuizId == quizId
                            orderby qq.Order
                            select c)
                           .Include(c => c.TestCases)
                           .AsNoTracking()
                           .FirstOrDefaultAsync();

            // FALLBACK: if quiz is not linked yet, use first CodeQuestion in DB
            if (cq == null)
            {
                cq = await _db.CodeQuestions
                    .Include(c => c.TestCases)
                    .AsNoTracking()
                    .OrderBy(c => c.Title)
                    .FirstOrDefaultAsync();
            }

            var vm = new CodingExamVm
            {
                QuizId = quiz.Id,
                QuizTitle = quiz.Title,
                DurationMinutes = durationMinutes,
                RemainingSeconds = remaining,
                ProblemTitle = cq?.Title ?? "Problem",
                Questions = cq?.Questions ?? "Write a program to solve the problem.",
                Language = "cpp",
                Code = ""
            };

            // Only non-hidden test cases are shown to students
            if (cq?.TestCases != null)
            {
                vm.SampleCases = cq.TestCases
                    .Where(t => !t.IsHidden)          // IsHidden = 0 → public
                    .OrderBy(t => t.Weight).ThenBy(t => t.Id)
                    .Select(t => new CodingExamVm.Sample
                    {
                        Input = t.Input,
                        ExpectedOutput = t.ExpectedOutput
                    })
                    .ToList();
            }

            return vm;
        }

        // ===== VMs =====

        public class CodingExamVm
        {
            [Required] public Guid QuizId { get; set; }
            public string QuizTitle { get; set; } = "";
            public int DurationMinutes { get; set; }

            // Remaining seconds for this attempt (used by client timer)
            public int RemainingSeconds { get; set; }

            public string ProblemTitle { get; set; } = "Problem";
            public string Questions { get; set; } = ""; // shown in UI
            public string Language { get; set; } = "cpp";
            public string Code { get; set; } = "";

            public List<Sample> SampleCases { get; set; } = new();

            public class Sample
            {
                public string Input { get; set; } = "";
                public string ExpectedOutput { get; set; } = "";
            }
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
