using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quiz_Application_College.Data;
using Quiz_Application_College.Domain;
using Quiz_Application_College.ViewModels;

namespace Quiz_Application_College.Areas.Admin.Controllers.Mcq
{
    [Area("Admin")]
    [Authorize(Policy = "IsAdmin")]
    // Everything under /Admin/MCQ/Quiz/...
    [Route("Admin/MCQ/Quiz")]
    public class QuizController : Controller
    {
        private readonly ApplicationDbContext _db;
        public QuizController(ApplicationDbContext db) => _db = db;

        // GET: /Admin/MCQ/Quiz
        [HttpGet("")]
        [HttpGet("Index")]
        public async Task<IActionResult> Index()
        {
            var folders = await _db.McqQuizFolders
                .Include(f => f.Quizzes.Where(q => q.Type == QuizType.Mcq))
                .AsNoTracking()
                .OrderBy(f => f.OrderNo)
                .ThenBy(f => f.Name)
                .ToListAsync();

            return View("~/Areas/Admin/Views/Mcq/Quiz/Index.cshtml", folders);
        }

        // POST: /Admin/MCQ/Quiz/CreateFolder
        [HttpPost("CreateFolder")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateFolder(string name, string? description)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["Error"] = "Folder name is required.";
                return RedirectToAction(nameof(Index));
            }

            var folder = new McqQuizFolder
            {
                Name = name.Trim(),
                Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim()
            };

