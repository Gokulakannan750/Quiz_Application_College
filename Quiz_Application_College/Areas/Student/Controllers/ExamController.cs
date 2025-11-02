using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quiz_Application_College.Data;
using Quiz_Application_College.Domain;
using Quiz_Application_College.ViewModels;
using System.Text.Json;

namespace Quiz_Application_College.Areas.Student.Controllers
{
    [Area("Student")]
    [Authorize(Roles = "Student")]
    public class ExamController : Controller
    {
        private readonly ApplicationDbContext _db;
        public ExamController(ApplicationDbContext db) => _db = db;

        private DateTimeOffset GetEndAt(Attempt a) =>
            a.StartedAt.AddMinutes(a.Quiz!.DurationMinutes);

        private async Task<bool> AutoSubmitIfExpiredAsync(Attempt attempt)
        {
            if (attempt.SubmittedAt != null) return false;

            var now = DateTimeOffset.Now;
            var endAt = GetEndAt(attempt);
            if (now <= endAt) return false;

            // Time over → score and submit
            var qids = await _db.QuizQuestions
                .Where(qq => qq.QuizId == attempt.QuizId)
                .Select(qq => qq.QuestionId)
                .ToListAsync();

            var qs = await _db.McqQuestions
                .Where(q => qids.Contains(q.Id))
                .Include(q => q.Options)
                .ToListAsync();

            var resps = await _db.Responses
                .Where(r => r.AttemptId == attempt.Id)
                .ToListAsync();

            decimal score = 0m;
            bool negOn = attempt.Quiz!.EnableNegativeMarking;
            var negRate = 0.25m; // 25% of question marks

            foreach (var q in qs)
            {
                var correct = q.Options.FirstOrDefault(o => o.IsCorrect)?.Id;
                var r = resps.FirstOrDefault(x => x.QuestionId == q.Id);
                if (r != null)
                {
                    var chosen = JsonSerializer.Deserialize<Guid?>(r.ResponseJson);
                    if (chosen.HasValue)
                    {
                        if (chosen.Value == correct) score += q.Marks;
                        else if (negOn) score -= (q.Marks * negRate);
                    }
                }
            }
            if (score < 0) score = 0m;

            attempt.Score = score;
            attempt.SubmittedAt = now;
            await _db.SaveChangesAsync();
            return true;
        }

        // GET: /Student/Exam/Start?quizId=...
        public async Task<IActionResult> Start(Guid quizId)
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value;
            var now = DateTimeOffset.Now;

            var quiz = await _db.Quizzes.FindAsync(quizId);
            if (quiz == null || !quiz.IsPublished) return Forbid();

            // Open schedule?
            var schedule = await _db.QuizSchedules
                .Where(s => s.QuizId == quizId && s.StartAt <= now && now <= s.EndAt)
                .OrderBy(s => s.EndAt)
                .FirstOrDefaultAsync();
            if (schedule == null) return Forbid();

            // Resume unfinished within this window
            var existingUnsubmitted = await _db.Attempts
                .Where(a => a.QuizId == quizId
                         && a.UserId == userId
                         && a.SubmittedAt == null
                         && a.StartedAt >= schedule.StartAt
                         && a.StartedAt <= schedule.EndAt)
                .OrderByDescending(a => a.StartedAt)
                .FirstOrDefaultAsync();

            if (existingUnsubmitted != null)
                return RedirectToAction("Player", new { attemptId = existingUnsubmitted.Id });

            // Enforce MaxAttempts in window
            var used = await _db.Attempts
                .Where(a => a.QuizId == quizId
                         && a.UserId == userId
                         && a.StartedAt >= schedule.StartAt
                         && a.StartedAt <= schedule.EndAt)
                .CountAsync();

            if (used >= schedule.MaxAttempts)
            {
                TempData["Error"] = "You have reached the maximum number of attempts for this quiz.";
                return RedirectToAction("Index", "Dashboard", new { area = "Student" });
            }

            // Create attempt
            var attempt = new Attempt { QuizId = quizId, UserId = userId, StartedAt = now };
            _db.Attempts.Add(attempt);
            await _db.SaveChangesAsync();

            // Build per-attempt ordering (shuffle based on quiz toggles)
            var shuffleQ = quiz.ShuffleQuestions;
            var shuffleO = quiz.ShuffleOptions;

            var questionIds = await _db.QuizQuestions
                .Where(qq => qq.QuizId == quizId)
                .OrderBy(qq => qq.Order)
                .Select(qq => qq.QuestionId)
                .ToListAsync();

