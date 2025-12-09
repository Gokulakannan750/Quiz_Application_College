using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quiz_Application_College.Data;
using Quiz_Application_College.Domain;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace Quiz_Application_College.Areas.Student.Controllers.Mcq
{
    [Area("Student")]
    [Authorize(AuthenticationSchemes = "StudentCookie")]
    [Route("Student/MCQ/Exam")]
    public class ExamController : Controller
    {
        private readonly ApplicationDbContext _db;
        public ExamController(ApplicationDbContext db) => _db = db;

        private Guid Spid() =>
            Guid.TryParse(User.FindFirst("spid")?.Value, out var id) ? id : Guid.Empty;

        private string StudentAttemptKey(Guid spid) => $"SP:{spid:D}";

        // ========= PLAY (GET) =========
        [HttpGet("Play")]
        public async Task<IActionResult> Play(Guid quizId)
        {
            var vm = await BuildVmAsync(quizId);
            if (vm is null)
                return BadRequest("You are not allowed to take this MCQ quiz right now or the time is over.");

            return View("~/Areas/Student/Views/Mcq/Exam/Play.cshtml", vm);
        }

        // ========= PLAY (POST) – DIRECT SUBMIT ONLY =========
        [HttpPost("Play")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Play([FromForm] Guid quizId, McqExamVm posted)
        {
            var id = quizId != Guid.Empty ? quizId : posted?.QuizId ?? Guid.Empty;

            // Rebuild VM (questions, title, etc.) and also enforce schedule + attempts
            var vm = await BuildVmAsync(id);
            if (vm is null)
                return BadRequest("You are not allowed to take this MCQ quiz right now or the time is over.");

            // carry over user's choices into the fresh VM
            MergeSelections(vm, posted);

            // Before scoring, re-check attempts in case user already used them
            if (!await HasAttemptsLeftAsync(id))
                return BadRequest("You have already used all attempts for this quiz.");

            // Compute result (for display)
            var result = await ComputeResultAsync(vm);

            // Save detailed result in DB (Attempt.Score + AttemptItems)
            await SaveAttemptResultsAsync(id, vm);

            // Mark attempt as submitted (use existing active attempt if any)
            await RecordAttemptAsync(id);

            return View("~/Areas/Student/Views/Mcq/Exam/Result.cshtml", result);
        }

        // ========= Helpers =========

        /// <summary>
        /// Build the VM only if: enrolled + schedule window + attempts left.
        /// Also: create or reuse an active Attempt for timer enforcement.
        /// </summary>
        private async Task<McqExamVm?> BuildVmAsync(Guid quizId)
        {
            var spid = Spid();
            if (spid == Guid.Empty) return null;

            var now = DateTimeOffset.Now;

            // Validate enrollment + schedule + type
            var allowed = await (from e in _db.Enrollments
                                 join q in _db.Quizzes on e.QuizId equals q.Id
                                 join s in _db.QuizSchedules on q.Id equals s.QuizId
                                 where e.StudentProfileId == spid
                                       && q.Id == quizId
                                       && q.Type == QuizType.Mcq
                                       && s.StartAt <= now && now <= s.EndAt
                                 select 1).AnyAsync();

            if (!allowed) return null;

            // Check attempts left (only counts submitted attempts)
            if (!await HasAttemptsLeftAsync(quizId)) return null;

            // Load quiz
            var quiz = await _db.Quizzes.AsNoTracking().FirstOrDefaultAsync(q => q.Id == quizId);
            if (quiz == null) return null;

            // Get or create an active Attempt for this quiz and student
            var attempt = await GetOrCreateActiveAttemptAsync(quizId);
            if (attempt == null) return null;

            // Compute remaining time based on Attempt.StartedAt
            var examEnd = attempt.StartedAt.AddMinutes(quiz.DurationMinutes);
            var secondsLeft = (int)Math.Ceiling((examEnd - now).TotalSeconds);

            // IMPORTANT: do NOT block when time is over.
            // We just clamp at zero; the client timer will auto-submit.
            if (secondsLeft < 0)
            {
                secondsLeft = 0;
            }

            // Load questions in quiz order
            var qids = await _db.QuizQuestions
                .Where(qq => qq.QuizId == quizId)
                .OrderBy(qq => qq.Order)
                .Select(qq => qq.QuestionId)
                .ToListAsync();

            var questions = await _db.McqQuestions
                .Include(q => q.Options)
                .Where(q => qids.Contains(q.Id))
                .AsNoTracking()
                .ToListAsync();

            var ordered = qids.Select(id => questions.First(x => x.Id == id)).ToList();

            var vm = new McqExamVm
            {
                QuizId = quiz.Id,
                QuizTitle = quiz.Title,
                DurationMinutes = quiz.DurationMinutes,
                RemainingSeconds = secondsLeft,
                Items = ordered.Select((q, i) => new McqExamVm.Item
                {
                    Index = i + 1,
                    QuestionId = q.Id,
                    Text = q.Text,
                    Options = q.Options
                        .OrderBy(o => o.Id)
                        .Select(o => new McqExamVm.Option { OptionId = o.Id, Text = o.Text })
                        .ToList()
                }).ToList()
            };

            return vm;
        }

        /// <summary>
        /// Returns true if the student still has attempts left (counts only submitted attempts).
        /// </summary>
        private async Task<bool> HasAttemptsLeftAsync(Guid quizId)
        {
            var spid = Spid();
            if (spid == Guid.Empty) return false;

            var key = StudentAttemptKey(spid);
            var now = DateTimeOffset.Now;

            // MaxAttempts from the active schedule window
            var maxAttempts = await _db.QuizSchedules
                .Where(s => s.QuizId == quizId && s.StartAt <= now && now <= s.EndAt)
                .Select(s => (int?)s.MaxAttempts)
                .FirstOrDefaultAsync() ?? 1;

            // Count only completed attempts (SubmittedAt not null)
            var usedAttempts = await _db.Attempts
                .Where(a => a.QuizId == quizId && a.UserId == key && a.SubmittedAt != null)
                .CountAsync();

            return usedAttempts < maxAttempts;
        }

        private async Task<Attempt?> GetOrCreateActiveAttemptAsync(Guid quizId)
        {
            var spid = Spid();
            if (spid == Guid.Empty) return null;

            var key = StudentAttemptKey(spid);

            // Try to find an existing active attempt
            var existing = await _db.Attempts
                .Where(a => a.QuizId == quizId && a.UserId == key && a.SubmittedAt == null)
                .OrderByDescending(a => a.StartedAt)
                .FirstOrDefaultAsync();

            if (existing != null)
                return existing;

            // No active attempt yet → create a new one that starts now
            var now = DateTimeOffset.Now;

            var startIp = HttpContext.Connection.RemoteIpAddress?.ToString();
            var startUa = Request.Headers["User-Agent"].ToString();

            var attempt = new Attempt
            {
                Id = Guid.NewGuid(),
                QuizId = quizId,
                UserId = key,
                StartedAt = now,
                // audit – start snapshot
                StartIpAddress = startIp,
                StartUserAgent = startUa,
                DeviceFingerprint = $"{startIp}|{startUa}"
            };

            _db.Attempts.Add(attempt);
            await _db.SaveChangesAsync();

            return attempt;
        }

        private async Task RecordAttemptAsync(Guid quizId)
        {
            var spid = Spid();
            if (spid == Guid.Empty) return;

            var key = StudentAttemptKey(spid);

            var attempt = await _db.Attempts
                .Where(a => a.QuizId == quizId && a.UserId == key && a.SubmittedAt == null)
                .OrderByDescending(a => a.StartedAt)
                .FirstOrDefaultAsync();

            var now = DateTimeOffset.Now;
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var ua = Request.Headers["User-Agent"].ToString();

            if (attempt == null)
            {
                // Safety: if no active attempt, create and immediately mark as submitted
                attempt = new Attempt
                {
                    Id = Guid.NewGuid(),
                    QuizId = quizId,
                    UserId = key,
                    StartedAt = now,
                    StartIpAddress = ip,
                    StartUserAgent = ua
                };
                _db.Attempts.Add(attempt);
            }

            // Ensure start info is sensible (fix old -1 rows if they occur again)
            if (string.IsNullOrEmpty(attempt.StartIpAddress) || attempt.StartIpAddress == "-1")
                attempt.StartIpAddress = ip;
            if (string.IsNullOrEmpty(attempt.StartUserAgent))
                attempt.StartUserAgent = ua;

            // Always set submit info
            attempt.SubmittedAt = now;
            attempt.SubmitIpAddress = ip;
            attempt.SubmitUserAgent = ua;

            // Always ensure fingerprint is filled
            if (string.IsNullOrEmpty(attempt.DeviceFingerprint))
            {
                var fpIp = attempt.StartIpAddress ?? ip;
                var fpUa = attempt.StartUserAgent ?? ua;
                attempt.DeviceFingerprint = $"{fpIp}|{fpUa}";
            }

            await _db.SaveChangesAsync();
        }

        private static void MergeSelections(McqExamVm target, McqExamVm source)
        {
            // If nothing posted, nothing to merge
            if (source?.Items == null || target?.Items == null)
                return;

            var count = Math.Min(target.Items.Count, source.Items.Count);

            for (int i = 0; i < count; i++)
            {
                var postedItem = source.Items[i];
                var targetItem = target.Items[i];

                // Only copy if student actually selected something
                if (postedItem.SelectedOptionId != Guid.Empty)
                {
                    targetItem.SelectedOptionId = postedItem.SelectedOptionId;
                }
            }
        }

        private async Task<McqResultVm> ComputeResultAsync(McqExamVm vm)
        {
            var selected = vm.Items
                .Select(i => i.SelectedOptionId)
                .Where(id => id != Guid.Empty)
                .ToList();

            var correctIds = await _db.McqOptions
                .Where(o => selected.Contains(o.Id) && o.IsCorrect)
                .Select(o => o.Id)
                .ToListAsync();

            int total = vm.Items.Count;
            int correct = vm.Items.Count(i => i.SelectedOptionId != Guid.Empty && correctIds.Contains(i.SelectedOptionId));

            return new McqResultVm
            {
                QuizId = vm.QuizId,
                QuizTitle = vm.QuizTitle,
                Total = total,
                Correct = correct,
                Items = vm.Items.Select((i, idx) => new McqResultVm.Item
                {
                    Number = idx + 1,
                    QuestionText = i.Text,
                    SelectedText = i.Options.FirstOrDefault(o => o.OptionId == i.SelectedOptionId)?.Text ?? "(no answer)",
                    IsCorrect = correctIds.Contains(i.SelectedOptionId)
                }).ToList()
            };
        }

        /// <summary>
        /// Persist per-question results (AttemptItem) and total score (Attempt.Score) for this attempt.
        /// </summary>
        private async Task SaveAttemptResultsAsync(Guid quizId, McqExamVm vm)
        {
            var spid = Spid();
            if (spid == Guid.Empty || vm == null) return;

            var key = StudentAttemptKey(spid);

            // Find the active attempt (not yet submitted)
            var attempt = await _db.Attempts
                .Where(a => a.QuizId == quizId && a.UserId == key && a.SubmittedAt == null)
                .OrderByDescending(a => a.StartedAt)
                .FirstOrDefaultAsync();

            if (attempt == null)
            {
                // Safety: nothing to persist
                return;
            }

            // Load quiz for negative marking rules
            var quiz = await _db.Quizzes
                .AsNoTracking()
                .FirstOrDefaultAsync(q => q.Id == quizId);

            if (quiz == null) return;

            // Collect question ids from the VM
            var questionIds = vm.Items.Select(i => i.QuestionId).ToList();
            if (!questionIds.Any()) return;

            // Load marks for each question
            var questions = await _db.McqQuestions
                .Where(q => questionIds.Contains(q.Id))
                .Select(q => new { q.Id, q.Marks })
                .ToListAsync();

            // Force dictionary value type to decimal, default 0 if null
            var marksMap = questions.ToDictionary(
                x => x.Id,
                x => x.Marks is decimal m ? m : 0m);

            // Load correct option id for each question
            var correctOptions = await _db.McqOptions
                .Where(o => questionIds.Contains(o.QuestionId) && o.IsCorrect)
                .Select(o => new { o.QuestionId, o.Id })
                .ToListAsync();

            var correctMap = correctOptions.ToDictionary(x => x.QuestionId, x => (Guid?)x.Id);

            // Remove any existing AttemptItems for this attempt (safety)
            var existingItems = await _db.AttemptItems
                .Where(ai => ai.AttemptId == attempt.Id)
                .ToListAsync();

            if (existingItems.Count > 0)
            {
                _db.AttemptItems.RemoveRange(existingItems);
            }

            decimal totalScore = 0m;

            decimal negative = quiz.NegativeMarkPerWrong is decimal v ? v : 0m;
            bool negativeEnabled = quiz.EnableNegativeMarking && negative > 0m;

            foreach (var item in vm.Items)
            {
                marksMap.TryGetValue(item.QuestionId, out var baseMarks); // decimal
                correctMap.TryGetValue(item.QuestionId, out var correctOptionId);

                Guid? selectedOptionId = item.SelectedOptionId == Guid.Empty
                    ? (Guid?)null
                    : item.SelectedOptionId;

                decimal earned = 0m;

                if (selectedOptionId.HasValue)
                {
                    // Correct answer → full marks
                    if (correctOptionId.HasValue && selectedOptionId.Value == correctOptionId.Value)
                    {
                        earned = baseMarks;
                    }
                    // Wrong answer with negative marking enabled
                    else if (negativeEnabled)
                    {
                        earned = -negative;
                    }
                }

                totalScore += earned;

                // Option order as displayed to the student
                var optionOrder = item.Options
                    .Select(o => o.OptionId)
                    .ToList();

                var optionOrderJson = JsonSerializer.Serialize(optionOrder);

                var attemptItem = new AttemptItem
                {
                    AttemptId = attempt.Id,
                    QuestionId = item.QuestionId,
                    Order = item.Index,            // 1-based index
                    OptionOrderJson = optionOrderJson,
                    MarksAwarded = earned
                };

                _db.AttemptItems.Add(attemptItem);
            }

            // Save total score on the Attempt
            attempt.Score = totalScore;

            await _db.SaveChangesAsync();
        }

        // ========= VMs =========

        public class McqExamVm
        {
            [Required] public Guid QuizId { get; set; }
            public string QuizTitle { get; set; } = "";
            public int DurationMinutes { get; set; }

            // remaining seconds from server, used by front-end timer
            public int RemainingSeconds { get; set; }

            public List<Item> Items { get; set; } = new();

            public class Item
            {
                public int Index { get; set; }
                public Guid QuestionId { get; set; }
                public string Text { get; set; } = "";
                public Guid SelectedOptionId { get; set; } = Guid.Empty;
                public List<Option> Options { get; set; } = new();
            }

            public class Option
            {
                public Guid OptionId { get; set; }
                public string Text { get; set; } = "";
            }
        }

        public class McqResultVm
        {
            public Guid QuizId { get; set; }
            public string QuizTitle { get; set; } = "";
            public int Total { get; set; }
            public int Correct { get; set; }
            public List<Item> Items { get; set; } = new();

            public class Item
            {
                public int Number { get; set; }
                public string QuestionText { get; set; } = "";
                public string SelectedText { get; set; } = "";
                public bool IsCorrect { get; set; }
            }
        }
    }
}
