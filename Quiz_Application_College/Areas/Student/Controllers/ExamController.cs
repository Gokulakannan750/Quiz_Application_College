using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quiz_Application_College.Data;
using Quiz_Application_College.Domain;
using Quiz_Application_College.ViewModels;
using System.Text.Json;
using Quiz_Application_College.ViewModels.Coding;
using Quiz_Application_College.Domain.Coding;
using Quiz_Application_College.Services.Coding;


namespace Quiz_Application_College.Areas.Student.Controllers
{
    [Area("Student")]
    [Authorize]
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

            // --- CODING QUESTIONS (attach via QuizCodingQuestions) ---
            var codingQuestions = await _db.QuizCodingQuestions
                .Include(x => x.CodeQuestion)
                .Where(x => x.QuizId == attempt.QuizId)
                .OrderBy(x => x.Order)
                .Select(x => x.CodeQuestion)
                .ToListAsync();

            ViewBag.CodingQuestions = codingQuestions;
            // --- END CODING QUESTIONS ---

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
        public async Task<IActionResult> Submit(Guid attemptId, [FromServices] ICodeRunner runner)
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value;

            var attempt = await _db.Attempts.Include(a => a.Quiz)
                .FirstOrDefaultAsync(a => a.Id == attemptId && a.UserId == userId);
            if (attempt == null) return BadRequest();

            if (attempt.SubmittedAt != null)
                return RedirectToAction(nameof(Result), new { attemptId });

            if (await AutoSubmitIfExpiredAsync(attempt))
                return RedirectToAction(nameof(Result), new { attemptId });

            // ===== MCQ scoring (existing logic) =====
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

