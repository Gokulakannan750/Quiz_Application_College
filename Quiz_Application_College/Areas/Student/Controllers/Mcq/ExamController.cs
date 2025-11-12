using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quiz_Application_College.Data;
using Quiz_Application_College.Domain;
using System.ComponentModel.DataAnnotations;

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

        // ========= PLAY (GET) =========
        [HttpGet("Play")]
        public async Task<IActionResult> Play(Guid quizId)
        {
            var vm = await BuildVmAsync(quizId);
            if (vm is null) return BadRequest("You are not allowed to take this MCQ quiz right now.");
            return View("~/Areas/Student/Views/Mcq/Exam/Play.cshtml", vm);
        }

        // ========= PLAY (POST) – Review / Back / Submit =========
        [HttpPost("Play")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Play([FromForm] Guid quizId, [FromForm] string mode, McqExamVm posted)
        {
            var id = quizId != Guid.Empty ? quizId : posted?.QuizId ?? Guid.Empty;

            var vm = await BuildVmAsync(id);
            if (vm is null) return BadRequest("You are not allowed to take this MCQ quiz right now.");

            // carry over user's choices
            MergeSelections(vm, posted);

            mode = (mode ?? "").Trim().ToLowerInvariant();

            if (mode == "review")
            {
                vm.IsReview = true;
                return View("~/Areas/Student/Views/Mcq/Exam/Play.cshtml", vm);
            }
            if (mode == "back")
            {
                vm.IsReview = false;
                return View("~/Areas/Student/Views/Mcq/Exam/Play.cshtml", vm);
            }
            if (mode == "submit")
            {
                // BEFORE scoring, re-check attempts in case another window already used it
                if (!await HasAttemptsLeftAsync(id))
                    return BadRequest("You have already used all attempts for this quiz.");

                var result = await ComputeResultAsync(vm);

                // RECORD the attempt so dashboard shows 1 / Max and future starts get blocked
                await RecordAttemptAsync(id);

                return View("~/Areas/Student/Views/Mcq/Exam/Result.cshtml", result);
            }

            return View("~/Areas/Student/Views/Mcq/Exam/Play.cshtml", vm);
        }

        // ========= Helpers =========

        // Build the VM only if: enrolled + schedule window + attempts left
        private async Task<McqExamVm?> BuildVmAsync(Guid quizId)
        {
            var spid = Spid();
            if (spid == Guid.Empty) return null;

            var now = DateTimeOffset.UtcNow;

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

            // Block if attempts exhausted
            if (!await HasAttemptsLeftAsync(quizId)) return null;

            var quiz = await _db.Quizzes.AsNoTracking().FirstOrDefaultAsync(q => q.Id == quizId);
            if (quiz == null) return null;

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

        // Check student's used attempts against active schedule's MaxAttempts
        private string StudentAttemptKey(Guid spid) => $"SP:{spid:D}";

        // Check student's used attempts against active schedule's MaxAttempts
        private async Task<bool> HasAttemptsLeftAsync(Guid quizId)
        {
            var spid = Spid();
            var key = StudentAttemptKey(spid);
            var now = DateTimeOffset.UtcNow;

            // MaxAttempts from the active schedule window
            var maxAttempts = await _db.QuizSchedules
                .Where(s => s.QuizId == quizId && s.StartAt <= now && now <= s.EndAt)
                .Select(s => (int?)s.MaxAttempts)
                .FirstOrDefaultAsync() ?? 1;

            // Count attempts recorded for this student (by synthetic key)
            var usedAttempts = await _db.Attempts
                .Where(a => a.QuizId == quizId && a.UserId == key)
                .CountAsync();

            return usedAttempts < maxAttempts;
        }

        // Create a minimal Attempt row so future starts are blocked and dashboard shows used attempt
        private async Task RecordAttemptAsync(Guid quizId)
        {
            var spid = Spid();
            var key = StudentAttemptKey(spid);

            var attempt = new Attempt
            {
                Id = Guid.NewGuid(),
                QuizId = quizId,
                UserId = key,
                StartedAt = DateTimeOffset.UtcNow,   // your entity has StartedAt (not CreatedAt)
            };

            _db.Attempts.Add(attempt);
            await _db.SaveChangesAsync();
        }


        private static void MergeSelections(McqExamVm target, McqExamVm source)
        {
            if (source?.Items == null) return;

            var map = source.Items
                .Where(i => i.QuestionId != Guid.Empty)
                .ToDictionary(i => i.QuestionId, i => i.SelectedOptionId);

            foreach (var item in target.Items)
            {
                if (map.TryGetValue(item.QuestionId, out var sel))
                    item.SelectedOptionId = sel;
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

        // ========= VMs =========

        public class McqExamVm
        {
            [Required] public Guid QuizId { get; set; }
            public string QuizTitle { get; set; } = "";
            public int DurationMinutes { get; set; }
            public bool IsReview { get; set; } = false;
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
