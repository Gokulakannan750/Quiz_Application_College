using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quiz_Application_College.Data;
using Quiz_Application_College.Domain;

namespace Quiz_Application_College.Areas.Admin.Controllers.Coding
{
    [Area("Admin")]
    [Authorize(Policy = "IsAdmin")]
    [Route("Admin/Coding/Quiz")]
    public class CodingQuizController : Controller
    {
        private readonly ApplicationDbContext _db;
        public CodingQuizController(ApplicationDbContext db) => _db = db;

        // --------------------------------------------------------------------
        // 1) TOP LEVEL: LANGUAGE FOLDERS
        // GET /Admin/Coding/Quiz  or /Admin/Coding/Quiz/Index
        // --------------------------------------------------------------------
        [HttpGet("")]
        [HttpGet("Index")]
        public async Task<IActionResult> Index()
        {
            var items = await _db.Quizzes
                .Where(q => q.Type == QuizType.Coding)
                .GroupBy(q => q.ProgrammingLanguage ?? "Uncategorized")
                .Select(g => new LanguageFolderVm
                {
                    Name = g.Key,
                    QuizCount = g.Count(),
                    PublishedCount = g.Count(q => q.IsPublished)
                })
                .OrderBy(x => x.Name)
                .ToListAsync();

            return View("~/Areas/Admin/Views/Coding/Quiz/Folders.cshtml", items);
        }

        // --------------------------------------------------------------------
        // 2) INSIDE A FOLDER: LIST QUIZZES FOR ONE LANGUAGE
        // GET /Admin/Coding/Quiz/Language/{language}
        // --------------------------------------------------------------------
        [HttpGet("Language/{language}")]
        public async Task<IActionResult> ByLanguage(string language)
        {
            if (string.IsNullOrWhiteSpace(language))
                return RedirectToAction(nameof(Index));

            var data = await _db.Quizzes
                .Where(q => q.Type == QuizType.Coding &&
                            (q.ProgrammingLanguage ?? "Uncategorized") == language)
                .Select(q => new QuizRowVm
                {
                    Id = q.Id,
                    Title = q.Title,
                    Description = q.Description,
                    DurationMinutes = q.DurationMinutes,
                    IsPublished = q.IsPublished,
                    CreatedAt = q.CreatedAt,
                    QuestionCount = _db.QuizCodingQuestions.Count(c => c.QuizId == q.Id),
                    ScheduleCount = _db.QuizSchedules.Count(s => s.QuizId == q.Id),
                    ProgrammingLanguage = q.ProgrammingLanguage
                })
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();

            ViewBag.Language = language;
            return View("~/Areas/Admin/Views/Coding/Quiz/Index.cshtml", data);
        }

        // --------------------------------------------------------------------
        // 3) CREATE quiz inside a language folder
        // GET /Admin/Coding/Quiz/Create/{language?}
        // --------------------------------------------------------------------
        [HttpGet("Create/{language?}")]
        public IActionResult Create(string? language)
        {
            ViewBag.Language = language ?? "Uncategorized";

            var quiz = new Quiz
            {
                DurationMinutes = 60,
                TotalMarks = 0,
                EnableNegativeMarking = false,
                NegativeMarkPerWrong = 0,
                ShuffleQuestions = false,
                ShuffleOptions = false,
                ShowReviewOnSubmit = true,
                ShowScoreOnSubmit = true,
                Type = QuizType.Coding,
                ProgrammingLanguage = language
            };

            // We use the Quiz entity directly for create/edit
            return View("~/Areas/Admin/Views/Coding/Quiz/Create.cshtml", quiz);
        }