            var rng = new Random();
            if (shuffleQ)
                questionIds = questionIds.OrderBy(_ => rng.Next()).ToList();

            int order = 1;
            foreach (var qid in questionIds)
            {
                var optionIds = await _db.McqOptions
                    .Where(o => o.QuestionId == qid)
                    .Select(o => o.Id)
                    .ToListAsync();

                if (shuffleO)
                    optionIds = optionIds.OrderBy(_ => rng.Next()).ToList();

                var optionJson = JsonSerializer.Serialize(optionIds);

                _db.AttemptItems.Add(new AttemptItem
                {
                    AttemptId = attempt.Id,
                    QuestionId = qid,
                    Order = order++,
                    OptionOrderJson = optionJson
                });
            }
            await _db.SaveChangesAsync();

            return RedirectToAction("Player", new { attemptId = attempt.Id });
        }

        // GET: /Student/Exam/Player/{attemptId}
        public async Task<IActionResult> Player(Guid attemptId)
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value;

            var attempt = await _db.Attempts
                .Include(a => a.Quiz)
                .FirstOrDefaultAsync(a => a.Id == attemptId && a.UserId == userId);
            if (attempt == null) return NotFound();

            // Hard timer: if expired, auto-submit and show result
            if (await AutoSubmitIfExpiredAsync(attempt))
                return RedirectToAction(nameof(Result), new { attemptId });

            // Load items in displayed order
            var items = await _db.AttemptItems
                .Where(ai => ai.AttemptId == attemptId)
                .OrderBy(ai => ai.Order)
                .ToListAsync();

            var qIds = items.Select(i => i.QuestionId).ToList();

            var questions = await _db.McqQuestions
                .Where(q => qIds.Contains(q.Id))
                .Include(q => q.Options)
                .ToListAsync();

            var existing = await _db.Responses
                .Where(r => r.AttemptId == attempt.Id)
                .ToListAsync();

            var vm = new ExamPlayerVm
            {
                AttemptId = attempt.Id,
                QuizTitle = attempt.Quiz!.Title,
                DurationMinutes = attempt.Quiz!.DurationMinutes,
                StartedAt = attempt.StartedAt
            };

            foreach (var it in items)
            {
                var q = questions.First(x => x.Id == it.QuestionId);

                var orderIds = JsonSerializer.Deserialize<List<Guid>>(it.OptionOrderJson) ?? new();
                var optionsInOrder = orderIds
                    .Select(id => q.Options.First(o => o.Id == id))
                    .Select(o => new ExamPlayerVm.OptionVm { OptionId = o.Id, Text = o.Text })
                    .ToList();

                vm.Questions.Add(new ExamPlayerVm.QuestionVm
                {
                    QuestionId = q.Id,
                    Text = q.Text,
                    Options = optionsInOrder
                });

                var ans = existing.FirstOrDefault(r => r.QuestionId == q.Id);
                vm.Answers[q.Id] = ans != null
                    ? JsonSerializer.Deserialize<Guid?>(ans.ResponseJson)
                    : null;
            }

            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> SaveAnswer([FromBody] SaveAnswerRequest req)
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value;

            var attempt = await _db.Attempts.Include(a => a.Quiz)
                .FirstOrDefaultAsync(a => a.Id == req.AttemptId && a.UserId == userId);
            if (attempt == null) return BadRequest();

            // Hard timer: if expired, auto-submit and block further saves
            if (await AutoSubmitIfExpiredAsync(attempt))
                return StatusCode(409, new { error = "Time over. Attempt has been submitted." });

            var resp = await _db.Responses
                .FirstOrDefaultAsync(r => r.AttemptId == req.AttemptId && r.QuestionId == req.QuestionId);

            var json = JsonSerializer.Serialize(req.OptionId);

            if (resp == null)
            {
                resp = new Response
                {
                    AttemptId = req.AttemptId,
                    QuestionId = req.QuestionId,
                    Type = QuestionType.Mcq,
                    ResponseJson = json
                };
                _db.Responses.Add(resp);
            }
            else
            {
                resp.ResponseJson = json;
            }