            _db.McqQuizFolders.Add(folder);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Folder created.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost("DeleteFolder/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteFolder(int id)
        {
            var folder = await _db.McqQuizFolders
                .Include(f => f.Quizzes)
                .FirstOrDefaultAsync(f => f.Id == id);

            if (folder == null)
                return NotFound();

            // If the folder has quizzes, delete them too
            if (folder.Quizzes != null && folder.Quizzes.Any())
            {
                _db.Quizzes.RemoveRange(folder.Quizzes);
            }

            _db.McqQuizFolders.Remove(folder);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Folder and its quizzes deleted.";
            return RedirectToAction(nameof(Index));
        }


        // GET: /Admin/MCQ/Quiz/Folder/5
        [HttpGet("Folder/{id:int}")]
        public async Task<IActionResult> Folder(int id)
        {
            var folder = await _db.McqQuizFolders
                .Include(f => f.Quizzes)
                .FirstOrDefaultAsync(f => f.Id == id);

            if (folder == null)
                return NotFound();

            return View("~/Areas/Admin/Views/Mcq/Quiz/Folder.cshtml", folder);
        }


        // GET: /Admin/MCQ/Quiz/Create?folderId=5
        [HttpGet("Create")]
        public IActionResult Create(int? folderId)
        {
            ViewBag.FolderId = folderId;
            return View("~/Areas/Admin/Views/Mcq/Quiz/Create.cshtml", new QuizCreateVm());
        }

        // POST: /Admin/MCQ/Quiz/Create
        [HttpPost("Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(QuizCreateVm vm, int? folderId)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.FolderId = folderId;
                return View("~/Areas/Admin/Views/Mcq/Quiz/Create.cshtml", vm);
            }

            var quiz = new Quiz
            {
                Title = vm.Title,
                Description = vm.Description,
                DurationMinutes = vm.DurationMinutes,
                TotalMarks = 0, // will be auto-calculated from attached questions
                EnableNegativeMarking = vm.EnableNegativeMarking,
                NegativeMarkPerWrong = vm.NegativeMarkPerWrong,
                ShuffleQuestions = vm.ShuffleQuestions,
                ShuffleOptions = vm.ShuffleOptions,
                ShowReviewOnSubmit = vm.ShowReviewOnSubmit,
                ShowScoreOnSubmit = vm.ShowScoreOnSubmit,
                IsPublished = false,
                CreatedAt = DateTimeOffset.Now,
                Type = QuizType.Mcq,
                McqQuizFolderId = folderId      // 🔴 link quiz to the folder
            };

            _db.Quizzes.Add(quiz);
            await _db.SaveChangesAsync();

            if (folderId.HasValue)
                return RedirectToAction(nameof(Folder), new { id = folderId.Value });

            return RedirectToAction(nameof(Index));
        }

        // POST: /Admin/MCQ/Quiz/Delete/{id}
        [HttpPost("Delete/{id:guid}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(Guid id)
        {
            var quiz = await _db.Quizzes
                .FirstOrDefaultAsync(q => q.Id == id && q.Type == QuizType.Mcq);

            if (quiz == null)
                return NotFound();

            var folderId = quiz.McqQuizFolderId;

            _db.Quizzes.Remove(quiz);
            await _db.SaveChangesAsync();

            if (folderId.HasValue)
                return RedirectToAction(nameof(Folder), new { id = folderId.Value });

            return RedirectToAction(nameof(Index));
        }


        // GET: /Admin/MCQ/Quiz/Edit/{id}
        [HttpGet("Edit/{id:guid}")]
        public async Task<IActionResult> Edit(Guid id)
        {
            var quiz = await _db.Quizzes.FindAsync(id);
            if (quiz == null) return NotFound();
            return View("~/Areas/Admin/Views/Mcq/Quiz/Edit.cshtml", quiz);
        }

        // POST: /Admin/MCQ/Quiz/Edit/{id}
        [HttpPost("Edit/{id:guid}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, Quiz form)
        {
            if (id != form.Id) return BadRequest();
            if (!ModelState.IsValid) return View("~/Areas/Admin/Views/Mcq/Quiz/Edit.cshtml", form);

            var quiz = await _db.Quizzes.FindAsync(id);
            if (quiz == null) return NotFound();

            quiz.Title = form.Title;
            quiz.Description = form.Description;
            quiz.DurationMinutes = form.DurationMinutes;
            quiz.EnableNegativeMarking = form.EnableNegativeMarking;
            quiz.NegativeMarkPerWrong = form.NegativeMarkPerWrong;
            quiz.ShuffleQuestions = form.ShuffleQuestions;
            quiz.ShuffleOptions = form.ShuffleOptions;
            quiz.ShowReviewOnSubmit = form.ShowReviewOnSubmit;
            quiz.ShowScoreOnSubmit = form.ShowScoreOnSubmit;
            quiz.UpdatedAt = DateTimeOffset.Now;

            await _db.SaveChangesAsync();

            if (quiz.McqQuizFolderId.HasValue)
                return RedirectToAction(nameof(Folder), new { id = quiz.McqQuizFolderId.Value });

            return RedirectToAction(nameof(Index));
        }

        // POST: /Admin/MCQ/Quiz/Publish/{id}
        [HttpPost("Publish/{id:guid}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Publish(Guid id)
        {
            var quiz = await _db.Quizzes.FindAsync(id);
            if (quiz == null) return NotFound();

            quiz.IsPublished = true;
            quiz.UpdatedAt = DateTimeOffset.Now;
            await _db.SaveChangesAsync();

            if (quiz.McqQuizFolderId.HasValue)
                return RedirectToAction(nameof(Folder), new { id = quiz.McqQuizFolderId.Value });

            return RedirectToAction(nameof(Index));
        }

        // POST: /Admin/MCQ/Quiz/Unpublish/{id}
        [HttpPost("Unpublish/{id:guid}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Unpublish(Guid id)
        {
            var quiz = await _db.Quizzes.FindAsync(id);
            if (quiz == null) return NotFound();

            quiz.IsPublished = false;
            quiz.UpdatedAt = DateTimeOffset.Now;
            await _db.SaveChangesAsync();

            if (quiz.McqQuizFolderId.HasValue)
                return RedirectToAction(nameof(Folder), new { id = quiz.McqQuizFolderId.Value });

            return RedirectToAction(nameof(Index));
        }


        // GET: /Admin/MCQ/Quiz/Assign/{id}
        [HttpGet("Assign/{id:guid}")]
        public async Task<IActionResult> Assign(Guid id)
        {
            var quiz = await _db.Quizzes.FindAsync(id);
            if (quiz == null) return NotFound();

            var qlist = await _db.McqQuestions
                .OrderBy(x => x.Text)
                .Select(x => new { x.Id, x.Text })
                .ToListAsync();

            var selected = await _db.QuizQuestions
                .Where(qq => qq.QuizId == id)
                .Select(qq => qq.QuestionId)
                .ToListAsync();

            ViewBag.Quiz = quiz;
            ViewBag.AllQuestions = qlist;
            ViewBag.Selected = selected;

            return View("~/Areas/Admin/Views/Mcq/Quiz/Assign.cshtml");
        }

        // POST: /Admin/MCQ/Quiz/Assign/{id}
        [HttpPost("Assign/{id:guid}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Assign(Guid id, Guid[] questionIds)
        {
            var quiz = await _db.Quizzes.FindAsync(id);
            if (quiz == null) return NotFound();

            var existing = await _db.QuizQuestions.Where(qq => qq.QuizId == id).ToListAsync();
            _db.QuizQuestions.RemoveRange(existing);

            int order = 1;
            foreach (var qid in (questionIds ?? Array.Empty<Guid>()).Distinct())
                _db.QuizQuestions.Add(new QuizQuestion { QuizId = id, QuestionId = qid, Order = order++ });

            await _db.SaveChangesAsync();
            TempData["Info"] = "Questions assigned.";
            return RedirectToAction(nameof(Index));
        }

        // GET: /Admin/MCQ/Quiz/Questions/{id}
        [HttpGet("Questions/{id:guid}")]
        public async Task<IActionResult> Questions(Guid id)
        {
            var quiz = await _db.Quizzes.FirstOrDefaultAsync(q => q.Id == id);
            if (quiz == null) return NotFound();

            var current = await _db.QuizQuestions
                .Where(qq => qq.QuizId == id)
                .Select(qq => new { qq.QuestionId, qq.Order })
                .ToListAsync();
            var map = current.ToDictionary(x => x.QuestionId, x => (int?)x.Order);

            var list = await _db.McqQuestions
                .AsNoTracking()
                .OrderBy(q => q.Text)
                .Select(q => new QuestionSelectVm
                {
                    QuestionId = q.Id,
                    Text = q.Text,
                    Selected = map.ContainsKey(q.Id),
                    Order = map.ContainsKey(q.Id) ? map[q.Id] : null,
                    SourceFileName = q.SourceFileName       // 🔴 pass Excel file name to view
                })
                .ToListAsync();

            ViewBag.QuizId = id;
            ViewBag.QuizTitle = quiz.Title;
            return View("~/Areas/Admin/Views/Mcq/Quiz/Questions.cshtml", list);
        }

        // POST: /Admin/MCQ/Quiz/Questions/{id}
        [HttpPost("Questions/{id:guid}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Questions(Guid id, Guid[] selectedIds)
        {
            var quiz = await _db.Quizzes.FirstOrDefaultAsync(q => q.Id == id);
            if (quiz == null) return NotFound();

            // Distinct list of selected question ids
            var selected = (selectedIds ?? Array.Empty<Guid>()).Distinct().ToHashSet();

            // --- update QuizQuestions (which questions belong to this quiz) ---

            var existing = await _db.QuizQuestions
                .Where(qq => qq.QuizId == id)
                .ToListAsync();

            var existingIds = existing.Select(e => e.QuestionId).ToHashSet();

            // Add new links
            var toAdd = selected.Except(existingIds).ToList();
            if (toAdd.Count > 0)
            {
                var nextOrder = existing.Count == 0 ? 1 : existing.Max(e => e.Order) + 1;

                foreach (var qid in toAdd)
                {
                    _db.QuizQuestions.Add(new QuizQuestion
                    {
                        QuizId = id,
                        QuestionId = qid,
                        Order = nextOrder++
                    });
                }
            }

            // Remove unselected links
            var toRemove = existing.Where(e => !selected.Contains(e.QuestionId)).ToList();
            if (toRemove.Count > 0)
            {
                _db.QuizQuestions.RemoveRange(toRemove);
            }

            // --- recalculate total marks from selected questions ---

            decimal totalMarks = 0m;
            if (selected.Count > 0)
            {
                totalMarks = await _db.McqQuestions
                    .Where(q => selected.Contains(q.Id))
                    .SumAsync(q => q.Marks);   // McqQuestion.Marks is decimal
            }

            quiz.TotalMarks = (int)Math.Round(totalMarks); // cast decimal → int safely

            await _db.SaveChangesAsync();

            TempData["Info"] = $"Question list updated. Total marks: {quiz.TotalMarks}.";
            return RedirectToAction(nameof(Questions), new { id });
        }
    }
}