            decimal mcqScore = 0m;
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
                        if (chosen.Value == correct) mcqScore += q.Marks;
                        else if (negOn) mcqScore -= (q.Marks * negRate);
                    }
                }
            }
            if (mcqScore < 0) mcqScore = 0m;

            // ===== Coding scoring (re-run all tests at submit to freeze marks) =====
            decimal codingTotal = 0m;

            var links = await _db.QuizCodingQuestions
                .Where(x => x.QuizId == attempt.QuizId)
                .Select(x => x.CodeQuestionId)
                .ToListAsync();

            foreach (var cqId in links)
            {
                var cq = await _db.CodeQuestions
                    .Include(x => x.TestCases)
                    .FirstOrDefaultAsync(x => x.Id == cqId);
                if (cq == null || cq.TestCases.Count == 0) continue;

                var item = await _db.AttemptCodeItems
                    .FirstOrDefaultAsync(x => x.AttemptId == attempt.Id && x.CodeQuestionId == cqId);

                var lang = string.IsNullOrWhiteSpace(item?.Language) ? "python" : item!.Language;
                var src = item?.SourceCode ?? "";

                int total = cq.TestCases.Count;
                int passed = 0;

                var totalWeight = Math.Max(1, cq.TestCases.Sum(t => t.Weight <= 0 ? 1 : t.Weight));
                int passedWeight = 0;

                if (!string.IsNullOrWhiteSpace(src))
                {
                    foreach (var t in cq.TestCases)
                    {
                        var req = new CodeRunRequest
                        {
                            Language = lang,
                            SourceCode = src,
                            Stdin = t.Input ?? "",
                            ExpectedOutput = t.ExpectedOutput ?? ""
                        };

                        var res = await runner.RunAsync(req);

                        var trimmedOut = (res.Stdout ?? string.Empty).TrimEnd('\r', '\n');
                        var trimmedExp = (t.ExpectedOutput ?? string.Empty).TrimEnd('\r', '\n');

                        bool isPass = res.Succeeded ||
                                      (string.IsNullOrWhiteSpace(res.Stderr) &&
                                       string.IsNullOrWhiteSpace(res.CompileOutput) &&
                                       string.Equals(trimmedOut, trimmedExp, StringComparison.Ordinal));

                        if (isPass)
                        {
                            passed++;
                            passedWeight += (t.Weight <= 0 ? 1 : t.Weight);
                        }
                    }
                }

                var earned = Math.Round((cq.MaxMarks <= 0 ? 0 : cq.MaxMarks) * (decimal)passedWeight / totalWeight, 2);

                if (item == null)
                {
                    item = new AttemptCodeItem
                    {
                        AttemptId = attempt.Id,
                        CodeQuestionId = cqId,
                        Language = lang,
                        SourceCode = src,
                        PassedCount = passed,
                        TotalCount = total,
                        MarksAwarded = earned,
                        LastRunAt = DateTimeOffset.UtcNow
                    };
                    _db.AttemptCodeItems.Add(item);
                }
                else
                {
                    item.PassedCount = passed;
                    item.TotalCount = total;
                    item.MarksAwarded = earned;
                    item.LastRunAt = DateTimeOffset.UtcNow;
                }

                codingTotal += earned;
            }

            attempt.Score = mcqScore + codingTotal;
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

            ViewBag.AttemptId = attemptId; // <-- used by the view’s coding section
            return View(vm);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveCode([FromBody] SaveCodeDto dto)
        {
            var attempt = await _db.Attempts
                .Include(a => a.Quiz)
                .FirstOrDefaultAsync(a => a.Id == dto.AttemptId);

            if (attempt == null || attempt.SubmittedAt != null)
                return BadRequest("Attempt not found or already submitted.");

            var item = await _db.AttemptCodeItems
                .FirstOrDefaultAsync(x => x.AttemptId == dto.AttemptId && x.CodeQuestionId == dto.CodeQuestionId);

            if (item == null)
            {
                item = new AttemptCodeItem
                {
                    AttemptId = dto.AttemptId,
                    CodeQuestionId = dto.CodeQuestionId,
                    Language = string.IsNullOrWhiteSpace(dto.Language) ? "python" : dto.Language,
                    SourceCode = dto.SourceCode ?? "",
                    PassedCount = 0,
                    TotalCount = 0,
                    MarksAwarded = 0m,
                    LastRunAt = null
                };
                _db.AttemptCodeItems.Add(item);
            }
            else
            {
                item.Language = string.IsNullOrWhiteSpace(dto.Language) ? item.Language : dto.Language;
                item.SourceCode = dto.SourceCode ?? item.SourceCode;
            }

            await _db.SaveChangesAsync();
            return Ok(new { ok = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RunCode([FromBody] RunCodeDto dto, [FromServices] ICodeRunner runner)
        {
            var attempt = await _db.Attempts.FirstOrDefaultAsync(a => a.Id == dto.AttemptId);
            if (attempt == null || attempt.SubmittedAt != null)
                return BadRequest("Attempt not found or already submitted.");

            var question = await _db.CodeQuestions
                .Include(q => q.TestCases)
                .FirstOrDefaultAsync(q => q.Id == dto.CodeQuestionId);
            if (question == null)
                return BadRequest("CodeQuestion not found.");
            if (question.TestCases.Count == 0)
                return BadRequest("No test cases configured.");

            // Ensure we have an AttemptCodeItem row
            var item = await _db.AttemptCodeItems
                .FirstOrDefaultAsync(x => x.AttemptId == dto.AttemptId && x.CodeQuestionId == dto.CodeQuestionId);
            if (item == null)
            {
                item = new AttemptCodeItem
                {
                    AttemptId = dto.AttemptId,
                    CodeQuestionId = dto.CodeQuestionId,
                    Language = string.IsNullOrWhiteSpace(dto.Language) ? "python" : dto.Language,
                    SourceCode = dto.SourceCode ?? ""
                };
                _db.AttemptCodeItems.Add(item);
                await _db.SaveChangesAsync();
            }
            else
            {
                item.Language = string.IsNullOrWhiteSpace(dto.Language) ? item.Language : dto.Language;
                item.SourceCode = dto.SourceCode ?? item.SourceCode;
                await _db.SaveChangesAsync();
            }

            // Run each test individually and aggregate results
            int total = question.TestCases.Count;
            int passed = 0;

            var totalWeight = Math.Max(1, question.TestCases.Sum(t => t.Weight <= 0 ? 1 : t.Weight));
            int passedWeight = 0;

            var compileLines = new List<string>();
            var runLines = new List<string>();

            foreach (var t in question.TestCases)
            {
                var req = new CodeRunRequest
                {
                    Language = item.Language ?? "python",
                    SourceCode = item.SourceCode ?? "",
                    Stdin = t.Input ?? "",
                    ExpectedOutput = t.ExpectedOutput ?? ""
                };

                var res = await runner.RunAsync(req);

                // consider it a pass if status Accepted OR clean stderr/compile and expected output matches trimmed stdout
                var trimmedOut = (res.Stdout ?? string.Empty).TrimEnd('\r', '\n');
                var trimmedExp = (t.ExpectedOutput ?? string.Empty).TrimEnd('\r', '\n');

                bool isPass = res.Succeeded ||
                              (string.IsNullOrWhiteSpace(res.Stderr) &&
                               string.IsNullOrWhiteSpace(res.CompileOutput) &&
                               string.Equals(trimmedOut, trimmedExp, StringComparison.Ordinal));

                if (isPass)
                {
                    passed++;
                    passedWeight += (t.Weight <= 0 ? 1 : t.Weight);
                }

                if (!string.IsNullOrWhiteSpace(res.CompileOutput))
                    compileLines.Add(res.CompileOutput);
                if (!string.IsNullOrWhiteSpace(res.Stderr))
                    runLines.Add(res.Stderr);
                if (!string.IsNullOrWhiteSpace(res.Stdout))
                    runLines.Add(res.Stdout);
            }

            // marks
            var earned = Math.Round((question.MaxMarks <= 0 ? 0 : question.MaxMarks) * (decimal)passedWeight / totalWeight, 2);

            item.PassedCount = passed;
            item.TotalCount = total;
            item.MarksAwarded = earned;
            item.LastRunAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync();

            return Json(new
            {
                passed,
                total,
                compileLog = string.Join("\n", compileLines),
                runLog = string.Join("\n", runLines)
            });
        }
    }
}
