using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quiz_Application_College.Data;
using Quiz_Application_College.Domain;
using Quiz_Application_College.Domain.Coding;
using Quiz_Application_College.Services.Coding;
using System.ComponentModel.DataAnnotations;

namespace Quiz_Application_College.Areas.Student.Controllers.Coding
{
    [Area("Student")]
    [Authorize(AuthenticationSchemes = "StudentCookie")]
    [Route("Student/Coding/Exam")]
    public class ExamController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly ICodeRunner _codeRunner;

        public ExamController(ApplicationDbContext db, ICodeRunner codeRunner)
        {
            _db = db;
            _codeRunner = codeRunner;
        }

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
            var now = DateTimeOffset.Now;

            if (now > endAt)
            {
                // Time over, mark as submitted at end time and stamp submit audit
                attempt.SubmittedAt = endAt;
                attempt.SubmitIpAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                attempt.SubmitUserAgent = Request.Headers["User-Agent"].ToString();

                await _db.SaveChangesAsync();
                return BadRequest("Time is over for this attempt.");
            }

            // Mark attempt as submitted now and stamp submit audit
            attempt.SubmittedAt = now;
            attempt.SubmitIpAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            attempt.SubmitUserAgent = Request.Headers["User-Agent"].ToString();

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
        // POST: Run (API for "Run code" button)
        // ========================
        [HttpPost("Run")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Run(Guid quizId, string code, string language)
        {
            var spid = Spid();
            if (spid == Guid.Empty)
            {
                return Json(new RunResponse
                {
                    Success = false,
                    Message = "Invalid student."
                });
            }

            var now = DateTimeOffset.Now;

            // Check student is allowed to access this coding quiz now
            var allowed = await (from e in _db.Enrollments
                                 join q in _db.Quizzes on e.QuizId equals q.Id
                                 join s in _db.QuizSchedules on q.Id equals s.QuizId
                                 where e.StudentProfileId == spid
                                       && q.Id == quizId
                                       && q.Type == QuizType.Coding
                                       && s.StartAt <= now && now <= s.EndAt
                                 select q)
                                 .FirstOrDefaultAsync();

            if (allowed == null)
            {
                return Json(new RunResponse
                {
                    Success = false,
                    Message = "You are not allowed to run this quiz right now."
                });
            }

            if (!_codeRunner.IsEnabled)
            {
                return Json(new RunResponse
                {
                    Success = false,
                    Message = "Code execution is disabled. Configure Judge0 BaseUrl in appsettings.json."
                });
            }

            // Load coding question linked to this quiz – same as BuildVmAsync
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

            if (cq == null)
            {
                return Json(new RunResponse
                {
                    Success = false,
                    Message = "Problem is not configured yet."
                });
            }

            // All testcases: public + hidden
            var allCases = cq.TestCases
                .OrderBy(t => t.IsHidden)              // public first, then hidden
                .ThenBy(t => t.Weight)
                .ThenBy(t => t.Id)
                .ToList();

            if (!allCases.Any())
            {
                return Json(new RunResponse
                {
                    Success = true,
                    Message = "No testcases configured for this problem.",
                    Cases = new List<RunCase>()
                });
            }

            var runCases = new List<RunCase>();

            int totalWeight = 0;
            int passedWeight = 0;
            int totalCount = 0;
            int passedCount = 0;

            foreach (var tc in allCases)
            {
                var req = new CodeRunRequest
                {
                    Language = language,
                    SourceCode = code,
                    Stdin = tc.Input,
                    ExpectedOutput = tc.ExpectedOutput
                };

                CodeRunResult result;
                try
                {
                    result = await _codeRunner.RunAsync(req);
                }
                catch (Exception ex)
                {
                    return Json(new RunResponse
                    {
                        Success = false,
                        Message = "Error calling online judge: " + ex.Message
                    });
                }

                var passed = result.Succeeded &&
                             string.Equals(
                                 result.Stdout?.TrimEnd(),
                                 tc.ExpectedOutput?.TrimEnd(),
                                 StringComparison.Ordinal);

                // For hidden testcases: evaluate but DO NOT show details
                var inputForStudent = tc.IsHidden ? "" : tc.Input;
                var expectedForStudent = tc.IsHidden ? "" : tc.ExpectedOutput;
                var actualForStudent = tc.IsHidden ? "" : result.Stdout;

                runCases.Add(new RunCase
                {
                    Input = inputForStudent,
                    Expected = expectedForStudent,
                    Actual = actualForStudent,
                    Passed = passed,
                    IsHidden = tc.IsHidden
                });

                // ----- scoring accumulation -----
                var w = tc.Weight <= 0 ? 1 : tc.Weight;
                totalWeight += w;
                totalCount++;

                if (passed)
                {
                    passedWeight += w;
                    passedCount++;
                }
            }

            // ----- compute marks from testcases -----
            decimal marks = 0m;
            if (totalWeight > 0)
            {
                var maxMarks = cq.MaxMarks; // from CodeQuestion.MaxMarks
                marks = maxMarks * passedWeight / totalWeight;
                marks = Math.Round(marks, 2);
            }

            // ----- save run details into AttemptCodeItem + update Attempt score -----
            var key = StudentAttemptKey(spid);

            // 1) Get the current active (or latest) attempt for this student + quiz
            var attempt = await _db.Attempts
                .Where(a => a.QuizId == quizId && a.UserId == key)
                .OrderByDescending(a => a.StartedAt)
                .FirstOrDefaultAsync();

            if (attempt != null)
            {
                // 2) Find or create AttemptCodeItem for this Attempt + CodeQuestion
                var codeItem = await _db.AttemptCodeItems
                    .FirstOrDefaultAsync(x => x.AttemptId == attempt.Id && x.CodeQuestionId == cq.Id);

                if (codeItem == null)
                {
                    codeItem = new AttemptCodeItem
                    {
                        Id = Guid.NewGuid(),
                        AttemptId = attempt.Id,
                        CodeQuestionId = cq.Id
                    };
                    _db.AttemptCodeItems.Add(codeItem);
                }

                // 3) Store last run details
                codeItem.Language = language;
                codeItem.SourceCode = code;
                codeItem.PassedCount = passedCount;
                codeItem.TotalCount = totalCount;
                codeItem.MarksAwarded = marks;
                codeItem.LastRunAt = now;

                await _db.SaveChangesAsync();

                // 4) Recompute total score for this attempt
                var totalMarks = await _db.AttemptCodeItems
                    .Where(x => x.AttemptId == attempt.Id)
                    .SumAsync(x => x.MarksAwarded);

                attempt.Score = totalMarks;
                await _db.SaveChangesAsync();
            }

            return Json(new RunResponse
            {
                Success = true,
                Message = "Code executed on all testcases (public + hidden).",
                Cases = runCases
                // If you want, you can add Score = marks to RunResponse later for UI display
            });
        }

        // ========================
        // Helper: build VM and enforce timer
        // ========================
        private async Task<CodingExamVm?> BuildVmAsync(Guid quizId)
        {
            var spid = Spid();
            if (spid == Guid.Empty) return null;

            var now = DateTimeOffset.Now;
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

                var startedAt = now;

                // Build device fingerprint exactly like MCQ: IP + '|' + UserAgent
                var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
                var ua = Request.Headers["User-Agent"].ToString();
                var fingerprint = $"{ip}|{ua}";

                activeAttempt = new Attempt
                {
                    Id = Guid.NewGuid(),
                    QuizId = quizId,
                    UserId = key,
                    StartedAt = startedAt,

                    StartIpAddress = ip,
                    StartUserAgent = ua,

                    DeviceFingerprint = fingerprint
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

        // ===== API DTOs for "Run code" =====
        public class RunResponse
        {
            public bool Success { get; set; }
            public string Message { get; set; } = "";
            public List<RunCase> Cases { get; set; } = new();
        }

        public class RunCase
        {
            public string Input { get; set; } = "";
            public string Expected { get; set; } = "";
            public string Actual { get; set; } = "";
            public bool Passed { get; set; }
            public bool IsHidden { get; set; }
            public string StatusText { get; set; } = "";
        }

    }
}