        // POST /Admin/Coding/Quiz/Create/{language?}
        [HttpPost("Create/{language?}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string? language, Quiz form)
        {
            var lang = language ?? form.ProgrammingLanguage ?? "Uncategorized";
            ViewBag.Language = lang;

            if (!ModelState.IsValid)
                return View("~/Areas/Admin/Views/Coding/Quiz/Create.cshtml", form);

            var quiz = new Quiz
            {
                Title = form.Title,
                Description = form.Description,
                DurationMinutes = form.DurationMinutes,
                TotalMarks = 0,
                EnableNegativeMarking = false,
                NegativeMarkPerWrong = 0,
                ShuffleQuestions = false,
                ShuffleOptions = false,
                ShowReviewOnSubmit = form.ShowReviewOnSubmit,
                ShowScoreOnSubmit = form.ShowScoreOnSubmit,
                IsPublished = false,
                CreatedAt = DateTimeOffset.Now,
                Type = QuizType.Coding,
                ProgrammingLanguage = lang
            };

            _db.Quizzes.Add(quiz);
            await _db.SaveChangesAsync();

            return RedirectToAction(nameof(ByLanguage), new { language = lang });
        }

        // --------------------------------------------------------------------
        // 4) EDIT quiz (keeps its language)
        // --------------------------------------------------------------------
        [HttpGet("Edit/{id:guid}")]
        public async Task<IActionResult> Edit(Guid id)
        {
            var quiz = await _db.Quizzes.FindAsync(id);
            if (quiz == null || quiz.Type != QuizType.Coding)
                return NotFound();

            ViewBag.Language = quiz.ProgrammingLanguage ?? "Uncategorized";
            return View("~/Areas/Admin/Views/Coding/Quiz/Edit.cshtml", quiz);
        }

        [HttpPost("Edit/{id:guid}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, Quiz form)
        {
            if (id != form.Id) return BadRequest();

            var quiz = await _db.Quizzes.FindAsync(id);
            if (quiz == null || quiz.Type != QuizType.Coding)
                return NotFound();

            if (!ModelState.IsValid)
            {
                ViewBag.Language = quiz.ProgrammingLanguage ?? "Uncategorized";
                return View("~/Areas/Admin/Views/Coding/Quiz/Edit.cshtml", form);
            }

            quiz.Title = form.Title;
            quiz.Description = form.Description;
            quiz.DurationMinutes = form.DurationMinutes;

            quiz.EnableNegativeMarking = false;
            quiz.NegativeMarkPerWrong = 0;
            quiz.ShuffleQuestions = false;
            quiz.ShuffleOptions = false;

            quiz.ShowReviewOnSubmit = form.ShowReviewOnSubmit;
            quiz.ShowScoreOnSubmit = form.ShowScoreOnSubmit;
            quiz.UpdatedAt = DateTimeOffset.Now;

            await _db.SaveChangesAsync();

            var lang = quiz.ProgrammingLanguage ?? "Uncategorized";
            return RedirectToAction(nameof(ByLanguage), new { language = lang });
        }

        // --------------------------------------------------------------------
        // 5) Publish / Unpublish
        // --------------------------------------------------------------------
        [HttpPost("Publish/{id:guid}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Publish(Guid id)
        {
            var quiz = await _db.Quizzes.FindAsync(id);
            if (quiz == null || quiz.Type != QuizType.Coding)
                return NotFound();

            quiz.IsPublished = true;
            quiz.UpdatedAt = DateTimeOffset.Now;
            await _db.SaveChangesAsync();

            var lang = quiz.ProgrammingLanguage ?? "Uncategorized";
            return RedirectToAction(nameof(ByLanguage), new { language = lang });
        }

        [HttpPost("Unpublish/{id:guid}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Unpublish(Guid id)
        {
            var quiz = await _db.Quizzes.FindAsync(id);
            if (quiz == null || quiz.Type != QuizType.Coding)
                return NotFound();

            quiz.IsPublished = false;
            quiz.UpdatedAt = DateTimeOffset.Now;
            await _db.SaveChangesAsync();

            var lang = quiz.ProgrammingLanguage ?? "Uncategorized";
            return RedirectToAction(nameof(ByLanguage), new { language = lang });
        }

        // --------------------------------------------------------------------
        // 6) Manage coding questions for a quiz (same as before)
        // --------------------------------------------------------------------
        [HttpGet("ManageQuestions/{id:guid}")]
        public async Task<IActionResult> ManageQuestions(Guid id)
        {
            var quiz = await _db.Quizzes.AsNoTracking()
                .FirstOrDefaultAsync(q => q.Id == id && q.Type == QuizType.Coding);
            if (quiz == null) return NotFound();

            var attached = await _db.QuizCodingQuestions
                .Where(x => x.QuizId == id)
                .Select(x => x.CodeQuestionId)
                .ToListAsync();

            var questions = await _db.CodeQuestions
                .OrderBy(c => c.Title)
                .Select(c => new ManageQuestionsVm.Item
                {
                    Id = c.Id,
                    Title = c.Title,
                    IsSelected = attached.Contains(c.Id)
                })
                .ToListAsync();

            var vm = new ManageQuestionsVm
            {
                QuizId = id,
                QuizTitle = quiz.Title,
                Items = questions
            };
            return View("~/Areas/Admin/Views/Coding/Quiz/ManageQuestions.cshtml", vm);
        }

        // --------------------------------------------------------------------
        // 7) Delete quiz
        // --------------------------------------------------------------------
        [HttpPost("Delete/{id:guid}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(Guid id)
        {
            var quiz = await _db.Quizzes.FindAsync(id);
            if (quiz == null || quiz.Type != QuizType.Coding)
                return NotFound();

            var lang = quiz.ProgrammingLanguage ?? "Uncategorized";

            // Remove related coding-question links
            var links = _db.QuizCodingQuestions.Where(x => x.QuizId == id);
            _db.QuizCodingQuestions.RemoveRange(links);

            // Remove schedules if any (even though we don't show them in UI now)
            var schedules = _db.QuizSchedules.Where(s => s.QuizId == id);
            _db.QuizSchedules.RemoveRange(schedules);

            // TODO: if later you want, we can also remove attempts related to this quiz

            _db.Quizzes.Remove(quiz);
            await _db.SaveChangesAsync();

            TempData["Message"] = "Coding quiz deleted.";
            return RedirectToAction(nameof(ByLanguage), new { language = lang });
        }

        
        [HttpPost("ManageQuestions/{id:guid}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ManageQuestions(Guid id, ManageQuestionsVm form)
        {
            if (id != form.QuizId) return BadRequest();

            var selectedIds = form.Items.Where(i => i.IsSelected).Select(i => i.Id).ToList();

            var existing = _db.QuizCodingQuestions.Where(x => x.QuizId == id);
            _db.QuizCodingQuestions.RemoveRange(existing);

            int order = 1;
            foreach (var qid in selectedIds)
            {
                _db.QuizCodingQuestions.Add(
                    new Quiz_Application_College.Domain.Coding.QuizCodingQuestion
                    {
                        QuizId = id,
                        CodeQuestionId = qid,
                        Order = order++
                    });
            }

            await _db.SaveChangesAsync();
            TempData["Message"] = "Coding questions updated.";
            return RedirectToAction(nameof(Index));
        }

        // ---------------------- View Models ----------------------
        public class LanguageFolderVm
        {
            public string Name { get; set; } = "";
            public int QuizCount { get; set; }
            public int PublishedCount { get; set; }
        }

        public class QuizRowVm
        {
            public Guid Id { get; set; }
            public string Title { get; set; } = "";
            public string? Description { get; set; }
            public int DurationMinutes { get; set; }
            public bool IsPublished { get; set; }
            public DateTimeOffset CreatedAt { get; set; }
            public int QuestionCount { get; set; }
            public int ScheduleCount { get; set; }
            public string? ProgrammingLanguage { get; set; }
        }

        public class ManageQuestionsVm
        {
            public Guid QuizId { get; set; }
            public string QuizTitle { get; set; } = "";
            public List<Item> Items { get; set; } = new();

            public class Item
            {
                public Guid Id { get; set; }
                public string Title { get; set; } = "";
                public bool IsSelected { get; set; }
            }
        }
    }
}
