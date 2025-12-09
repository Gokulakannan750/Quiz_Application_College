using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quiz_Application_College.Data;
using Quiz_Application_College.Domain;
using Quiz_Application_College.Domain.Coding;

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
        // 1. LIST ALL CODING FOLDERS (TILES)
        // --------------------------------------------------------------------
        [HttpGet("")]
        [HttpGet("Index")]
        public async Task<IActionResult> Index()
        {
            var folders = await _db.CodingQuizFolders
                .Include(f => f.Quizzes.Where(q => q.Type == QuizType.Coding))
                .AsNoTracking()
                .OrderBy(f => f.OrderNo)
                .ThenBy(f => f.Name)
                .ToListAsync();

            return View("~/Areas/Admin/Views/Coding/Quiz/Folders.cshtml", folders);
        }

        // --------------------------------------------------------------------
        // 2. CREATE FOLDER
        // --------------------------------------------------------------------
        [HttpPost("CreateFolder")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateFolder(string name, string? description)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["Error"] = "Folder name is required.";
                return RedirectToAction(nameof(Index));
            }

            var folder = new CodingQuizFolder
            {
                Name = name.Trim(),
                Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim()
            };

            _db.CodingQuizFolders.Add(folder);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Folder created successfully.";
            return RedirectToAction(nameof(Index));
        }

        // --------------------------------------------------------------------
        // 3. DELETE FOLDER (with its quizzes)
        // --------------------------------------------------------------------
        [HttpPost("DeleteFolder/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteFolder(int id)
        {
            var folder = await _db.CodingQuizFolders
                .Include(f => f.Quizzes)
                .FirstOrDefaultAsync(f => f.Id == id);

            if (folder == null)
                return NotFound();

            if (folder.Quizzes.Any())
            {
                _db.Quizzes.RemoveRange(folder.Quizzes);
            }

            _db.CodingQuizFolders.Remove(folder);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Folder deleted successfully.";
            return RedirectToAction(nameof(Index));
        }

        // --------------------------------------------------------------------
        // 4. VIEW QUIZZES IN A FOLDER
        // --------------------------------------------------------------------
        [HttpGet("Folder/{id:int}")]
        public async Task<IActionResult> Folder(int id)
        {
            var folder = await _db.CodingQuizFolders
                .Include(f => f.Quizzes)
                .FirstOrDefaultAsync(f => f.Id == id);

            if (folder == null)
                return NotFound();

            return View("~/Areas/Admin/Views/Coding/Quiz/Index.cshtml", folder);
        }

        // --------------------------------------------------------------------
        // 5. CREATE QUIZ INSIDE A FOLDER
        // --------------------------------------------------------------------
        [HttpGet("Create/{folderId:int}")]
        public IActionResult Create(int folderId)
        {
            ViewBag.FolderId = folderId;

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
                Type = QuizType.Coding
            };

            return View("~/Areas/Admin/Views/Coding/Quiz/Create.cshtml", quiz);
        }

        [HttpPost("Create/{folderId:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int folderId, Quiz form)
        {
            ViewBag.FolderId = folderId;

            if (!ModelState.IsValid)
            {
                return View("~/Areas/Admin/Views/Coding/Quiz/Create.cshtml", form);
            }

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
                CodingQuizFolderId = folderId
            };

            _db.Quizzes.Add(quiz);
            await _db.SaveChangesAsync();

            return RedirectToAction(nameof(Folder), new { id = folderId });
        }

        // --------------------------------------------------------------------
        // 6. EDIT QUIZ
        // --------------------------------------------------------------------
        [HttpGet("Edit/{id:guid}")]
        public async Task<IActionResult> Edit(Guid id)
        {
            var quiz = await _db.Quizzes
                .FirstOrDefaultAsync(q => q.Id == id && q.Type == QuizType.Coding);

            if (quiz == null)
                return NotFound();

            ViewBag.FolderId = quiz.CodingQuizFolderId;
            return View("~/Areas/Admin/Views/Coding/Quiz/Edit.cshtml", quiz);
        }

        [HttpPost("Edit/{id:guid}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, Quiz form)
        {
            if (id != form.Id)
                return BadRequest();

            var quiz = await _db.Quizzes
                .FirstOrDefaultAsync(q => q.Id == id && q.Type == QuizType.Coding);

            if (quiz == null)
                return NotFound();

            if (!ModelState.IsValid)
            {
                ViewBag.FolderId = quiz.CodingQuizFolderId;
                return View("~/Areas/Admin/Views/Coding/Quiz/Edit.cshtml", form);
            }

            quiz.Title = form.Title;
            quiz.Description = form.Description;
            quiz.DurationMinutes = form.DurationMinutes;
            quiz.ShowReviewOnSubmit = form.ShowReviewOnSubmit;
            quiz.ShowScoreOnSubmit = form.ShowScoreOnSubmit;
            quiz.UpdatedAt = DateTimeOffset.Now;

            await _db.SaveChangesAsync();

            if (quiz.CodingQuizFolderId.HasValue)
                return RedirectToAction(nameof(Folder), new { id = quiz.CodingQuizFolderId.Value });

            return RedirectToAction(nameof(Index));
        }

        // --------------------------------------------------------------------
        // 7. PUBLISH / UNPUBLISH QUIZ
        // --------------------------------------------------------------------
        [HttpPost("Publish/{id:guid}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Publish(Guid id)
        {
            var quiz = await _db.Quizzes
                .FirstOrDefaultAsync(q => q.Id == id && q.Type == QuizType.Coding);

            if (quiz == null)
                return NotFound();

            quiz.IsPublished = true;
            quiz.UpdatedAt = DateTimeOffset.Now;
            await _db.SaveChangesAsync();

            if (quiz.CodingQuizFolderId.HasValue)
                return RedirectToAction(nameof(Folder), new { id = quiz.CodingQuizFolderId.Value });

            return RedirectToAction(nameof(Index));
        }

        [HttpPost("Unpublish/{id:guid}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Unpublish(Guid id)
        {
            var quiz = await _db.Quizzes
                .FirstOrDefaultAsync(q => q.Id == id && q.Type == QuizType.Coding);

            if (quiz == null)
                return NotFound();

            quiz.IsPublished = false;
            quiz.UpdatedAt = DateTimeOffset.Now;
            await _db.SaveChangesAsync();

            if (quiz.CodingQuizFolderId.HasValue)
                return RedirectToAction(nameof(Folder), new { id = quiz.CodingQuizFolderId.Value });

            return RedirectToAction(nameof(Index));
        }

        // --------------------------------------------------------------------
        // 8. DELETE QUIZ
        // --------------------------------------------------------------------
        [HttpPost("Delete/{id:guid}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(Guid id)
        {
            var quiz = await _db.Quizzes
                .FirstOrDefaultAsync(q => q.Id == id && q.Type == QuizType.Coding);

            if (quiz == null)
                return NotFound();

            var folderId = quiz.CodingQuizFolderId;

            var links = _db.QuizCodingQuestions.Where(x => x.QuizId == id);
            _db.QuizCodingQuestions.RemoveRange(links);

            var schedules = _db.QuizSchedules.Where(s => s.QuizId == id);
            _db.QuizSchedules.RemoveRange(schedules);

            _db.Quizzes.Remove(quiz);
            await _db.SaveChangesAsync();

            if (folderId.HasValue)
                return RedirectToAction(nameof(Folder), new { id = folderId.Value });

            return RedirectToAction(nameof(Index));
        }

        // --------------------------------------------------------------------
        // 9. MANAGE CODING QUESTIONS FOR A QUIZ
        // --------------------------------------------------------------------
        [HttpGet("ManageQuestions/{id:guid}")]
        public async Task<IActionResult> ManageQuestions(Guid id)
        {
            var quiz = await _db.Quizzes.AsNoTracking()
                .FirstOrDefaultAsync(q => q.Id == id && q.Type == QuizType.Coding);

            if (quiz == null) return NotFound();

            // Existing links for this quiz (to know which are selected)
            var links = await _db.QuizCodingQuestions
                .Where(x => x.QuizId == id)
                .OrderBy(x => x.Order)
                .ToListAsync();

            var attachedIds = links.Select(x => x.CodeQuestionId).ToHashSet();

            var questions = await _db.CodeQuestions
                .Include(c => c.TestCases)
                .OrderBy(c => c.Title)
                .Select(c => new ManageQuestionsVm.Item
                {
                    Id = c.Id,
                    Title = c.Title,
                    MaxMarks = c.MaxMarks,
                    TestCaseCount = c.TestCases.Count,
                    IsSelected = attachedIds.Contains(c.Id)
                })
                .ToListAsync();

            var selected = questions.Where(x => x.IsSelected).ToList();

            var vm = new ManageQuestionsVm
            {
                QuizId = id,
                QuizTitle = quiz.Title,
                Items = questions,
                SelectedCount = selected.Count,
                SelectedTotalMarks = selected.Sum(x => x.MaxMarks)
            };

            return View("~/Areas/Admin/Views/Coding/Quiz/ManageQuestions.cshtml", vm);
        }

        [HttpPost("ManageQuestions/{id:guid}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ManageQuestions(Guid id, ManageQuestionsVm form)
        {
            if (id != form.QuizId) return BadRequest();

            // Load the quiz so we can update TotalMarks as well
            var quiz = await _db.Quizzes
                .FirstOrDefaultAsync(q => q.Id == id && q.Type == QuizType.Coding);

            if (quiz == null) return NotFound();

            // Items that are ticked in the UI
            var selectedItems = form.Items
                .Where(i => i.IsSelected)
                .ToList();

            var selectedIds = selectedItems
                .Select(i => i.Id)
                .ToList();

            // Remove existing links
            var existing = _db.QuizCodingQuestions.Where(x => x.QuizId == id);
            _db.QuizCodingQuestions.RemoveRange(existing);

            // Re-create links in order
            int order = 1;
            foreach (var qid in selectedIds)
            {
                _db.QuizCodingQuestions.Add(new QuizCodingQuestion
                {
                    QuizId = id,
                    CodeQuestionId = qid,
                    Order = order++
                });
            }

            // Recalculate quiz total marks from attached coding questions
            // Each ManageQuestionsVm.Item.MaxMarks already comes from CodeQuestion.MaxMarks (decimal)
            var totalMarks = selectedItems.Sum(i => i.MaxMarks);

            // Quiz.TotalMarks is int, so convert explicitly.
            // We round to nearest integer in case you ever use fractional weights.
            quiz.TotalMarks = (int)Math.Round(totalMarks, MidpointRounding.AwayFromZero);

            await _db.SaveChangesAsync();

            TempData["Message"] = "Coding questions updated.";

            if (quiz.CodingQuizFolderId.HasValue)
                return RedirectToAction(nameof(Folder), new { id = quiz.CodingQuizFolderId.Value });

            return RedirectToAction(nameof(Index));
        }

        // --------------------------------------------------------------------
        // VIEW MODEL FOR MANAGE QUESTIONS
        // --------------------------------------------------------------------
        public class ManageQuestionsVm
        {
            public Guid QuizId { get; set; }
            public string QuizTitle { get; set; } = "";
            public List<Item> Items { get; set; } = new();

            // Summary for UI
            public int SelectedCount { get; set; }
            public decimal SelectedTotalMarks { get; set; }

            public class Item
            {
                public Guid Id { get; set; }
                public string Title { get; set; } = "";

                // Total marks of this coding question (from CodeQuestion.MaxMarks)
                public decimal MaxMarks { get; set; }

                // How many testcases belong to this question
                public int TestCaseCount { get; set; }

                public bool IsSelected { get; set; }
            }
        }
    }
}