            await _db.SaveChangesAsync();
            return Ok(new { ok = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(Guid attemptId)
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value;

            var attempt = await _db.Attempts.Include(a => a.Quiz)
                .FirstOrDefaultAsync(a => a.Id == attemptId && a.UserId == userId);
            if (attempt == null) return BadRequest();

            // Already auto-submitted?
            if (attempt.SubmittedAt != null)
                return RedirectToAction(nameof(Result), new { attemptId });

            // Hard timer: if expired, auto-submit now
            if (await AutoSubmitIfExpiredAsync(attempt))
                return RedirectToAction(nameof(Result), new { attemptId });

            // Score within time
            var qids = await _db.QuizQuestions
                .Where(qq => qq.QuizId == attempt.QuizId)
                .Select(qq => qq.QuestionId)
                .ToListAsync();

            var qs = await _db.McqQuestions
                .Where(q => qids.Contains(q.Id))
                .Include(q => q.Options)
                .ToListAsync();

            var resps = await _db.Responses
                .Where(r => r.AttemptId == attempt.Id)
                .ToListAsync();

            decimal score = 0m;
            bool negOn = attempt.Quiz!.EnableNegativeMarking;
            var negRate = 0.25m;

            foreach (var q in qs)
            {
                var correct = q.Options.FirstOrDefault(o => o.IsCorrect)?.Id;
                var r = resps.FirstOrDefault(x => x.QuestionId == q.Id);
                if (r != null)
                {
                    var chosen = JsonSerializer.Deserialize<Guid?>(r.ResponseJson);
                    if (chosen.HasValue)
                    {
                        if (chosen.Value == correct) score += q.Marks;
                        else if (negOn) score -= (q.Marks * negRate);
                    }
                }
            }
            if (score < 0) score = 0m;

            attempt.Score = score;
            attempt.SubmittedAt = DateTimeOffset.Now;
            await _db.SaveChangesAsync();

            if (attempt.Quiz!.ShowScoreOnSubmit)
                return RedirectToAction(nameof(Result), new { attemptId });

            return RedirectToAction(nameof(Submitted), new { attemptId });
        }

        public async Task<IActionResult> Result(Guid attemptId)
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value;
            var attempt = await _db.Attempts.Include(a => a.Quiz)
                .FirstOrDefaultAsync(a => a.Id == attemptId && a.UserId == userId);
            if (attempt == null) return NotFound();

            return View(attempt);
        }

        public async Task<IActionResult> Submitted(Guid attemptId)
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value;
            var attempt = await _db.Attempts.Include(a => a.Quiz)
                .FirstOrDefaultAsync(a => a.Id == attemptId && a.UserId == userId);
            if (attempt == null) return NotFound();

            return View(attempt);
        }

        public async Task<IActionResult> Review(Guid attemptId)
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value;

            var attempt = await _db.Attempts
                .Include(a => a.Quiz)
                .FirstOrDefaultAsync(a => a.Id == attemptId && a.UserId == userId);
            if (attempt == null) return NotFound();
            if (attempt.SubmittedAt == null) return Forbid();
            if (attempt.Quiz?.ShowReviewOnSubmit != true) return Forbid();

            var items = await _db.AttemptItems
                .Where(ai => ai.AttemptId == attemptId)
                .OrderBy(ai => ai.Order)
                .ToListAsync();

            var qIds = items.Select(i => i.QuestionId).ToList();
            var questions = await _db.McqQuestions
                .Where(q => qIds.Contains(q.Id))
                .Include(q => q.Options)
                .ToListAsync();

            var responses = await _db.Responses
                .Where(r => r.AttemptId == attemptId)
                .ToListAsync();

            var vm = new Quiz_Application_College.ViewModels.ExamReviewVm
            {
                QuizTitle = attempt.Quiz!.Title,
                TotalScore = attempt.Score
            };

            int n = 1;
            foreach (var it in items)
            {
                var q = questions.First(x => x.Id == it.QuestionId);
                var yourResp = responses.FirstOrDefault(r => r.QuestionId == q.Id);
                var yourChosen = yourResp != null
                    ? JsonSerializer.Deserialize<Guid?>(yourResp.ResponseJson)
                    : null;

                var correct = q.Options.FirstOrDefault(o => o.IsCorrect)?.Id;

                var orderIds = JsonSerializer.Deserialize<List<Guid>>(it.OptionOrderJson) ?? new();
                var ordered = orderIds
                    .Select(id => q.Options.First(o => o.Id == id))
                    .Select(o => new Quiz_Application_College.ViewModels.ExamReviewVm.Option { Id = o.Id, Text = o.Text })
                    .ToList();

                decimal earned = (yourChosen.HasValue && yourChosen.Value == correct) ? q.Marks : 0m;

                vm.Items.Add(new Quiz_Application_College.ViewModels.ExamReviewVm.Item
                {
                    Number = n++,
                    Question = q.Text,
                    Options = ordered,
                    YourOptionId = yourChosen,
                    CorrectOptionId = correct,
                    Marks = q.Marks,
                    Earned = earned
                });
            }

            return View(vm);
        }
    }
}
